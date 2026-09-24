import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { BankSummary, DashboardDto } from "@/lib/types";
import { api } from "@/lib/api";
import RadarChart from "@/components/RadarChart";
import ScoreRing from "@/components/ScoreRing";

const TAGS = ["DSA", "OOP", "Logic", "DB", "Web", "AI/ML", "Communication"];

const LEVEL_COLOR: Record<string, string> = {
  Excellent: "#66bb6a",
  Strong: "#9ccc65",
  "Getting there": "#ffb300",
  "Need practice": "#ef5350",
};

const LEVEL_MIN: Record<string, number> = {
  "Need practice": 0,
  "Getting there": 40,
  Strong: 60,
  Excellent: 80,
};

function levelFor(avg: number) {
  if (avg >= 80) return "Excellent";
  if (avg >= 60) return "Strong";
  if (avg >= 40) return "Getting there";
  return "Need practice";
}

export default function Dashboard() {
  const navigate = useNavigate();
  const [dash, setDash] = useState<DashboardDto | null>(null);
  const [banks, setBanks] = useState<BankSummary[]>([]);
  const [error, setError] = useState("");
  const [launching, setLaunching] = useState(false);

  const [bankId, setBankId] = useState("");
  const [count, setCount] = useState(5);
  const [minutes, setMinutes] = useState(10);
  const [adaptive, setAdaptive] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const [d, b] = await Promise.all([api.dashboard(), api.banks()]);
        setDash(d);
        setBanks(b);
        if (b.length) setBankId(b[0].id);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load dashboard");
      }
    })();
  }, []);

  const startSession = async () => {
    if (!bankId) return;
    setLaunching(true);
    setError("");
    try {
      const s = await api.startSession({ bankId, questionCount: count, durationMinutes: minutes, adaptive });
      navigate(`/interview/${s.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start session");
    } finally {
      setLaunching(false);
    }
  };

  const radarStats = [
    ...(dash?.radars ?? []),
    ...TAGS.filter((t) => !(dash?.radars ?? []).some((r) => r.tag === t)).map((t) => ({
      tag: t,
      average: 0,
      attempts: 0,
      level: "Need practice",
    })),
  ].slice(0, 7);

  return (
    <div className="dashboard">
      <div className="dash-head">
        <div>
          <h1>Dashboard</h1>
          <p className="muted">Your practice analytics and quick-start launcher.</p>
        </div>
      </div>

      {error && <div className="alert error">{error}</div>}

      {!dash ? (
        <div className="skeleton card" />
      ) : (
        <>
          <div className="stat-grid">
            <div className="stat-card">
              <span className="stat-label">Sessions completed</span>
              <span className="stat-value">{dash.sessionsCompleted}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Questions answered</span>
              <span className="stat-value">{dash.questionsAnswered}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Overall average</span>
              <span className="stat-value">{Math.round(dash.overallAverage)}%</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Est. percentile</span>
              <span className="stat-value">{Math.round(dash.percentile)}<small>th</small></span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Day streak</span>
              <span className="stat-value">{dash.currentStreakDays} 🔥</span>
            </div>
          </div>

          <div className="dash-grid">
            <div className="card">
              <h2>Skill radar</h2>
              {radarStats.some((r) => r.average > 0) ? (
                <RadarChart stats={radarStats} />
              ) : (
                <p className="muted pad">Complete a session to see your skill radar.</p>
              )}
            </div>

            <div className="card start-card">
              <h2>Start an interview session</h2>
              <label>
                Question bank
                <select value={bankId} onChange={(e) => setBankId(e.target.value)}>
                  {banks.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name} ({b.questionCount} questions)
                    </option>
                  ))}
                </select>
              </label>
              <div className="row-2">
                <label>
                  Questions
                  <select value={count} onChange={(e) => setCount(Number(e.target.value))}>
                    {[3, 5, 8, 10].map((n) => (
                      <option key={n} value={n}>
                        {n}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Duration (min)
                  <select value={minutes} onChange={(e) => setMinutes(Number(e.target.value))}>
                    {[5, 10, 15, 30].map((m) => (
                      <option key={m} value={m}>
                        {m}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <label className="check-row">
                <input type="checkbox" checked={adaptive} onChange={(e) => setAdaptive(e.target.checked)} />
                <span>
                  <b>Adaptive difficulty</b> — targets the tags you're weakest in
                </span>
              </label>
              <button className="btn primary block big" onClick={startSession} disabled={launching}>
                {launching ? "Preparing…" : "Start session"}
              </button>
            </div>
          </div>

          <div className="dash-grid wide">
            <div className="card">
              <h2>Tag-wise strength</h2>
              <div className="tag-list">
                {radarStats.map((r) => {
                  const level = r.average <= 0 ? "Need practice" : r.level || levelFor(r.average);
                  const color = r.average <= 0 ? "#556" : LEVEL_COLOR[level] ?? "#999";
                  return (
                    <div className="tag-row" key={r.tag}>
                      <span className="tag-chip" style={{ background: color + "22", color, borderColor: color + "66" }}>
                        {r.tag}
                      </span>
                      <div className="tag-bar">
                        <div className="tag-fill" style={{ width: `${Math.min(100, r.average)}%`, background: color }} />
                      </div>
                      <span className="tag-level" style={{ color }}>
                        {level}
                      </span>
                      <span className="tag-attempts">{r.attempts} attempts</span>
                    </div>
                  );
                })}
              </div>
            </div>

            <div className="card">
              <h2>Recent sessions</h2>
              {dash.recentSessions.length === 0 ? (
                <p className="muted pad">Nothing yet — your first practice session will appear here.</p>
              ) : (
                <div className="recent-list">
                  {dash.recentSessions.map((s) => (
                    <button key={s.id} className="recent-row" onClick={() => navigate(`/results/${s.id}`)}>
                      <ScoreRing score={s.score} size={56} stroke={7} label="" />
                      <div className="recent-info">
                        <b>{s.bankName}</b>
                        <span>
                          {new Date(s.completedAt).toLocaleString()} · p{s.percentile.toFixed(0)}
                        </span>
                      </div>
                      <span className="recent-arrow">→</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}