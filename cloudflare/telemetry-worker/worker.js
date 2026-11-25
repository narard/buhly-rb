const GITHUB_LATEST_URL = "https://api.github.com/repos/narard/buhly-rb/releases/latest";
const CACHE_KEY_RELEASE = "cache:latest_release_v1";
const CACHE_TTL_SEC = 600;

function corsHeaders() {
  return {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type, X-Stats-Token",
    "Cache-Control": "no-store"
  };
}

function jsonResponse(payload, status = 200) {
  return new Response(JSON.stringify(payload, null, 2), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      ...corsHeaders()
    }
  });
}

function getMinskDateKey(daysAgo = 0) {
  const minskMs = Date.now() + 3 * 3600 * 1000 - daysAgo * 86400 * 1000;
  return new Date(minskMs).toISOString().slice(0, 10);
}

async function incrementCounter(kv, key, ttlSeconds) {
  if (!kv) return;
  try {
    const raw = await kv.get(key);
    const current = raw ? parseInt(raw, 10) || 0 : 0;
    const opts = ttlSeconds ? { expirationTtl: ttlSeconds } : undefined;
    await kv.put(key, String(current + 1), opts);
  } catch (_) {
    // Ignore KV transient or quota errors so desktop client never breaks
  }
}

async function getLatestRelease(kv) {
  if (kv) {
    try {
      const cached = await kv.get(CACHE_KEY_RELEASE, { type: "json" });
      if (cached && cached.tag_name && cached.html_url) {
        return cached;
      }
    } catch (_) {}
  }

  try {
    const resp = await fetch(GITHUB_LATEST_URL, {
      headers: {
        "Accept": "application/vnd.github+json",
        "User-Agent": "Buhly-Cloudflare-Worker"
      }
    });
    if (resp.ok) {
      const data = await resp.json();
      const slim = {
        tag_name: data.tag_name || "v3.2.0",
        html_url: data.html_url || "https://github.com/narard/buhly-rb/releases/latest",
        published_at: data.published_at || ""
      };
      if (kv) {
        try {
          await kv.put(CACHE_KEY_RELEASE, JSON.stringify(slim), { expirationTtl: CACHE_TTL_SEC });
        } catch (_) {}
      }
      return slim;
    }
  } catch (_) {}

  return {
    tag_name: "v3.2.0",
    html_url: "https://github.com/narard/buhly-rb/releases/latest",
    published_at: ""
  };
}

async function readInt(kv, key) {
  if (!kv) return 0;
  try {
    const raw = await kv.get(key);
    return raw ? parseInt(raw, 10) || 0 : 0;
  } catch (_) {
    return 0;
  }
}

async function sumDailyCounters(kv, prefix, excludeKey, knownMap) {
  if (!kv) return 0;
  try {
    const missingKeys = [];
    let total = 0;
    let cursor = undefined;
    let listComplete = false;
    while (!listComplete) {
      const opts = cursor ? { prefix, cursor } : { prefix };
      const page = await kv.list(opts);
      if (page && Array.isArray(page.keys)) {
        for (const entry of page.keys) {
          const name = entry && entry.name;
          if (!name || name === excludeKey) continue;
          if (knownMap && Object.prototype.hasOwnProperty.call(knownMap, name)) {
            total += knownMap[name];
          } else {
            missingKeys.push(name);
          }
        }
      }
      listComplete = !page || page.list_complete !== false || !page.cursor;
      cursor = page && page.cursor ? page.cursor : undefined;
    }

    const chunkSize = 25;
    for (let i = 0; i < missingKeys.length; i += chunkSize) {
      const chunk = missingKeys.slice(i, i + chunkSize);
      const vals = await Promise.all(chunk.map((k) => readInt(kv, k)));
      for (const v of vals) {
        total += v;
      }
    }
    return total;
  } catch (_) {
    return 0;
  }
}

function timingSafeEqual(a, b) {
  const enc = new TextEncoder();
  const bufA = enc.encode(String(a || ""));
  const bufB = enc.encode(String(b || ""));
  const len = Math.max(bufA.length, bufB.length);
  let diff = bufA.length ^ bufB.length;
  for (let i = 0; i < len; i++) {
    const byteA = i < bufA.length ? bufA[i] : 0;
    const byteB = i < bufB.length ? bufB[i] : 0;
    diff |= byteA ^ byteB;
  }
  return diff === 0 && bufA.length > 0;
}

