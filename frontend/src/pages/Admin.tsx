import { useEffect, useState, type FormEvent } from "react";
import type { BankSummary } from "@/lib/types";
import { api } from "@/lib/api";

const TAGS = ["DSA", "OOP", "Logic", "DB", "Web", "AI/ML", "Communication"];

export default function Admin() {
  const [banks, setBanks] = useState<BankSummary[]>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState("");

  // create-bank form
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  // add-question form
  const [bankId, setBankId] = useState("");
  const [text, setText] = useState("");
  const [tag, setTag] = useState("Logic");
  const [difficulty, setDifficulty] = useState("Medium");
  const [type, setType] = useState(1);
  const [keywords, setKeywords] = useState("");
  const [points, setPoints] = useState("");
  const [options, setOptions] = useState<string[]>([]);
  const [correctIndex, setCorrectIndex] = useState(0);
  const [hint, setHint] = useState("");

  const refresh = async () => {
    try {
      setBanks(await api.banks());
      setError("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load banks");
    }
  };

  useEffect(() => {
    refresh();
  }, []);

  const createBank = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const b = await api.adminCreateBank({ name, description });
      setNotice(`Bank “${b.name}” created (id: ${b.id}). Now add questions below.`);
      setName("");
      setDescription("");
      setBankId(b.id);
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create bank");
    } finally {
      setBusy(false);
    }
  };

  const addQuestion = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const isMcq = type === 0;
      await api.adminAddQuestion({
        bankId,
        text,
        tag,
        difficulty,
        type,
        expectedKeywords: keywords.split(",").map((s) => s.trim()).filter(Boolean),
        modelPoints: points.split(",").map((s) => s.trim()).filter(Boolean),
        options: isMcq ? options.map((o, i) => ({ text: o })) : [],
        correctIndex: isMcq ? correctIndex : -1,
        timeLimitSeconds: 180,
        hint: hint || null,
      });
      setNotice("Question added. The list below will refresh.");
      setText("");
      setKeywords("");
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add question");
    } finally {
      setBusy(false);
    }
  };

  const addOption = () => setOptions((o) => [...o, ""]);

  return (
    <div className="admin">
      <div className="dash-head">
        <div>
          <h1>Admin Studio</h1>
          <p className="muted">Manage question banks and questions.</p>
        </div>
      </div>

      {notice && <div className="alert info">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <div className="admin-grid">
        <form className="card" onSubmit={createBank}>
          <h2>Create question bank</h2>
          <label>
            Bank name
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="SDE — System Design Practice" required />
          </label>
          <label>
            Description
            <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Short description shown to candidates" />
          </label>
          <button className="btn primary block" disabled={busy}>
            Create bank
          </button>
        </form>

        <form className="card" onSubmit={addQuestion}>
          <h2>Add question</h2>
          <label>
            Bank
            <select value={bankId} onChange={(e) => setBankId(e.target.value)}>
              <option value="">Select a bank…</option>
              {banks.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Question text
            <textarea
              className="answer-box small"
              rows={3}
              value={text}
              onChange={(e) => setText(e.target.value)}
              placeholder="What does the question ask?"
              required
            />
          </label>
          <div className="row-3">
            <label>
              Tag
              <select value={tag} onChange={(e) => setTag(e.target.value)}>
                {TAGS.map((t) => (
                  <option key={t}>{t}</option>
                ))}
              </select>
            </label>
            <label>
              Difficulty
              <select value={difficulty} onChange={(e) => setDifficulty(e.target.value)}>
                <option>Easy</option>
                <option>Medium</option>
                <option>Hard</option>
              </select>
            </label>
            <label>
              Type
              <select value={type} onChange={(e) => setType(Number(e.target.value))}>
                <option value={1}>Subjective</option>
                <option value={0}>MCQ</option>
                <option value={2}>Voice</option>
              </select>
            </label>
          </div>

          {type === 0 ? (
            <>
              <label>Options (mark the correct one via the checkbox)</label>
              {options.map((o, i) => (
                <div className="option-row" key={i}>
                  <input
                    type="radio"
                    name="correct"
                    checked={correctIndex === i}
                    onChange={() => setCorrectIndex(i)}
                    title="Mark correct"
                  />
                  <input value={o} onChange={(e) => setOptions((arr) => arr.map((v, j) => (j === i ? e.target.value : v)))} placeholder={`Option ${i + 1}`} />
                  <button type="button" className="link small" onClick={() => setOptions((arr) => arr.filter((_, j) => j !== i))}>
                    remove
                  </button>
                </div>
              ))}
              <button type="button" className="btn ghost small" onClick={addOption}>
                + Add option
              </button>
            </>
          ) : (
            <>
              <label>
                Expected keywords <span className="hint">(comma separated — used by the AI rubric)</span>
                <input value={keywords} onChange={(e) => setKeywords(e.target.value)} placeholder="hash function, collision, bucket" />
              </label>
              <label>
                Model points <span className="hint">(comma separated)</span>
                <input value={points} onChange={(e) => setPoints(e.target.value)} placeholder="uses a hash function, resolves collisions" />
              </label>
            </>
          )}

          <label>
            Hint <span className="hint">(optional)</span>
            <input value={hint} onChange={(e) => setHint(e.target.value)} placeholder="A nudge candidates can reveal" />
          </label>

          <button className="btn primary block" disabled={busy || !bankId}>
            Add question
          </button>
        </form>
      </div>

      <div className="card">
        <h2>All banks ({banks.length})</h2>
        <div className="bank-admin-list">
          {banks.map((b) => (
            <div className="bank-admin-row" key={b.id}>
              <div>
                <b>{b.name}</b>
                <span className="muted small">
                  {b.questionCount} questions · {b.tags.join(", ") || "no tags yet"}
                </span>
              </div>
              <button
                className="btn ghost small"
                onClick={() => setBankId(b.id)}
                disabled={bankId === b.id}
              >
                {bankId === b.id ? "Selected ✓" : "Add questions"}
              </button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}