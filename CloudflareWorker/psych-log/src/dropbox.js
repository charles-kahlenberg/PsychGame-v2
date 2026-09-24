// Minimal Dropbox API client. Counts calls because Workers cap outbound
// fetches per invocation (50 on the free plan), and a backfill can touch
// many files.

export function isDropboxConfigured(env) {
  return Boolean(env.DROPBOX_REFRESH_TOKEN && env.DROPBOX_APP_KEY && env.DROPBOX_APP_SECRET);
}

export async function createDropboxClient(env) {
  let calls = 1;
  const tokenResp = await fetch("https://api.dropbox.com/oauth2/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "refresh_token",
      refresh_token: env.DROPBOX_REFRESH_TOKEN,
      client_id: env.DROPBOX_APP_KEY,
      client_secret: env.DROPBOX_APP_SECRET,
    }),
  });
  if (!tokenResp.ok) {
    throw new Error(`Dropbox token refresh failed: ${tokenResp.status} ${await tokenResp.text()}`);
  }
  const { access_token: accessToken } = await tokenResp.json();

  // Returns { content: null, rev: null } when the file doesn't exist yet.
  async function download(path) {
    calls++;
    const resp = await fetch("https://content.dropboxapi.com/2/files/download", {
      method: "POST",
      headers: { Authorization: `Bearer ${accessToken}`, "Dropbox-API-Arg": JSON.stringify({ path }) },
    });

    if (resp.status === 409) {
      const err = await resp.json().catch(() => null);
      if (err?.error?.[".tag"] === "path" && err.error.path?.[".tag"] === "not_found") {
        return { content: null, rev: null };
      }
      throw new Error(`Dropbox download failed: ${JSON.stringify(err)}`);
    }
    if (!resp.ok) throw new Error(`Dropbox download failed: ${resp.status} ${await resp.text()}`);

    const resultHeader = resp.headers.get("Dropbox-API-Result");
    return { content: await resp.text(), rev: resultHeader ? JSON.parse(resultHeader).rev : null };
  }

  // mode: "overwrite", or { rev } to only succeed if the file is unchanged
  // since that revision (rev null = file must not exist yet). A lost race
  // throws an error with .conflict = true.
  async function upload(path, content, mode = "overwrite") {
    calls++;
    const apiMode =
      mode === "overwrite" ? { ".tag": "overwrite" } : mode.rev ? { ".tag": "update", update: mode.rev } : { ".tag": "add" };
    const resp = await fetch("https://content.dropboxapi.com/2/files/upload", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/octet-stream",
        "Dropbox-API-Arg": JSON.stringify({ path, mode: apiMode, mute: true }),
      },
      body: content,
    });

    if (!resp.ok) {
      const errText = await resp.text();
      const conflict = resp.status === 409 && errText.includes("conflict");
      throw Object.assign(new Error(`Dropbox upload failed: ${resp.status} ${errText}`), { conflict });
    }
    return resp.json();
  }

  return {
    download,
    upload,
    get calls() {
      return calls;
    },
  };
}
