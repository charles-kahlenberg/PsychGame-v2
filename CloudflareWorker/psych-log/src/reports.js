// Pure rendering of the human-readable Dropbox reports. No I/O in this file,
// so it can be run locally against exported D1 data.

const TZ = "America/New_York";
export const BOM = "﻿"; // lets Excel detect UTF-8 (smart quotes etc. in AI text)
const EOL = "\r\n";

// In game-flow order. Keys match ClickLogger.SceneToMenuName.
const SCREEN_LABELS = {
  main_menu: "Main menu",
  introduction: "Introduction",
  rules: "Rules",
  save_select: "Save select",
  loading: "Loading",
  respond: "Responding",
  grading: "Grading",
  review: "Review",
  game_end: "Game end",
};
// Builds before 2026-09-24 only timed these three screens.
const LEGACY_SCREENS = new Set(["main_menu", "respond", "review"]);

// Matched by keyword rather than exact text so small wording edits to
// Scenarios.txt don't break the labels. Order matters: scenario 2 also
// mentions "sleep", so "sleep deprivation" must stay specific.
const SCENARIO_LABELS = [
  ["mindfulness", "Mindfulness & stress"],
  ["social media", "Social media habits"],
  ["eyewitness", "Eyewitness testimony"],
  ["chocolate", "Chocolate & happiness"],
  ["haunted", "Haunted apartment"],
  ["sleep deprivation", "Sleep deprivation"],
];

const AI_EVENT_LABELS = {
  hint: "Received hint",
  concept_help: "Received concept help",
  grading_feedback: "Received AI feedback",
};

export const OVERVIEW_COLUMNS = [
  "Session #",
  "Username",
  "Test group",
  "Date (ET)",
  "Start time (ET)",
  "Last activity (ET)",
  "Session length (min)",
  ...Object.values(SCREEN_LABELS).map((label) => `${label} time (min)`),
  "Scenarios attempted",
  "Scenarios",
  "Responses submitted",
  "Average AI score",
  "Hints requested",
  "Concept help requested",
  "AI feedback received",
  "Card refreshes",
  "Total clicks",
  "Browser / device",
  "Features on",
  "Session ID",
  "Timeline file",
];

export const ATTEMPT_COLUMNS = [
  "Session #",
  "Username",
  "Test group",
  "Date (ET)",
  "Scenario #",
  "Scenario",
  "Started (ET)",
  "Time on scenario (min)",
  "Initial cards",
  "Card refreshes",
  "Hints requested",
  "Concept help requested",
  "Responses submitted",
  "Response word count",
  "AI score",
  "Response",
  "AI feedback",
  "Hints & concept help given",
  "Session ID",
];

const TIMELINE_COLUMNS = ["Time (ET)", "Elapsed", "Screen", "Event", "Scenario", "Details"];

const dateFmt = new Intl.DateTimeFormat("en-CA", { timeZone: TZ, year: "numeric", month: "2-digit", day: "2-digit" });
const timeFmt = new Intl.DateTimeFormat("en-US", {
  timeZone: TZ,
  hour: "numeric",
  minute: "2-digit",
  second: "2-digit",
  hour12: true,
});
const fileTimeFmt = new Intl.DateTimeFormat("en-US", { timeZone: TZ, hour: "numeric", minute: "2-digit", hour12: true });

const fmtDate = (ms) => dateFmt.format(new Date(ms));
const fmtTime = (ms) => timeFmt.format(new Date(ms));
const minutes = (ms) => Number((ms / 60000).toFixed(1));

function fmtDuration(ms) {
  const total = Math.round(ms / 1000);
  const m = Math.floor(total / 60);
  const s = total % 60;
  return m > 0 ? `${m}m ${s}s` : `${s}s`;
}

