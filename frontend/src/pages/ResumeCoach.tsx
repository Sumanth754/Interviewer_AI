import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import type { QuestionDto } from "@/lib/types";
import { api } from "@/lib/api";

const PREVIEWS = [
  "C#, ASP.NET Core, .NET 8, Entity Framework, SQL Server, MongoDB, Redis, REST APIs, Angular, React, TypeScript",
  "Python, FastAPI, Django, pandas, NumPy, scikit-learn, PyTorch, YOLO, OpenCV, machine learning, deep learning",
  "SQL, database design, indexing, transactions, ACID, noSQL, caching, system design, microservices",
];

export default function ResumeCoach() {
  const navigate = useNavigate();
  const [text, setText] = useState("");
  const [skills, setSkills] = useState<string[]>([]);
  const [questions, setQuestions] = useState<QuestionDto[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const generate = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const res = await api.resumeQuestions(text);
      setSkills(res.detectedSkills);
      setQuestions(res.questions);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to generate questions");
    } finally {
      setBusy(false);
    }
  };

  const start = async (q: QuestionDto) => {
    setBusy(true);
    try {
      const banks = await api.banks();
      const bank = [...banks].sort((a, b) => b.questionCount - a.questionCount)[0];
      const s = await api.startSession({ bankId: bank.id, questionCount: 5, durationMinutes: 10, adaptive: true });
      navigate(`/interview/${s.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start practice");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="resume">
      <div className="dash-head">
        <div>
          <h1>Resume Coach</h1>
          <p className="muted">Paste your resume — get questions tailored to the skills you claim.</p>
        </div>
      </div>

      <form className="card resume-form" onSubmit={generate}>
        <textarea
          className="answer-box"
          rows={10}
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Paste your resume text here… (mention technologies, tools and projects)"
        />
        <div className="resume-actions">
          <div className="preset-row">
            <span className="muted small">Try a sample:</span>
            {PREVIEWS.map((p, i) => (
              <button type="button" key={i} className="link small" onClick={() => setText(p)} disabled={busy}>
                Sample {i + 1} ({p.split(",")[0]})
              </button>
            ))}
          </div>
          <button className="btn primary" disabled={busy || text.trim().length < 10}>
            {busy ? "Analyzing skills…" : "Generate questions"}
          </button>
        </div>
        {error && <div className="alert error">{error}</div>}
      </form>

      {skills.length > 0 && (
        <div className="card">
          <h2>Skills detected</h2>
          <div className="skill-cloud">
            {skills.map((s) => (
              <span className="skill-chip" key={s}>
                {s}
              </span>
            ))}
          </div>
        </div>
      )}

      {questions.length > 0 && (
        <div className="card">
          <h2>Recommended practice (matches your strongest signals)</h2>
          <div className="review-list">
            {questions.map((q, i) => (
              <div className="review-item static" key={q.id}>
                <div className="review-header">
                  <span className="tag-chip">{q.tag}</span>
                  <div className="review-q">
                    <b>
                      Q {i + 1} · {q.difficulty}
                    </b>
                    <span className="muted small">{q.text}</span>
                  </div>
                </div>
                <button className="btn ghost small" onClick={() => start(q)}>
                  Practice this tag →
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}