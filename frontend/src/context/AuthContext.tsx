import { createContext, useContext, useState, type ReactNode } from "react";
import type { AuthResponse } from "@/lib/types";
import { api, auth } from "@/lib/api";

interface AuthContextValue {
  user: AuthResponse | null;
  login: (email: string, password: string) => Promise<AuthResponse>;
  register: (fullName: string, email: string, password: string) => Promise<AuthResponse>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthResponse | null>(auth.user);

  const login = async (email: string, password: string) => {
    const res = await api.login({ email, password });
    auth.set(res);
    setUser(res);
    return res;
  };

  const register = async (fullName: string, email: string, password: string) => {
    const res = await api.register({ fullName, email, password });
    auth.set(res);
    setUser(res);
    return res;
  };

  const logout = () => {
    auth.clear();
    setUser(null);
  };

  return <AuthContext.Provider value={{ user, login, register, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>");
  return ctx;
}