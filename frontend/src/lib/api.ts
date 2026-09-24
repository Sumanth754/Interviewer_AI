import type {
  AuthResponse,
  BankSummary,
  DashboardDto,
  QuestionDto,
  ResumeQuestionsResponse,
  SessionQuestionView,
  SessionStartView,
  SessionView,
} from "./types";

const TOKEN_KEY = "aichat_token";
const USER_KEY = "aichat_user";

// API base URL. In dev the Vite proxy forwards /api to http://localhost:5000.
// In production build with VITE_API_BASE=https://your-api-host e.g. https://myapi.onrender.com
const API_BASE = (import.meta.env.VITE_API_BASE as string | undefined) ?? "";

function url(path: string): string {
  if (/^https?:\/\//.test(API_BASE)) return `${API_BASE.replace(/\/$/, "")}${path}`;
  return `${API_BASE}${path}`;
}

export const auth = {
  get token() {
    return localStorage.getItem(TOKEN_KEY);
  },
  get user(): AuthResponse | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as AuthResponse) : null;
  },
  set(value: AuthResponse) {
    localStorage.setItem(TOKEN_KEY, value.token);
    localStorage.setItem(USER_KEY, JSON.stringify(value));
  },
  clear() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  },
};

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = { "Content-Type": "application/json", ...(options.headers as Record<string, string> | undefined) };
  const token = auth.token;
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(url(path), { ...options, headers });
  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    try {
      const body = await res.json();
      if (body?.error || body?.detail) message = body.detail || body.error || message;
    } catch {
      /* ignore non-json */
    }
    throw new Error(message);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  register: (payload: { fullName: string; email: string; password: string }) =>
    request<AuthResponse>("/api/auth/register", { method: "POST", body: JSON.stringify(payload) }),

  login: (payload: { email: string; password: string }) =>
    request<AuthResponse>("/api/auth/login", { method: "POST", body: JSON.stringify(payload) }),

  banks: () => request<BankSummary[]>("/api/banks"),

  startSession: (payload: { bankId: string; questionCount: number; durationMinutes: number; adaptive: boolean }) =>
    request<SessionStartView>("/api/sessions/start", { method: "POST", body: JSON.stringify(payload) }),

  submitAnswer: (
    sessionId: string,
    index: number,
    payload: { answer?: string; selectedOptionIndex?: number; mode: string; timeTakenSeconds: number; focusLost: boolean }
  ) =>
    request<SessionQuestionView>(`/api/sessions/${sessionId}/answers/${index}`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  session: (id: string) => request<SessionView>(`/api/sessions/${id}`),

  finishSession: (sessionId: string, timedOut = false) =>
    request<SessionView>(`/api/sessions/${sessionId}/finish${timedOut ? "?timedOut=true" : ""}`, { method: "POST", body: "{}" }),

  dashboard: () => request<DashboardDto>("/api/me/dashboard"),

  resumeQuestions: (resumeText: string) =>
    request<ResumeQuestionsResponse>("/api/resume/questions", {
      method: "POST",
      body: JSON.stringify({ resumeText }),
    }),

  adminCreateBank: (payload: { name: string; description: string }) =>
    request<BankSummary>("/api/admin/banks", { method: "POST", body: JSON.stringify(payload) }),

  adminAddQuestion: (payload: {
    bankId: string;
    text: string;
    tag: string;
    difficulty: string;
    type: number;
    expectedKeywords: string[];
    modelPoints: string[];
    options: { text: string; code?: string | null }[];
    correctIndex: number;
    timeLimitSeconds: number;
    hint?: string | null;
  }) => request<{ id: string }>("/api/admin/questions", { method: "POST", body: JSON.stringify(payload) }),
};