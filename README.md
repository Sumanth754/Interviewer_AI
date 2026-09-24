# AI Interview Practice & Assessment Platform

An **AI-interview studio** for campus placement prep: adaptive difficulty, live AI-scored answers (text **and** voice), focus-integrity tracking, resume-driven question generation and percentile analytics.

- **Backend:** ASP.NET Core (.NET 8) Web API — C#, JWT auth, MongoDB + Redis with automatic in-memory fallbacks, Swagger.
- **Scoring:** Rubric engine built in; upgrade to **Gemini 2.5 Flash** by setting one env var.
- **Frontend:** React 19 + TypeScript + Vite, dark glass design, SVG skill radar, Web Speech API voice input.
- **Tests:** xUnit (14 tests) for hashing, JWT, rubric scoring, adaptive picker, percentiles, seed data.
- **Deploy:** optional `docker-compose.yml` for Mongo + Redis + API. Full hosting walkthroughs (VPS / Render / Railway + GitHub Pages) in **[DEPLOY.md](DEPLOY.md)**.

---

## Directory layout

```
ai-interview-platform/            # repository root
├── backend\
│   ├── AIInterviewPlatform.sln
│   ├── src\AIInterviewPlatform.Api\     # the Web API
│   └── tests\AIInterviewPlatform.Tests\ # xUnit tests
├── frontend\                            # React + Vite single-page app
├── .github\workflows\ci.yml             # CI (build + test) on push
├── docker-compose.yml                   # optional mongo+redis+api stack
├── LICENSE
└── README.md
```

> All commands below assume your terminal is **in the repository root** (`cd` into the folder once) unless a command says otherwise.

---

## 1. Run the backend (no external dependencies needed)

```powershell
# start the API on http://localhost:5000  (Swagger at /swagger)
cd backend\src\AIInterviewPlatform.Api
dotnet run

# or, if you installed the SDK yourself (user-scope):
& "$env:USERPROFILE\dotnet\dotnet.exe" run
```

On first start it creates two seeded question banks (~33 questions) automatically.
Health check:

```
GET http://localhost:5000/api/health
```

Expected (without MongoDB/Redis running):

```json
{ "status": "Ok", "database": "InMemory", "cache": "Memory", "aiProvider": "Rubric", "version": "1.0.0" }
```

---

## 2. Run the frontend

```powershell
cd frontend
npm install      # first time only
npm run dev
```

Open **http://localhost:5173**. Vite proxies `/api` → `http://localhost:5000`, so no CORS setup is needed.

---

## 3. How the account model works

