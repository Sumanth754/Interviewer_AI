import { useState, type FormEvent } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "@/context/AuthContext";

export default function Register() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [banner, setBanner] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const res = await register(fullName, email, password);
      if (res.role === "Admin") {
        setBanner("You created the very first account on this platform, so you've been granted the Admin role.");
        navigate("/admin", { replace: true });
      } else {
        navigate("/dashboard", { replace: true });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Registration failed");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="auth-wrap">
      <form className="card auth-card" onSubmit={submit}>
        <h1>Create your account</h1>
        <p className="muted">Free. Takes 20 seconds.</p>
        {banner && <div className="alert info">{banner}</div>}
        {error && <div className="alert error">{error}</div>}
        <label>
          Full name
          <input type="text" value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="Ada Lovelace" required autoFocus />
        </label>
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@example.com" required />
        </label>
        <label>
          Password <span className="hint">(min 6 characters)</span>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" required minLength={6} />
        </label>
        <button className="btn primary block" disabled={busy}>
          {busy ? "Creating account…" : "Create account"}
        </button>
        <p className="auth-alt">
          Already have an account? <Link to="/login">Sign in</Link>
        </p>
      </form>
    </div>
  );
}