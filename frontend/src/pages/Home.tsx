import { Link } from "react-router-dom";
import { useAuth } from "@/context/AuthContext";

const FEATURES = [
  {
    icon: "⇅",
    title: "Adaptive difficulty",
    body: "The engine watches which tags you're weakest in and hands you harder or easier questions in real time, like a real interviewer.",
  },
  {
    icon: "◉",
    title: "AI auto-graded answers",
    body: "Free-text answers are scored against a rubric — and upgraded to Gemini 2.5 Flash analysis when an API key is set.",
  },
  {
    icon: "◌",
    title: "Voice-mode answers",
    body: "Practise out loud with voice-to-text answers, exactly like a live interview — then read the scoring the AI gave you.",
  },
  {
    icon: "⌁",
    title: "Focus-integrity & anti-cheat",
    body: "Tab-switches are tracked and fed into your final score, keeping every practice session honest.",
  },
  {
    icon: "›",
    title: "Resume → personalized questions",
    body: "Paste your resume and get questions generated from the exact skills you claim.",
  },
  {
    icon: "⌰",
    title: "Analytics & percentile map",
    body: "Tag-wise radar, streaks, and your standing among candidates — so you know what to grind next.",
  },
];

export default function Home() {
  const { user } = useAuth();
  const cta = user ? (
    <Link to="/dashboard" className="btn primary big">
      Go to my dashboard →
    </Link>
  ) : (
    <div className="cta-row">
      <Link to="/register" className="btn primary big">
        Start free practice
      </Link>
      <Link to="/login" className="btn ghost big">
        I have an account
      </Link>
    </div>
  );

  return (
    <div className="home">
      <section className="hero">
        <div className="hero-card">
          <span className="chip">AI Interview Practice Platform</span>
          <h1>
            Walk in <span className="grad">interview-ready.</span>
          </h1>
          <p className="hero-sub">
            The only practice studio with adaptive difficulty, live AI scoring, voice answers and anti-cheat integrity —
            built to mirror a real campus interview loop.
          </p>
          {cta}
          <div className="hero-meta">
            <span>.NET 8 + MongoDB/Redis</span>
            <span>Gemini-ready scoring</span>
            <span>Free &amp; self-hosted</span>
          </div>
        </div>
      </section>

      <section className="features">
        <div className="section-head">
          <h2>Built for the way interviews actually work</h2>
          <p>Every feature maps to something a real interviewer evaluates.</p>
        </div>
        <div className="feature-grid">
          {FEATURES.map((f) => (
            <div className="feature-card" key={f.title}>
              <div className="feature-icon">{f.icon}</div>
              <h3>{f.title}</h3>
              <p>{f.body}</p>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}