function fmtElapsed(ms) {
  const total = Math.max(0, Math.round(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = String(total % 60).padStart(2, "0");
  return h > 0 ? `${h}:${String(m).padStart(2, "0")}:${s}` : `${m}:${s}`;
}

// Free-text cells (participant answers, AI text) can start with "-" or "=",
// which Excel would otherwise try to evaluate as a formula.
function cell(value) {
  if (value === null || value === undefined) return "";
  let str = String(value);
  if (typeof value === "string" && /^[=+\-@\t\r]/.test(str)) str = `'${str}`;
  return /[",\n\r]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str;
}

export const csvLine = (values) => values.map(cell).join(",");

export function csvFile(columns, lines) {
  return BOM + [csvLine(columns), ...lines].join(EOL) + EOL;
}

export function parseScenarioList(fileText) {
  return fileText
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean);
}

function describeScenario(text, scenarioTexts) {
  if (!text || text === "Missing scenario") return null;
  const trimmed = text.trim();
  const idx = scenarioTexts.indexOf(trimmed);
  const lower = trimmed.toLowerCase();
  const match = SCENARIO_LABELS.find(([keyword]) => lower.includes(keyword));
  const label = match ? match[1] : trimmed.length > 60 ? `${trimmed.slice(0, 57)}...` : trimmed;
  return { key: idx >= 0 ? `#${idx + 1}` : label, num: idx >= 0 ? idx + 1 : null, label };
}

export function describeDevice(ua) {
  if (!ua) return "";
  if (/UnityPlayer\//.test(ua)) return "Unity editor / desktop build";
  if (/^curl\//.test(ua)) return "curl (developer test)";
  const browser = /Edg\//.test(ua)
    ? "Edge"
    : /OPR\//.test(ua)
      ? "Opera"
      : /Firefox\//.test(ua)
        ? "Firefox"
        : /Chrome\//.test(ua)
          ? "Chrome"
          : /Safari\//.test(ua)
            ? "Safari"
            : "Other browser";
  const os = /iPhone|iPad|iPod/.test(ua)
    ? "iOS"
    : /Android/.test(ua)
      ? "Android"
      : /Windows/.test(ua)
        ? "Windows"
        : /Macintosh|Mac OS X/.test(ua)
          ? "Mac"
          : /CrOS/.test(ua)
            ? "ChromeOS"
            : /Linux/.test(ua)
              ? "Linux"
              : "Other OS";
  return `${browser} on ${os}`;
}

const wordCount = (text) => (text.trim() ? text.trim().split(/\s+/).length : 0);
const formatCards = (cards) =>
  (cards || "")
    .split("|")
    .map((c) => c.trim())
    .filter(Boolean)
    .join(", ");

function timelineFileName(sessionNum, startMs, username) {
  const safeUser = (username || "no-username").replace(/[^A-Za-z0-9 _.-]/g, "_").slice(0, 40);
  const time = fileTimeFmt.format(new Date(startMs)).replace(":", ".");
  return `Session ${String(sessionNum).padStart(4, "0")} - ${fmtDate(startMs)} ${time} - ${safeUser}.csv`;
}

// rows: { clicks, aiResponses, userResponses, cardEvents, menuDurations }, each
// the raw D1 rows for this one session.
export function buildSessionReport(session, rows, scenarioTexts) {
  const t = (iso) => Date.parse(iso);
  const scenarioOf = (text) => describeScenario(text, scenarioTexts);

  const visits = rows.menuDurations.map((m) => {
    const end = t(m.timestamp);
    return { start: end - m.duration_ms, end, menu: m.menu_name, scenario: scenarioOf(m.scenario), durationMs: m.duration_ms };
  });
  const visitAt = (time) => visits.find((v) => v.start <= time && time <= v.end);

  const events = [];
  const push = (time, order, event, scenario, details) => {
    const visit = visitAt(time);
    events.push({
      time,
      order,
      screen: visit ? SCREEN_LABELS[visit.menu] || visit.menu : "",
      event,
      scenario: scenario || visit?.scenario || null,
      details,
    });
  };

  const startedAt = t(session.started_at);
  const device = describeDevice(session.user_agent);
  // Sessions from before test groups existed are NULL; they all played the
  // original game, which is group 1.
  const testGroup = session.test_group ?? 1;
  const features =
    (session.features || "")
      .split("|")
      .filter(Boolean)
      .join(", ") || "(none)";
  push(
    startedAt,
    0,
    "Session started",
    null,
    `Username: ${session.username || "(none)"}; Test group ${testGroup}${device ? `; ${device}` : ""}`
  );

  for (const v of visits) {
    const label = SCREEN_LABELS[v.menu] || v.menu;
    push(v.start, 1, `Opened ${label}`, v.scenario, "");
    push(v.end, 9, `Left ${label}`, v.scenario, `Spent ${fmtDuration(v.durationMs)}`);
  }
  for (const c of rows.cardEvents) {
    const refresh = c.event_type === "refresh";
    const after = refresh && c.elapsed_ms != null ? ` (previous cards shown for ${fmtDuration(c.elapsed_ms)})` : "";
    push(t(c.timestamp), 3, refresh ? "Cards refreshed" : "Cards dealt", scenarioOf(c.scenario), formatCards(c.cards) + after);
  }
  for (const c of rows.clicks) {
    push(t(c.timestamp), 5, "Clicked", null, c.object_name || "(empty space)");
  }
  for (const u of rows.userResponses) {
    push(t(u.timestamp), 6, "Submitted response", scenarioOf(u.scenario), u.response || "");
  }
  for (const a of rows.aiResponses) {
    push(t(a.timestamp), 7, AI_EVENT_LABELS[a.kind] || a.kind, scenarioOf(a.scenario), a.content || "");
  }

  events.sort((a, b) => a.time - b.time || a.order - b.order);
  const firstMs = Math.min(startedAt, ...events.map((e) => e.time));
  const lastMs = Math.max(...events.map((e) => e.time));

  const fileName = timelineFileName(session.row_id, startedAt, session.username);
  const timelineCsv = csvFile(
    TIMELINE_COLUMNS,
    events.map((e) =>
      csvLine([fmtTime(e.time), fmtElapsed(e.time - firstMs), e.screen, e.event, e.scenario?.label || "", e.details])
    )
  );

  // --- Per-scenario rollup ---
  const attempts = new Map();
  const attemptFor = (desc, time) => {
    const key = desc?.key || "unknown";
    if (!attempts.has(key)) {
      attempts.set(key, {
        desc: desc || { num: null, label: "(scenario not recorded)" },
        firstSeen: time,
        timeMs: 0,
        initialCards: "",
        refreshes: 0,
        hints: 0,
        conceptHelp: 0,
        responses: [],
        feedback: [],
        scores: [],
        helpTexts: [],
      });
    }
    const a = attempts.get(key);
    a.firstSeen = Math.min(a.firstSeen, time);
    return a;
  };

  for (const v of visits) {
    if (v.menu === "respond") attemptFor(v.scenario, v.start).timeMs += v.durationMs;
  }
  for (const c of rows.cardEvents) {
    const a = attemptFor(scenarioOf(c.scenario), t(c.timestamp));
    if (c.event_type === "refresh") a.refreshes++;
    else if (!a.initialCards) a.initialCards = formatCards(c.cards);
  }
  for (const u of rows.userResponses) {
    attemptFor(scenarioOf(u.scenario), t(u.timestamp)).responses.push(u.response || "");
  }
  for (const r of rows.aiResponses) {
    const a = attemptFor(scenarioOf(r.scenario), t(r.timestamp));
    if (r.kind === "grading_feedback") {
      a.feedback.push(r.content || "");
      if (r.score != null) a.scores.push(r.score);
    } else {
      if (r.kind === "hint") a.hints++;
      if (r.kind === "concept_help") a.conceptHelp++;
      a.helpTexts.push(`[${r.kind === "hint" ? "Hint" : "Concept help"}] ${r.content || ""}`);
    }
  }

  const sortedAttempts = [...attempts.values()].sort((a, b) => a.firstSeen - b.firstSeen);
  const date = fmtDate(startedAt);
  const separator = "\n\n---\n\n";

  const attemptLines = sortedAttempts.map((a) => {
    const response = a.responses.join(separator);
    return csvLine([
      session.row_id,
      session.username || "",
      testGroup,
      date,
      a.desc.num ?? "",
      a.desc.label,
      fmtTime(a.firstSeen),
      minutes(a.timeMs),
      a.initialCards,
      a.refreshes,
      a.hints,
      a.conceptHelp,
      a.responses.length,
      wordCount(response),
      a.scores.length > 0 ? a.scores[a.scores.length - 1] : "",
      response,
      a.feedback.join(separator),
      a.helpTexts.join(separator),
      session.session_id,
    ]);
  });

  // A session with no timing rows beyond the legacy three came from a build
  // that didn't time the other screens: leave those blank rather than 0.
  const timedAllScreens = visits.some((v) => !LEGACY_SCREENS.has(v.menu));
  const screenTimes = Object.keys(SCREEN_LABELS).map((menu) =>
    timedAllScreens || LEGACY_SCREENS.has(menu)
      ? minutes(visits.filter((v) => v.menu === menu).reduce((sum, v) => sum + v.durationMs, 0))
      : ""
  );
  const countAi = (kind) => rows.aiResponses.filter((r) => r.kind === kind).length;
  const knownAttempts = sortedAttempts.filter((a) => a.desc.label !== "(scenario not recorded)");
  const scores = rows.aiResponses.filter((r) => r.kind === "grading_feedback" && r.score != null).map((r) => r.score);
  const avgScore = scores.length > 0 ? Number((scores.reduce((s, x) => s + x, 0) / scores.length).toFixed(1)) : "";

  const overviewLine = csvLine([
    session.row_id,
    session.username || "",
    testGroup,
    date,
    fmtTime(startedAt),
    fmtTime(lastMs),
    minutes(lastMs - firstMs),
    ...screenTimes,
    knownAttempts.length,
    knownAttempts.map((a) => a.desc.label).join("; "),
    rows.userResponses.length,
    avgScore,
    countAi("hint"),
    countAi("concept_help"),
    countAi("grading_feedback"),
    rows.cardEvents.filter((c) => c.event_type === "refresh").length,
    rows.clicks.length,
    device,
    features,
    session.session_id,
    fileName,
  ]);

  return { fileName, timelineCsv, overviewLine, attemptLines };
}

// Bump whenever report columns or README text change: the sync then
// re-uploads the README and re-renders every session's reports.
export const LAYOUT_VERSION = 2;

export function buildReadme(scenarioTexts) {
  const scenarioList = scenarioTexts
    .map((text, i) => `  ${i + 1}. ${describeScenario(text, scenarioTexts).label}\n     "${text}"`)
    .join("\n\n");

  return `PSYCH GAME - SESSION DATA
=========================

This folder updates automatically while people play. Every time a player
changes screens, the files for their session are refreshed within seconds
(and a background check runs every 5 minutes to catch anything missed).
All times are US Eastern. All durations are in minutes unless noted.

WHERE TO START
--------------
Sessions Overview.csv
  One row per play session. Best starting point: who played, which test
  group they were in, when, for how long, how much time they spent on each
  screen, their average AI score, and how many hints / responses / AI
  feedback messages they had. "Session #" increases in the order sessions
  started.

Scenario Responses.csv
  One row per session per scenario. Contains what the player actually wrote,
  the AI grading feedback and score they got back, any hints/concept help
  they were given, which vocab cards they were dealt, and how long they
  spent on the scenario. Use this for coding/grading responses.

Session Timelines/
  One file per session, named "Session #### - date time - username". A
  step-by-step, time-ordered account of everything that happened in that
  session (screens opened/left, clicks, cards, hints, responses, feedback).
  Use this to see HOW a player went through the game.

Raw Data/all_events.csv
  Every logged event in one machine-readable file (one row per event, all
  event types mixed, raw UTC timestamps). Intended for R / Python / SPSS
  scripts, not for reading by eye.

NOTES
-----
- Test group: the study condition the player was assigned by their link
  (?group=N). Group 1 is the original game (control); "Features on" lists
  exactly which experimental features that player saw. Sessions from before
  test groups existed are shown as group 1, which is what they all played.
- Screens, in game order: Main menu, Introduction, Rules, Save select,
  Loading, Responding (the scenario screen), Grading, Review, Game end.
  Screen time is measured from when a screen opens until the player leaves
  it. If a player closes the tab abruptly, their last screen may be missing.
- Early builds only timed Main menu, Responding and Review. For those
  sessions the other screen-time columns are left blank (not measured),
  rather than 0.
- AI score: the 0-100 grade the AI gave the response. "Average AI score"
  averages all graded responses in the session; on Scenario Responses, if a
  scenario was graded more than once, the latest score is shown. Blank means
  no score was recorded (e.g. sessions before scores were logged).
- "Clicked (empty space)" means the player clicked somewhere that wasn't a
  button, card, or other interactive object.
- Multiple responses / feedback messages for the same scenario in one
  session are separated by a "---" line inside the cell.
- Cells that begin with "-", "=", "+" or "@" have a leading apostrophe added
  so Excel shows them as text instead of treating them as formulas.
- Sessions with usernames like "test", "sync-test", etc. were developer
  testing and can be filtered out.

SCENARIOS
---------
${scenarioList}
`;
}
