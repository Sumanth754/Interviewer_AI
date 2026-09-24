import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import type { SessionView } from "@/lib/types";
import { api } from "@/lib/api";
import ScoreRing from "@/components/ScoreRing";

export default function Results() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const [session, setSession] = useState<SessionView | null>(null);
  const [error, setError] = useState("");
  const [accordion, setAccordion] = useState<number | null>(0);

  useEffect(() => {
    (async () => {
      try {
        setSession(await api.session(id));
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load results");
      }
    })();
  }, [id]);

  if (error)
    return (
      <div className="card center-card">
        <p className="alert error">{error}</p>
        <button className="btn primary" onClick={() => navigate("/dashboard")}>
          Back to dashboard
        </button>
      </div>
    );
  if (!session) return <div className="skeleton card big-skeleton" />;

  const score = session.overallScore ?? 0;
  const answered = session.questions.filter((q) => q.isAnswered).length;
  const tagMap = new Map<string, { total: number; n: number }>();
  session.questions.forEach((q) => {
    if (q.score == null) return;
    const t = tagMap.get(q.question.tag) ?? { total: 0, n: 0 };
    t.total += q.score;
    t.n += 1;
    tagMap.set(q.question.tag, t);
  });
  const breakdown = [...tagMap.entries()]
    .map(([tag, v]) => ({ tag, avg: Math.round(v.total / v.n) }))
    .sort((a, b) => b.avg - a.avg);

  return (
    <div className="results">
      <div className="result-hero card">
        <ScoreRing score={score} size={180} stroke={16} label="overall score" />
        <div className="result-hero-info">
          <h1>{session.status === "TimedOut" ? "Session timed out" : "Practice complete"}</h1>
          <p className="muted">
            {session.bankName} · {answered}/{session.questions.length} answered ·{" "}
            {new Date(session.startedAt).toLocaleString()}
          </p>
          <div className="result-stats">
            <div className="stat-card inline">
              <span className="stat-label">Estimated percentile</span>
              <span className="stat-value">
                {Math.round(session.percentile)}
                <small>th</small>
              </span>
            </div>
            <div className="stat-card inline">
              <span className="stat-label">Focus-loss events</span>
              <span className="stat-value">{session.focusLossCount}</span>
            </div>
            <div className="stat-card inline">
              <span className="stat-label">Mode</span>
              <span className="stat-value">{session.adaptive ? "Adaptive" : "Balanced"}</span>
            </div>
          </div>
          <p className="summary">{session.summary}</p>
          <div className="cta-row">
            <button className="btn primary" onClick={() => navigate("/dashboard")}>
              Practice again
            </button>
            <button className="btn ghost" onClick={() => navigate("/resume")}>
              Resume coaching
            </button>
          </div>
        </div>
      </div>

      <div className="result-body">
        <div className="card">
          <h2>Tag breakdown</h2>
          <div className="tag-list">
            {breakdown.map((b) => (
              <div className="tag-row" key={b.tag}>
                <span className="tag-chip">{b.tag}</span>
                <div className="tag-bar">
                  <div className="tag-fill" style={{ width: `${b.avg}%`, background: b.avg >= 60 ? "#66bb6a" : b.avg >= 40 ? "#ffb300" : "#ef5350" }} />
                </div>
                <span className="tag-level">{b.avg}%</span>
              </div>
            ))}
          </div>
        </div>

        <div className="card">
          <h2>Question review</h2>
          <div className="review-list">
            {session.questions.map((q) => (
              <div className="review-item" key={q.index}>
                <button className="review-header" onClick={() => setAccordion(accordion === q.index ? null : q.index)}>
                  <span className={`qdot ${q.score != null ? (q.score >= 60 ? "done" : "weak") : ""}`} />
                  <div className="review-q">
                    <b>
                      Q{q.index + 1} · {q.question.tag} ({q.question.difficulty})
                    </b>
                    <span className="muted small">{q.question.text}</span>
                  </div>
                  <span className="review-score">{q.score ?? "—"}</span>
                  <span className="review-caret">{accordion === q.index ? "▾" : "▸"}</span>
                </button>
                {accordion === q.index && (
                  <div className="review-detail">
                    {q.question.typeName === "Mcq" && q.mcqResult && (
                      <p>
                        Your pick: <b>Option {String.fromCharCode(65 + q.mcqResult.selectedIndex)}</b> · Correct:{" "}
                        <b>Option {String.fromCharCode(65 + q.mcqResult.correctIndex)}</b>
                      </p>
                    )}
                    {q.answer && <p className="review-answer">“{q.answer}”</p>}
                    <p>{q.feedback}</p>
                    {q.strengths.length > 0 && <p className="fb-good">Good: {q.strengths.join(" · ")}</p>}
                    {q.improvements.length > 0 && <p className="fb-bad">Improve: {q.improvements.join(" · ")}</p>}
                    {q.focusLost && <span className="badge-warn">Focus lost during this question</span>}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}