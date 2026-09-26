import { useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import type { SessionQuestionView, SessionStartView } from "@/lib/types";
import { api } from "@/lib/api";

type RecognitionLike = {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  onresult: ((e: { resultIndex: number; results: ArrayLike<{ isFinal: boolean; 0: { transcript: string } }> }) => void) | null;
  onend: (() => void) | null;
  onerror: (() => void) | null;
  start: () => void;
  stop: () => void;
};

function speechRecognition(): RecognitionLike | null {
  const w = window as unknown as { SpeechRecognition?: new () => RecognitionLike; webkitSpeechRecognition?: new () => RecognitionLike };
  const Ctor = w.SpeechRecognition ?? w.webkitSpeechRecognition;
  return Ctor ? new Ctor() : null;
}

const fmt = (s: number) => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, "0")}`;

export default function Interview() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const [session, setSession] = useState<SessionStartView | null>(null);
  const [index, setIndex] = useState(0);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const [answer, setAnswer] = useState("");
  const [selected, setSelected] = useState<number | null>(null);
  const [timeTaken, setTimeTaken] = useState(0);
  const [focusLossPending, setFocusLossPending] = useState(false);
  const [remaining, setRemaining] = useState(0);
  const [transcript, setTranscript] = useState("");
  const [listening, setListening] = useState(false);
  const [voiceAnswered, setVoiceAnswered] = useState(false);
  const [showHint, setShowHint] = useState(false);
  const [showFeedback, setShowFeedback] = useState(false);

  const lastAnswer = useRef<Record<string, string>>({});
  const lastOption = useRef<Record<number, number>>({});
  // Which questions were answered out loud. The rubric only awards the spoken
  // communication credit when the submitted mode is "voice", so the microphone
  // state has to travel with the answer instead of being inferred on submit.
  const lastMode = useRef<Record<string, string>>({});
  const lastTime = useRef<Record<string, number>>({});
  const recRef = useRef<RecognitionLike | null>(null);
  const blurRef = useRef(false);

  // Load session
  useEffect(() => {
    (async () => {
      try {
        const s = await api.session(id) as unknown as SessionStartView;
        setSession(s);
        setRemaining(s.totalSeconds);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load session");
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // Session countdown
  useEffect(() => {
    if (session === null || remaining === null) return;
    if (remaining <= 0) {
      complete(true);
      return;
    }
    const t = setTimeout(() => setRemaining((r) => (r === null ? r : r - 1)), 1000);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remaining, session]);

  // Focus-loss tracking
  useEffect(() => {
    const onBlur = () => {
      blurRef.current = true;
      setFocusLossPending(true);
    };
    const onVis = () => {
      if (document.hidden) {
        blurRef.current = true;
        setFocusLossPending(true);
      }
    };
    window.addEventListener("blur", onBlur);
    document.addEventListener("visibilitychange", onVis);
    return () => {
      window.removeEventListener("blur", onBlur);
      document.removeEventListener("visibilitychange", onVis);
    };
  }, []);

  // Restore answer when navigating back
  useEffect(() => {
    if (!session) return;
    const q = session.questions[index];
    if (!q) return;
    setAnswer(lastAnswer.current[q.question.id] ?? "");
    setSelected(lastOption.current[index] ?? null);
    setShowFeedback(q.isAnswered);
    setShowHint(false);
    setTranscript("");
    setVoiceAnswered(lastMode.current[q.question.id] === "voice");
  }, [session, index]);

  const q = session?.questions[index];
  const answered = q?.isAnswered ?? false;

  const consumeFocusForSubmit = () => {
    const flag = focusLossPending || blurRef.current;
    blurRef.current = false;
    setFocusLossPending(false);
    return flag;
  };

  const submit = async (finalFlag: boolean | undefined = undefined) => {
    if (!session || !q || busy) return;
    // Release the mic before scoring, otherwise a late transcript result
    // overwrites the answer that was just submitted.
    stopListening();
    const questionId = q.question.id;
    const isMcq = q.question.typeName === "Mcq";
    setBusy(true);
    setError("");
    try {
      const payload = {
        answer: isMcq ? undefined : answer.trim() || undefined,
        selectedOptionIndex: isMcq ? (selected ?? undefined) : undefined,
        // Previously hardcoded to "typed", which made the rubric's spoken
        // communication credit unreachable no matter what the user did.
        mode: !isMcq && lastMode.current[questionId] === "voice" ? "voice" : "typed",
        timeTakenSeconds: lastTime.current[questionId] ?? Math.max(1, timeTaken),
        focusLost: finalFlag ?? consumeFocusForSubmit(),
      };
      await api.submitAnswer(session.id, q.index, payload);
      const fresh = (await api.session(session.id)) as unknown as SessionStartView;
      setSession(fresh);
      setShowFeedback(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not submit answer");
    } finally {
      setBusy(false);
    }
  };

  const complete = async (timedOut: boolean) => {
    try {
      const result = await api.finishSession(id, timedOut);
      navigate(`/results/${id}`, { state: { result } });
    } catch {
      navigate(`/results/${id}`);
    }
  };

  if (error && !session) {
    return (
      <div className="card center-card">
        <p className="alert error">{error}</p>
        <button className="btn primary" onClick={() => navigate("/dashboard")}>
          Back to dashboard
        </button>
      </div>
    );
  }
  if (!session || !q) return <div className="skeleton card big-skeleton" />;

  const progressPos = index + (q.isAnswered ? 1 : 0);

  const startListening = () => {
    const rec = speechRecognition();
    if (!rec) {
      setError("Voice input isn't supported in this browser — use Chrome/Edge, or type instead.");
      return;
    }
    rec.lang = "en-US";
    rec.continuous = true;
    rec.interimResults = true;
    rec.onresult = (e) => {
      let text = "";
      for (let i = e.resultIndex; i < e.results.length; i++) text += e.results[i][0].transcript;
      setTranscript(text);
      setAnswer(text);
    };
    rec.onend = () => setListening(false);
    rec.onerror = () => setListening(false);
    recRef.current = rec;
    rec.start();
    setListening(true);
    // Mark this question as spoken so submit() reports mode "voice".
    lastMode.current[q.question.id] = "voice";
    setVoiceAnswered(true);
  };

  const stopListening = () => {
    recRef.current?.stop();
    setListening(false);
  };

  const next = () => {
    if (index + 1 < session.questions.length) setIndex(index + 1);
    else complete(false);
  };

  return (
    <div className="interview">
      <div className="interview-top">
        <button className="btn ghost small" onClick={() => navigate("/dashboard")}>
          ← Exit
        </button>
        <div className="interview-title">
          <b>{session.bankName}</b>
          <span>
            {answered ? "Answered" : "Answering"} · Q{index + 1} of {session.questions.length}
          </span>
        </div>
        <div className={`timer ${remaining <= 60 ? "danger" : ""}`}>{fmt(remaining)}</div>
      </div>

      <div className="progress">
        <div className="progress-fill" style={{ width: `${(progressPos / session.questions.length) * 100}%` }} />
      </div>

      <div className="question-card card">
        <div className="q-meta">
          <span className={`tag-chip ${q.question.tag.toLowerCase()}`}>{q.question.tag}</span>
          <span className={`diff diff-${q.question.difficulty.toLowerCase()}`}>{q.question.difficulty}</span>
          <span className="q-type">{q.question.typeName}</span>
          {focusLossPending && <span className="badge-warn">Focus loss detected</span>}
        </div>
        <h2 className="q-text">{q.question.text}</h2>

        {q.question.typeName === "Mcq" ? (
          <div className="mcq-list">
            {q.question.options.map((o, i) => {
              let cls = "mcq-option";
              if (answered && q.mcqResult) {
                if (i === q.mcqResult.correctIndex) cls += " correct";
                else if (i === q.mcqResult.selectedIndex) cls += " wrong";
              } else if (selected === i) cls += " selected";
              return (
                <button key={i} className={cls} disabled={answered} onClick={() => setSelected(i)}>
                  <span className="mcq-letter">{String.fromCharCode(65 + i)}</span>
                  <span className="mcq-text">{o.code ? <code>{o.code}</code> : o.text}</span>
                </button>
              );
            })}
          </div>
        ) : (
          <div className="text-answer">
            <div className="voice-bar">
              <button className={`btn ${listening ? "primary" : "ghost"}`} onClick={listening ? stopListening : startListening}>
                {listening ? "■ Stop recording" : "● Voice mode"}
              </button>
              <span className="muted small">
                {listening
                  ? "Speaking… transcript updates live."
                  : voiceAnswered
                    ? "Recorded by voice — earns the spoken-communication credit on submit."
                    : "Speak your answer, or type below."}
              </span>
            </div>
            {listening && <div className="live-wave"><span /><span /><span /><span /><span /></div>}
            <textarea
              className="answer-box"
              rows={8}
              value={answer}
              disabled={answered}
              onChange={(e) => {
                setAnswer(e.target.value);
                lastAnswer.current[q.question.id] = e.target.value;
              }}
              placeholder="Type (or speak) your answer as if you were in a live interview…"
            />
            {q.question.hint && (
              <div className="hint-bar">
                <button className="link small" onClick={() => setShowHint((v) => !v)}>
                  {showHint ? "Hide hint" : "Show hint"}
                </button>
                {showHint && <p>{q.question.hint}</p>}
              </div>
            )}
          </div>
        )}

        <div className="q-actions">
          {!answered ? (
            <>
              <button
                className="btn primary"
                disabled={busy || (q.question.typeName === "Mcq" ? selected === null : answer.trim().length === 0)}
                onClick={() => submit()}
              >
                {busy ? "Scoring…" : "Submit answer"}
              </button>
              <span className="muted small">Scored live by the AI engine.</span>
            </>
          ) : (
            <div className="feedback">
              <div className="feedback-score">
                <strong>{q.score}</strong>/100
              </div>
              <div className="feedback-body">
                <p>{q.feedback || "No feedback."}</p>
                {q.strengths.length > 0 && (
                  <p className="fb-good">Good: {q.strengths.slice(0, 3).join(" · ")}</p>
                )}
                {q.improvements.length > 0 && (
                  <p className="fb-bad">Improve: {q.improvements.slice(0, 3).join(" · ")}</p>
                )}
                {q.focusLost && <p className="badge-warn">This answer was marked for a focus-loss event.</p>}
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="interview-footer">
        <div className="switcher">
          {session.questions.map((sq) => {
            const cls = "qdot" + (sq.isAnswered ? " done" : sq.index === index ? " current" : "");
            return (
              <button
                key={sq.index}
                className={cls}
                onClick={() => {
                  lastTime.current[q.question.id] = Math.max(1, timeTaken);
                  stopListening();
                  setIndex(sq.index);
                }}
                aria-label={`Question ${sq.index + 1}`}
              />
            );
          })}
        </div>
        {answered && !busy && (
          <button className="btn primary big" onClick={next}>
            {index + 1 < session.questions.length ? "Next question →" : "See results →"}
          </button>
        )}
      </div>
    </div>
  );
}