export default {
  async fetch(request, env, ctx) {
    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: corsHeaders() });
    }

    const url = new URL(request.url);
    const path = url.pathname.replace(/\/+$/, "") || "/";
    const kv = env && env.STATS_KV ? env.STATS_KV : null;
    const today = getMinskDateKey(0);
    const isGet = request.method === "GET";
    const isPingPost = request.method === "POST" && url.searchParams.get("ping") === "1";
    const userAgent = request.headers.get("User-Agent") || "";

    // 1. Daily version check + anonymous desktop launch counter (0% PII; filters out "/" & scanner bots)
    if (path === "/api/version" || path === "/version" || path === "/") {
      const oneYear = 365 * 86400;
      const isDesktopClient = path === "/api/version" && userAgent.indexOf("Buhly-Desktop") === 0;
      if (isGet && isDesktopClient && kv && ctx && typeof ctx.waitUntil === "function") {
        ctx.waitUntil(incrementCounter(kv, "opens:" + today, oneYear));
      }
      const rel = await getLatestRelease(kv);
      return jsonResponse({
        tag_name: rel.tag_name,
        html_url: rel.html_url,
        published_at: rel.published_at,
        source: "buhly-worker"
      });
    }

    // 2. Counted download tracking (?ping=1 returns 204 No Content with CORS) or 302 redirect (1 KV write/req)
    if (path === "/api/download/zip" || path === "/api/download/exe") {
      const oneYear = 365 * 86400;
      if ((isGet || isPingPost) && kv && ctx && typeof ctx.waitUntil === "function") {
        ctx.waitUntil(incrementCounter(kv, "dl:" + today, oneYear));
      }
      if (url.searchParams.get("ping") === "1") {
        return new Response(null, { status: 204, headers: corsHeaders() });
      }
      const file = path.endsWith("/exe") ? "Buhly.exe" : "Buhly.zip";
      return Response.redirect(
        "https://github.com/narard/buhly-rb/releases/latest/download/" + file,
        302
      );
    }

    // 3. Aggregated statistics for local Buhly_Dashboard.html
    if (path === "/api/stats" || path === "/stats") {
      if (env && env.STATS_TOKEN) {
        const provided = request.headers.get("X-Stats-Token") || "";
        if (!timingSafeEqual(provided, env.STATS_TOKEN)) {
          return jsonResponse({ ok: false, error: "Unauthorized" }, 401);
        }
      }

      const days = [];
      for (let i = 29; i >= 0; i--) {
        days.push(getMinskDateKey(i));
      }

      const openPromises = days.map((d) => readInt(kv, "opens:" + d));
      const dlPromises = days.map((d) => readInt(kv, "dl:" + d));
      const [opensArr, dlArr, legacyOpensTotal, legacyDlTotal] = await Promise.all([
        Promise.all(openPromises),
        Promise.all(dlPromises),
        readInt(kv, "opens:total"),
        readInt(kv, "dl:total")
      ]);

      const knownOpens = {};
      const knownDl = {};
      for (let i = 0; i < days.length; i++) {
        knownOpens["opens:" + days[i]] = opensArr[i];
        knownDl["dl:" + days[i]] = dlArr[i];
      }

      const [sumAllDailyOpens, sumAllDailyDl] = await Promise.all([
        sumDailyCounters(kv, "opens:", "opens:total", knownOpens),
        sumDailyCounters(kv, "dl:", "dl:total", knownDl)
      ]);

      const daily = days.map((d, idx) => ({
        date: d,
        opens: opensArr[idx],
        site_downloads: dlArr[idx]
      }));

      const opensToday = opensArr[29] || 0;
      const opensYesterday = opensArr[28] || 0;
      const opens7d = opensArr.slice(23).reduce((a, b) => a + b, 0);
      const opens30d = opensArr.reduce((a, b) => a + b, 0);
      const dlToday = dlArr[29] || 0;
      const dl30d = dlArr.reduce((a, b) => a + b, 0);
      const opensTotal = Math.max(legacyOpensTotal, sumAllDailyOpens, opens30d);
      const dlTotal = Math.max(legacyDlTotal, sumAllDailyDl, dl30d);

      return jsonResponse({
        ok: true,
        kv_configured: !!kv,
        timezone: "UTC+3",
        today,
        opens_today: opensToday,
        opens_yesterday: opensYesterday,
        opens_7d: opens7d,
        opens_30d: opens30d,
        opens_total: opensTotal,
        site_downloads_today: dlToday,
        site_downloads_total: dlTotal,
        daily
      });
    }

    return jsonResponse({ ok: false, error: "Not found" }, 404);
  }
};
