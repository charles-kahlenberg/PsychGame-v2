// node test.mjs — runs the worker against a fake OpenAI, no key or network.
import assert from "node:assert";
import worker from "./src/index.js";

let calls = 0, nextStatus = [];
globalThis.fetch = async (url, init) => {
  calls++;
  const status = nextStatus.shift() ?? 200;
  if (status !== 200) return new Response("nope", { status });
  const { messages } = JSON.parse(init.body);
  return Response.json({ choices: [{ message: { content: ` echo:${messages[1].content.length} ` } }] });
};
const post = (path, body) =>
  worker.fetch(new Request(`https://x.dev${path}`, { method: "POST", body: JSON.stringify(body) }), { OPENAI_API_KEY: "k" });
const read = async r => [r.status, await r.json()];

let [s, b] = await read(await post("/intro", { scenario: "", cards: ["a"] }));
assert.equal(s, 200); assert.match(b.intro, /^echo:\d+$/);           // blank scenario falls back, reply trimmed

[s, b] = await read(await post("/hint", { mode: "hint", scenario: "x", cards: [" ", ""] }));
assert.deepEqual([s, b], [400, { error: "cards are required for hint mode" }]);

[s, b] = await read(await post("/hint/", { mode: "concept", scenario: "x", concept: "y" }));
assert.equal(s, 200); assert.ok(b.hint);                               // trailing slash ok

[s, b] = await read(await post("/grade", { scenario: "x", userResponse: "" }));
assert.deepEqual([s, b], [400, { error: "userResponse is required" }]);

calls = 0; nextStatus = [503];
[s, b] = await read(await post("/grade", { scenario: "x", userResponse: "y", usedCards: [] }));
assert.equal(s, 200); assert.ok(b.feedback); assert.equal(calls, 2); // 5xx retried once

calls = 0; nextStatus = [401];
[s, b] = await read(await post("/grade", { scenario: "x", userResponse: "y" }));
assert.equal(s, 502); assert.equal(calls, 1);                          // bad key not retried

assert.equal((await post("/nope", {})).status, 404);
assert.equal((await worker.fetch(new Request("https://x.dev/hint", { method: "OPTIONS" }), {})).headers.get("Access-Control-Allow-Origin"), "*");
console.log("ok");
