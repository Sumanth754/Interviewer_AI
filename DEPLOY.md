# Deployment Guide

Everything you need to put the AI Interview Platform on the internet. Pick **one** of the
three options below.

## Architecture refresher

```
Browser (React app)
   │   calls /api/... on  VITE_API_BASE   (CORS must allow the frontend origin)
   ▼
Backend API  (.NET 8, :5000 or :8080)
   ▼
MongoDB (storage) + Redis (cache)   ← optional; InMemory fallback if absent
```

Two things must always be true in any deployment:
1. **API env var** `JWT_KEY` — set a long random key (JWT signing). Never the dev default.
2. **Frontend build** sets `VITE_API_BASE=<api-url>` so the browser calls your API, and the
   API's `Cors__Origins=<frontend-url>` must list the frontend origin.

---

## Option A — Render.com (easiest, free tier) + MongoDB Atlas

### 1. MongoDB Atlas (free)
1. Sign in at https://www.mongodb.com/atlas → **Create** (free M0 cluster, any region).
2. **Database Access** → Add user → remember the username/password.
3. **Network Access** → Allow access `0.0.0.0/0` (open for demo).
4. **Connect** → Drivers → copy the connection string:
   `mongodb+srv://USER:PASS@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority`

### 2. Deploy the API to Render
1. Push this repo to GitHub (see the repo's main README / "Uploading to GitHub").
2. https://dashboard.render.com → **New** → **Web Service** → connect the repo.
3. Settings:
   - **Root directory:** `backend`
   - **Build command:** `dotnet restore AIInterviewPlatform.sln && dotnet publish src/AIInterviewPlatform.Api/AIInterviewPlatform.Api.csproj -c Release -o out`
   - **Start command:** `dotnet out/AIInterviewPlatform.Api.dll`
   - **Instance type:** Free (as of writing); paid starts ~$5/mo.
4. **Environment variables** under *Environment*:
   | Variable | Value |
   |---|---|
   | `ASPNETCORE_URLS` | `http://+:8080` |
   | `JWT_KEY` | your long random key |
   | `Database__Mode` | `Mongo` |
   | `Database__Mongo__ConnectionString` | your Atlas URI |
   | `Database__Mongo__DatabaseName` | `ai_interview_platform` |
   | `Cache__Mode` | `Redis` (or leave out → in-memory cache) |
   | `Redis__ConnectionString` | `redistogo:...` if you add a Redis add-on |
   | `AI__Gemini__ApiKey` | (optional) your Gemini key |
5. **Deploy**. When done, copy the service URL, e.g. `https://aichat-api.onrender.com`. Check `https://aichat-api.onrender.com/api/health`.

### 3. Deploy the frontend to Render static site
1. **New** → **Static Site** → connect the same repo.
2. **Root directory:** `frontend`
   - **Build command:** `npm ci && npm run build`
   - **Publish directory:** `dist`
   - **Environment variable:** `VITE_API_BASE=https://aichat-api.onrender.com`
3. **Deploy.** Copy the site URL, e.g. `https://aichat-frontend.onrender.com`.

### 4. Wire CORS
Back on the API Web Service → **Environment** → set:
```
Cors__Origins=https://aichat-frontend.onrender.com
```
Redeploy the API. Also set the same origin if you used Redis/memory without CORS — this step is **required**.

> Web Speech API (voice mode) requires HTTPS — both Render free URLs are HTTPS already. ✔

---

## Option B — Any VPS / VM with Docker (self-hosted, full control)

You need a machine with Docker + Git, e.g. a Ubuntu droplet:

```bash
git clone https://github.com/YOUR_USER/ai-interview-platform.git
cd ai-interview-platform

export JWT_KEY="$(openssl rand -base64 48)"          # persists only for this shell
export CORS_ORIGINS="https://app.yourdomain.com"

docker compose up -d --build            # mongo, redis, api on :8080
```

`docker-compose.yml` already wires Mongo + Redis + the API. For a real TLS domain, either:

- Put it behind **Caddy** (auto-HTTPS):
  ```caddyfile
  app.yourdomain.com {
      reverse_proxy * aichat_api:8080
  }
  ```
  (run Caddy in the same compose network, add `app` to the footprint).
- Or use Nginx + Let's Encrypt.

Frontend: build locally with `VITE_API_BASE=https://app.yourdomain.com` and serve `frontend/dist`
with any static host, or the same compose stack via Nginx.

---

## Option C — GitHub Pages (frontend only) + Railway (API)

Backend: same idea as Render but https://railway.app — add a `Web Service`, pick the
`backend` root dir, set the same env vars. Railway also offers **native Mongo + Redis plugins**.

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

- [ ] `JWT_KEY` is a **random long** value on the server (keys in `appsettings.json` are dev-only).
  ```powershell
  [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))
  ```
- [ ] `Cors__Origins` matches the exact frontend origin (no trailing slash).
- [ ] `VITE_API_BASE` used at frontend build time (it's baked into `dist/`, so rebuild+redeploy the frontend if it changes).
- [ ] First registered user becomes **Admin** automatically — register your admin account right after deploy.
- [ ] `https://<api>/api/health` reports the expected `database` / `cache` / `aiProvider`.
- [ ] CI badge works: GitHub **Actions** tab runs `dotnet build`/`test` + `npm build` on every push.
- [ ] MongoDB/Redis unreachable → app still *works* on in-memory (handy smoke test, not for prod).

## Updating after deploy

Push to `main` → CI runs. For Render/Railway static + web services, push auto-deploys (enable
auto-deploy in service settings). For VPS:

```bash
cd ai-interview-platform && git pull && docker compose up -d --build
```