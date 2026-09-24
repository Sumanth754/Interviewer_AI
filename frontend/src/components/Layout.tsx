import { Link, NavLink, useNavigate } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "@/context/AuthContext";

export default function Layout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate("/");
  };

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `nav-link${isActive ? " active" : ""}`;

  return (
    <div className="app-shell">
      <header className="topbar">
        <Link to="/" className="brand">
          <span className="brand-logo">⌘</span>
          <span>
            <b>AIInterview</b>
            <small>Practice Studio</small>
          </span>
        </Link>
        <nav className="nav-links">
          {!user && (
            <>
              <NavLink to="/login" className={linkClass}>
                Sign in
              </NavLink>
              <NavLink to="/register" className={linkClass}>
                Get started
              </NavLink>
            </>
          )}
          {user && (
            <>
              <NavLink to="/dashboard" className={linkClass}>
                Dashboard
              </NavLink>
              <NavLink to="/resume" className={linkClass}>
                Resume Coach
              </NavLink>
              {user.role === "Admin" && (
                <NavLink to="/admin" className={linkClass}>
                  Admin
                </NavLink>
              )}
            </>
          )}
        </nav>
        {user && (
          <div className="user-chip">
            <span className="avatar">{user.fullName.slice(0, 1).toUpperCase()}</span>
            <span className="user-chip-name">{user.fullName}</span>
            {user.role === "Admin" && <span className="badge-role">Admin</span>}
            <button className="btn ghost small" onClick={handleLogout}>
              Sign out
            </button>
          </div>
        )}
      </header>
      <main className="page">{children}</main>
      <footer className="bottom-bar">
        Built with .NET 8 &middot; React + Vite &middot; AI-scored interview practice
      </footer>
    </div>
  );
}