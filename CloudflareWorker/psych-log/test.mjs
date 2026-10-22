// node test.mjs — renders a made-up session's reports, no D1 or Dropbox.
import assert from "node:assert";
import { readFileSync } from "node:fs";
import { OVERVIEW_COLUMNS, buildSessionReport, parseScenarioList, studyColumns } from "./src/reports.js";

const scenarios = parseScenarioList(readFileSync(new URL("../../Assets/Resources/Scenarios.txt", import.meta.url), "utf8"));
const at = (s) => new Date(Date.UTC(2026, 9, 20, 18, 0, s)).toISOString();
const session = { row_id: 1, session_id: "s", username: "u", started_at: at(10), test_group: 2, user_agent: "", features: "GradingTransition" };
const rows = {
  clicks: [],
  cardEvents: [],
  menuDurations: [
    { menu_name: "main_menu", timestamp: at(20), duration_ms: 20000 }, // opened before the session row was written
    { menu_name: "respond", timestamp: at(80), duration_ms: 60000, scenario: scenarios[0] },
    { menu_name: "grading", timestamp: at(85), duration_ms: 5000 },
    { menu_name: "grading", timestamp: at(90), duration_ms: 5000 }, // old builds: really the next synopsis
    { menu_name: "respond", timestamp: at(140), duration_ms: 50000, scenario: scenarios[3] },
    { menu_name: "review", timestamp: at(150), duration_ms: 63e12 }, // editor recompile: counted from the year 1
  ],
  aiResponses: [
    { kind: "hint", timestamp: at(30), scenario: scenarios[0], content: "first" },
    { kind: "hint", timestamp: at(31), scenario: scenarios[0], content: "second" }, // same click, asked twice
    { kind: "hint", timestamp: at(45), scenario: scenarios[0], content: "again" },
    { kind: "grading_feedback", timestamp: at(82), scenario: scenarios[0], content: "Score: 40/100", score: 40 },
  ],
  userResponses: [
    { timestamp: at(79), scenario: scenarios[0], response: "an answer" },
    { timestamp: at(139), scenario: scenarios[3], response: null }, // submitted blank: no grade comes back
  ],
};

const report = buildSessionReport(session, rows, scenarios);
const overview = report.overviewLine.split(",");
const col = (name) => overview[OVERVIEW_COLUMNS.indexOf(name)];
assert.equal(col("Main menu time (min)"), "0.3");   // all 20s, not cut at the session row
assert.equal(col("Review time (min)"), "2.3");      // year-1 duration clamped to the session start
assert.equal(col("Grading time (min)"), "0.1");
assert.equal(col("Introduction time (min)"), "0.1"); // the "grading" visit that ran into responding
assert.equal(col("Hints requested"), "2");          // the double ask counts once
assert.equal(col("Session length (min)"), "2.5");   // from the main menu opening
assert.ok(!report.timelineCsv.includes(",first"));  // the replaced reply is left out
assert.ok(report.timelineCsv.includes(",second"));

const study = report.studyLine.split(",");
const field = (name) => study[studyColumns(scenarios.length).indexOf(name)];
assert.equal(field("S1BrainyHelp"), "2");
assert.equal(field("S1CardHint"), "0");
assert.equal(col("Credits time (min)"), "0");
assert.equal(field("S1Score"), "40");
assert.equal(field("S4Narrative"), "");             // the blank answer leaves empty cells, not an error
assert.equal(field("S4Score"), "");
assert.equal(field("S4StartTime"), "1:30");          // since the main menu opened
assert.equal(field("S2StartTime"), "");             // not played

const blankOnly = { ...rows, userResponses: [rows.userResponses[1]] };
assert.equal(buildSessionReport(session, blankOnly, scenarios).studyLine, null); // nothing written: no Study Data row
console.log("ok");

// Backup round trip: dump a seeded database, restore it into an empty one.
const { DatabaseSync } = await import("node:sqlite");
const { buildDump } = await import("./src/backup.js");
const d1Shim = (db) => ({ prepare: (sql) => ({ all: async () => ({ results: db.prepare(sql).all().map((r) => ({ ...r })) }) }) });
const source = new DatabaseSync(":memory:");
source.exec(readFileSync(new URL("./schema.sql", import.meta.url), "utf8"));
source.exec(`INSERT INTO sessions (session_id, started_at, started_at_et, test_group) VALUES ('s1', 't', 'et', 2);
  INSERT INTO user_responses (session_id, timestamp, timestamp_et, response) VALUES ('s1', 't', 'et', 'it''s "quoted",
multi-line; DROP TABLE x;');
  INSERT INTO menu_durations (session_id, timestamp, timestamp_et, menu_name, duration_ms) VALUES ('s1', 't', 'et', 'review', 1.5);`);
const restored = new DatabaseSync(":memory:");
restored.exec(await buildDump(d1Shim(source)));
for (const t of ["sessions", "user_responses", "menu_durations"]) {
  assert.deepEqual(restored.prepare(`SELECT * FROM ${t}`).all(), source.prepare(`SELECT * FROM ${t}`).all());
}
console.log("backup ok");