- Anyone can register.
- **The very first account ever registered becomes the Admin** (that's the bootstrap rule). Register a second account and it's a normal Candidate.
- Admin can create question banks and add questions (`/admin` in the UI).

---

## 4. Try the full flow (UI)

1. Open http://localhost:5173 → **Get started** → register.
   > Your first registration on a fresh server = Admin. Register any later accounts as Candidate users for practice.
2. **Dashboard** → pick the *SDE Campus — Full Stack (.NET/React)* or *AI/ML & Python Practice* bank → choose question count / duration → click **Start session**.
3. Answer:
   - **MCQ** questions → pick an option.
   - **Subjective / Voice** questions → type an answer, or click **● Voice mode** (works in Chrome/Edge) and speak.
   - Hitting the "answers" flow marks focus-loss events (anti-cheat) that affect the final score.
   - Feedback appears under each question instantly (30± s on local rubric; up to ~25 s with Gemini).
4. When done → **See results** → overall ring, percentile, tag breakdown, per-question review. Re-open any past result from the Dashboard (results are persisted by session id).

---

## 5. API smoke-test (PowerShell, no UI)

```powershell
$base = "http://localhost:5000"
$b    = @{ fullName="Ada Lovelace"; email="ada@test.com"; password="pass1234" } | ConvertTo-Json
$me   = Invoke-RestMethod -Method Post -Uri "$base/api/auth/register" -Body $b -ContentType "application/json"
$h    = @{ Authorization = "Bearer $($me.token)" }

# banks
Invoke-RestMethod -Uri "$base/api/banks" -Headers $h | ConvertTo-Json -Depth 4

# start an adaptive session of 5 questions
$start = Invoke-RestMethod -Method Post -Uri "$base/api/sessions/start" -Headers $h `
  -Body (@{ bankId=$resp[0].id; questionCount=5; durationMinutes=10; adaptive=$true } | ConvertTo-Json) `
  -ContentType "application/json"

# answer question 0 (typed)
Invoke-RestMethod -Method Post -Uri "$base/api/sessions/$($start.id)/answers/0" -Headers $h `
  -Body (@{ answer="A hash table maps keys to buckets via a hash function and resolves collisions."; mode="typed"; timeTakenSeconds=45; focusLost=$false } | ConvertTo-Json) `
  -ContentType "application/json"

# finish and get results
Invoke-RestMethod -Method Post -Uri "$base/api/sessions/$($start.id)/finish" -Headers $h -Body "{}" -ContentType "application/json" | ConvertTo-Json -Depth 5

# dashboard + resume coach
Invoke-RestMethod -Uri "$base/api/me/dashboard" -Headers $h | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "$base/api/resume/questions" -Headers $h `
  -Body (@{ resumeText="I know C#, React, MongoDB, Redis, Python, YOLO and built full stack apps" } | ConvertTo-Json) `
  -ContentType "application/json" | ConvertTo-Json -Depth 5
```

---

## 6. Tests

```powershell
cd backend
& "$env:USERPROFILE\dotnet\dotnet.exe" test
# -> Failed: 0, Passed: 14
```

---

## 7. Optional: enable MongoDB + Redis

The app detects and switches automatically (`Database:Mode = Auto`, `Cache:Mode = Auto`). Easiest path — Docker:

```powershell
cd <repo-root>
docker compose up -d --build      # starts mongo, redis, api on port 8080
```

Or set env vars before `dotnet run` when you have local servers:

```powershell
$env:Mongo__ConnectionString="mongodb://localhost:27017"
$env:Redis__ConnectionString="localhost:6379"
$env:Database__Mode="Mongo"
$env:Cache__Mode="Redis"
```

Health then reports `"database": "Mongo", "cache": "Redis"`.

---

## 8. Optional: enable Gemini AI scoring

Get a free key from https://aistudio.google.com/apikey, then:

```powershell
$env:AI__Gemini__ApiKey="<your-key>"
dotnet run
# health -> "aiProvider": "Gemini"; scores come with richer LLM feedback
```

Without a key, the local rubric scorer is used and everything still works.

---

## Engineering notes worth mentioning in an interview

- **Cache-aside** for `/api/banks` (list + per-bank detail) and per-candidate dashboard; dashboard cache is invalidated the moment a session finishes.
- **Adaptive picker:** order questions by your weakest tag (lowest rolling average) and bump *down* difficulty on weak tags so you get wins before hard questions.
- **Scoring pipeline:** rubric always available (deterministic); `CompositeScoreService` adds a Gemini pass when configured; both feed the same `Feedback` DTO so the UI never changes.
- **Anti-cheat:** focus-loss events recorded per question and penalized at session finish (up to −10 points), shown on results.
- **Percentile:** score → normal-CDF percentile estimate vs. the platform mean (μ=56, σ=16.5).
- **First-user bootstrap** grants Admin so the platform is usable end-to-end with zero config; `AddSingleton(gemini)` is registered only when a key is present so DI never resolves `null`.

## User-facing features at a glance

| Area | What it does |
|---|---|
| Adaptive sessions | difficulty/tag targeting based on your history |
| AI scoring | real-time rubric feedback + strengths/improvements |
| Voice mode | Web Speech API, transcript goes through the same scorer |
| Resume Coach | pasted resume → detected skills → matched questions |
| Analytics | skill radar, tag bars, streaks, percentile, recent sessions |
| Admin | create banks & questions (MCQ/subjective/voice) |