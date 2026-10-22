// Twice-daily full copy of D1 into Dropbox, so the logs survive losing the
// database itself (D1's Time Travel restore goes away with the database).
// Each file is a complete .sql dump; restore into an empty database with:
//   wrangler d1 execute psych_log_db --remote --file="<backup>.sql"
// Files rotate through 14 names (Eastern weekday + AM/PM), so the folder holds
// the last week and Dropbox's own version history keeps older copies of each name.

import { createDropboxClient, folder, isDropboxConfigured } from "./dropbox.js";

// e.g. "Mon AM" for the midnight run going into Monday.
const slotName = (date) =>
  new Intl.DateTimeFormat("en-US", { timeZone: "America/New_York", weekday: "short", hour: "numeric", hour12: true })
    .formatToParts(date)
    .filter((p) => p.type === "weekday" || p.type === "dayPeriod")
    .map((p) => p.value)
    .join(" ");

const sqlValue = (v) => (v === null ? "NULL" : typeof v === "number" ? String(v) : `'${String(v).replace(/'/g, "''")}'`);

export async function buildDump(db) {
  // Tables before indexes; Cloudflare's internal _cf_ tables can't be recreated.
  const { results: objects } = await db
    .prepare(
      `SELECT type, name, sql FROM sqlite_master
       WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '_cf_%'
       ORDER BY type = 'index', rowid`
    )
    .all();

  const lines = ["PRAGMA defer_foreign_keys = TRUE;"];
  for (const { type, name, sql } of objects) {
    lines.push(`${sql};`);
    if (type !== "table") continue;
    // ponytail: one SELECT per table, page by rowid once a table nears D1's result size limit
    const { results: rows } = await db.prepare(`SELECT * FROM "${name}"`).all();
    for (const row of rows) {
      lines.push(`INSERT INTO "${name}" VALUES (${Object.values(row).map(sqlValue).join(",")});`);
    }
  }
  return lines.join("\n") + "\n";
}

export async function backupToDropbox(env) {
  if (!isDropboxConfigured(env)) return;
  const path = `${folder(env)}/Backups/psych_log_db ${slotName(new Date())}.sql`;
  const dump = await buildDump(env.DB);
  const dropbox = await createDropboxClient(env);
  await dropbox.upload(path, dump);
  console.log(`Backed up ${dump.length} bytes to ${path}`);
}
