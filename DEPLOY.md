# Deployment Guide

Everything you need to put the AI Interview Platform on the internet. The API can
serve the compiled React bundle from `wwwroot`, so the recommended setup is **one
service on one URL** — no separate frontend host and no CORS to configure.

## Architecture refresher

```
Browser
   │  same origin — no CORS, no VITE_API_BASE needed
   ▼
ASP.NET Core 8  (serves /api/*  +  the React bundle from wwwroot)
   ▼
MongoDB (storage) + Redis (cache)   ← optional; InMemory fallback if absent
```

If you would rather split them, Option C still works: build the frontend with
`VITE_API_BASE=<api-url>` and list the frontend origin in `Cors__Origins`.

Two things must always be true in any deployment:
1. **API env var** `JWT_KEY` — set a long random key (JWT signing). Without it the
   app generates a random per-instance key, so every restart logs everyone out.
2. **API env var** `Bootstrap__AdminEmail` — your own address. The first account
   registered becomes Admin, and with in-memory storage that is whoever registers
   first after each deploy. This pins the promotion to you.

---

## Option A — Render.com (easiest, free tier) + MongoDB Atlas

### 1. MongoDB Atlas (free) — optional but recommended
Without it the app still runs, but all users and sessions live in memory and reset
on every deploy.
1. Sign in at https://www.mongodb.com/atlas → **Create** (free M0 cluster, any region).
2. **Database Access** → Add user → remember the username/password.
3. **Network Access** → Allow access `0.0.0.0/0` (open for demo).
4. **Connect** → Drivers → copy the connection string:
   `mongodb+srv://USER:PASS@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority`

### 2. Deploy to Render
1. Push this repo to GitHub.
2. https://dashboard.render.com → **New** → **Web Service** → connect the repo.
3. Settings:
   - **Root directory:** leave blank (repository root)
   - **Build command:** `bash scripts/render-build.sh`
   - **Start command:** `dotnet out/AIInterviewPlatform.Api.dll`
   - **Instance type:** Free (as of writing); paid starts ~$5/mo.
4. **Environment variables** under *Environment*:
   | Variable | Value |
   |---|---|
   | `ASPNETCORE_URLS` | `http://+:8080` |
   | `JWT_KEY` | your long random key (see checklist below) |
   | `Bootstrap__AdminEmail` | your own email, e.g. `you@example.com` |
   | `Database__Mode` | `Mongo` (or `Auto`) |
   | `Database__Mongo__ConnectionString` | your Atlas URI |
   | `Database__Mongo__DatabaseName` | `ai_interview_platform` |
   | `Cache__Mode` | `Redis` (or leave out → in-memory cache) |
   | `Cache__Redis__ConnectionString` | `redistogo:...` if you add a Redis add-on |
   | `AI__Gemini__ApiKey` | (optional) your Gemini key |
   | `Swagger__Enabled` | `false` to hide `/swagger` on a public host |
5. **Deploy**. When done, copy the service URL, e.g. `https://interviewer-ai-ppmz.onrender.com`.

> `scripts/render-build.sh` builds the frontend, publishes the API into `out/`, and
> copies `frontend/dist` into `out/wwwroot` — which is why a single URL serves the app.
> Leave **VITE_API_BASE unset**; with a same-origin bundle the app calls relative
> `/api` paths automatically.

### 3. Verify the deployment

```powershell
.\tests\verify-routes.ps1 -BaseUrl "https://interviewer-ai-ppmz.onrender.com"
```

Checks that `/` serves the app, client routes fall back to `index.html`, the API
answers, and protected endpoints still return 401. Also confirm
`https://<api>/api/health` reports the expected `database` / `cache` / `aiProvider`.

> Web Speech API (voice mode) requires HTTPS — Render URLs are HTTPS already. ✔

---

## Option B — Any VPS / VM with Docker (self-hosted, full control)

You need a machine with Docker + Git, e.g. a Ubuntu droplet:

```bash
git clone https://github.com/YOUR_USER/ai-interview-platform.git
cd ai-interview-platform

export JWT_KEY="$(openssl rand -base64 48)"          # persists only for this shell
export Bootstrap__AdminEmail="you@example.com"      # only you can become Admin

docker compose up -d --build            # mongo, redis, app on :8080
```

`docker-compose.yml` wires Mongo + Redis, builds the React bundle into the API
image, and serves the whole app on `:8080`. For a real TLS domain, either:

- Put it behind **Caddy** (auto-HTTPS):
  ```caddyfile
  app.yourdomain.com {
      reverse_proxy * aichat_api:8080
  }
  ```
  (run Caddy in the same compose network, add `app` to the footprint).
- Or use Nginx + Let's Encrypt.

No `VITE_API_BASE` and no CORS are needed — the app and API share an origin.

---

## Option C — GitHub Pages (frontend only) + Railway (API)

Only needed if you want the two halves on separate hosts. Backend: same idea as
Render but on https://railway.app — add a `Web Service`, pick the `backend` root
dir, set the same env vars. Railway also offers **native Mongo + Redis plugins**.

Frontend on GitHub Pages:
1. Create a `gh-pages` branch or enable **Settings → Pages → Deploy from a branch**.
2. Install `gh-pages` and add to `frontend/package.json`:
   ```json
   "homepage": "https://YOUR_USER.github.io/ai-interview-platform/",
   "scripts": { "deploy": "npm ci && npm run build && npx gh-pages -d dist" }
   ```
3. Build with `VITE_API_BASE=https://ai-api.up.railway.app`, then `npm run deploy`.
4. Keep CORS in sync: `Cors__Origins=https://YOUR_USER.github.io`.

> Note: GitHub Pages changes the base path — set `"base": "/ai-interview-platform/"` in
> `frontend/vite.config.ts` if you keep the frontend in a project repo instead of a
> `YOUR_USER.github.io` repo.

---

## Production checklist

- [ ] `JWT_KEY` is a **random long** value on the server. Without it the app mints a
      random key per instance, so every deploy logs all users out.
  ```powershell
  [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))
  ```
- [ ] `Bootstrap__AdminEmail` is set to your own address, so nobody else can claim
      Admin by registering first.
- [ ] `https://<api>/` loads the **app** (not a 404) and `https://<api>/api/health` answers.
- [ ] Run `.\tests\verify-routes.ps1 -BaseUrl "https://<api>"` — all checks pass.
- [ ] `https://<api>/api/health` reports the expected `database` / `cache` / `aiProvider`.
- [ ] `Cors__Origins` matches the exact frontend origin (no trailing slash) — only
      needed if the frontend is hosted separately.
- [ ] CI badge works: GitHub **Actions** tab runs `dotnet build`/`test` + `npm build` on every push.
- [ ] MongoDB/Redis unreachable → app still *works* on in-memory (handy smoke test, not for prod).

## Updating after deploy

Push to `main` → CI runs. Render/Railway auto-deploy on push (enable auto-deploy in
service settings). For VPS:

```bash
cd ai-interview-platform && git pull && docker compose up -d --build
```