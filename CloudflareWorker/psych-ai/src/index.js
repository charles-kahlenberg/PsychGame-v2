// Brainy's AI: the scenario intro, hints/concept help and grading, in one
// worker. POST JSON to /intro, /hint or /grade; replies { intro }, { hint }
// or { feedback } (the field the game's Worker.Reply reads), else { error }.

const MODEL = "gpt-4.1-mini";

// The game gives up after 20s (Worker.Post), so two 9s tries fit inside it.
const ATTEMPTS = 2;
const ATTEMPT_MS = 9000;

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

const DEFAULT_SCENARIO =
  "The UT psychology faculty is interested in studying the effects of mindfulness meditation on stress levels among students. " +
  "They predict that participating in a mindfulness meditation program will lead to reduced levels of perceived stress and may even have effects on memory and decision making. " +
  "How might you test this hypothesis?";

const GRADE_MAX_WORDS = 110;

function jsonResponse(obj, status = 200) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
  });
}

// The endpoint is public, so player text is capped before it reaches the key.
const text = (v, max = 4000) => String(v || "").trim().slice(0, max);
const cardList = v => (Array.isArray(v) ? v.map(c => text(c, 200)).filter(Boolean).slice(0, 20) : []);

// Each route turns the request body into an OpenAI job, or an error string.
const ROUTES = {
  "/intro": body => {
    const scenario = text(body.scenario) || DEFAULT_SCENARIO;
    const cards = cardList(body.cards);
    return {
      field: "intro",
      temperature: 0.7,
      max_tokens: 170,
      system:
        "You are Brainy, a friendly NPC in a psychology card game. " +
        "Give the player a short setup/introduction for the scenario and a gentle reminder to use their cards. " +
        "Keep it upbeat and simple.",
      user:
        `Scenario:\n${scenario}\n\n` +
        `Cards:\n${cards.length ? cards.join(", ") : "(no cards provided)"}\n\n` +
        "Write 2–4 short sentences. Do not give the full answer. Do not mention OpenAI.",
    };
  },

  "/hint": body => {
    const mode = text(body.mode || "hint").toLowerCase();
    const scenario = text(body.scenario);
    if (!scenario) return "scenario is required";

    const job = {
      field: "hint",
      fallback: "No hint generated.",
      temperature: 0.7,
      max_tokens: 140,
      system: "You generate short, supportive hints.",
    };

    if (mode === "concept") {
      const concept = text(body.concept, 200);
      if (!concept) return "concept is required";
      job.user = `
Determine if the concept can apply to the current scenario. It does not need to be an excellent match,
but if there is a plausible way to use the concept, explain how.

Reply in 1–3 short sentences:
- If it fits: explain briefly how it connects + one example sentence using the concept naturally.
- If it does NOT fit at all: say it is not a strong fit (use your own wording), briefly why, and what type of scenario it fits better.

Scenario: "${scenario}"
Concept: "${concept}"
Keep it concise (under ~45 words).
`.trim();
    } else {
      const cards = cardList(body.cards);
      if (!cards.length) return "cards are required for hint mode";
      job.user = `
You are a helpful coach in a psychology learning game.
Write a 1–2 sentence hint to help the player respond to the scenario.
Use 1–2 of the cards to guide them. Do not give the full answer.

Scenario:
${scenario}

Cards:
${cards.map((c, i) => `${i + 1}. ${c}`).join("\n")}
`.trim();
    }
    return job;
  },

  "/grade": body => {
    const scenario = text(body.scenario);
    const userResponse = text(body.userResponse);
    const usedCards = cardList(body.usedCards);
    if (!scenario) return "scenario is required";
    if (!userResponse) return "userResponse is required";
    return {
      field: "feedback",
      temperature: 0.4,
      max_tokens: 220,
      system:
        "You are grading a short free-response in an intro psychology game. Use OpenStax Psychology 2e for grounding. " +
        "Be concise, supportive, and specific.\n\n" +
        "Rubric:\n" +
        "• 30 pts: number of relevant provided cards used.\n" +
        "• 70 pts: accuracy of how those cards were used.\n" +
        "• Be positive. Spelling doesn't matter.\n" +
        "• Do not require citations.\n\n" +
        "Format:\n" +
        "• First line: exactly `Score: [NUMBER]/100`\n" +
        `• Explanation under ${GRADE_MAX_WORDS} words.`,
      user:
        `Scenario:\n${scenario}\n\n` +
        `Student used cards: ${usedCards.length ? usedCards.join(", ") : "[No cards matched in response]"}\n\n` +
        `Student response:\n${userResponse}`,
    };
  },
};

// The model's reply text. Retries once on a timeout, network error, 429 or
// 5xx; a bad key or bad request fails at once, since retrying can't fix it.
async function askOpenAI(env, job) {
  let lastError = "";
  for (let attempt = 1; attempt <= ATTEMPTS; attempt++) {
    try {
      const resp = await fetch("https://api.openai.com/v1/chat/completions", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${env.OPENAI_API_KEY}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          model: MODEL,
          temperature: job.temperature,
          max_tokens: job.max_tokens,
          messages: [
            { role: "system", content: job.system },
            { role: "user", content: job.user },
          ],
        }),
        signal: AbortSignal.timeout(ATTEMPT_MS),
      });
      if (resp.ok) {
        const data = await resp.json();
        return data?.choices?.[0]?.message?.content?.trim() || "";
      }
      lastError = `OpenAI ${resp.status}: ${await resp.text()}`;
      if (resp.status !== 429 && resp.status < 500) break;
    } catch (e) {
      lastError = String(e);
    }
  }
  throw new Error(lastError);
}

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") {
      return new Response(null, { headers: CORS_HEADERS });
    }
    if (request.method !== "POST") {
      return jsonResponse({ error: "Use POST" }, 405);
    }

    const route = ROUTES[new URL(request.url).pathname.replace(/\/+$/, "")];
    if (!route) {
      return jsonResponse({ error: "Unknown path; use /intro, /hint or /grade" }, 404);
    }

    let body;
    try {
      body = await request.json();
    } catch {
      return jsonResponse({ error: "Invalid JSON" }, 400);
    }

    const job = route(body || {});
    if (typeof job === "string") {
      return jsonResponse({ error: job }, 400);
    }

    try {
      const reply = (await askOpenAI(env, job)) || job.fallback || "";
      return jsonResponse({ [job.field]: reply });
    } catch (e) {
      // The game drops non-2xx bodies, so this log is where failures show up.
      console.error(`${job.field} failed:`, e.message);
      return jsonResponse({ error: "AI request failed", details: e.message }, 502);
    }
  },
};
