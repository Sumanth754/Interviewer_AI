export type Role = "Candidate" | "Admin";

export interface AuthResponse {
  token: string;
  email: string;
  fullName: string;
  role: Role;
}

export interface BankSummary {
  id: string;
  name: string;
  description: string;
  questionCount: number;
  tags: string[];
}

export interface McqOption {
  text: string;
  code?: string | null;
}

export interface QuestionDto {
  id: string;
  text: string;
  tag: string;
  difficulty: "Easy" | "Medium" | "Hard";
  type: number;
  typeName: "Mcq" | "Subjective" | "Voice";
  options: McqOption[];
  timeLimitSeconds: number;
  hint?: string | null;
}

export interface SessionQuestionView {
  index: number;
  question: QuestionDto;
  mode: string;
  score: number | null;
  answer?: string | null;
  feedback?: string | null;
  strengths: string[];
  improvements: string[];
  timeTakenSeconds: number;
  focusLost: boolean;
  isAnswered: boolean;
  mcqResult?: { selectedIndex: number; correctIndex: number } | null;
}

export interface SessionStartView {
  id: string;
  bankName: string;
  startedAt: string;
  totalSeconds: number;
  adaptive: boolean;
  questions: SessionQuestionView[];
}

export interface SessionView {
  id: string;
  bankName: string;
  startedAt: string;
  completedAt?: string | null;
  totalSeconds: number;
  adaptive: boolean;
  focusLossCount: number;
  overallScore: number | null;
  percentile: number;
  summary?: string | null;
  status: "InProgress" | "Complete" | "TimedOut";
  questions: SessionQuestionView[];
}

export interface SessionResultView {
  id: string;
  bankName: string;
  startedAt: string;
  completedAt?: string | null;
  totalSeconds: number;
  adaptive: boolean;
  focusLossCount: number;
  overallScore: number;
  percentile: number;
  summary?: string | null;
  status: string;
  questions: SessionQuestionView[];
}

export interface TagStat {
  tag: string;
  average: number;
  attempts: number;
  level: string;
}

export interface RecentSessionDto {
  id: string;
  bankName: string;
  completedAt: string;
  score: number;
  percentile: number;
}

export interface DashboardDto {
  sessionsCompleted: number;
  questionsAnswered: number;
  overallAverage: number;
  percentile: number;
  currentStreakDays: number;
  radars: TagStat[];
  recentSessions: RecentSessionDto[];
}

export interface ResumeQuestionsResponse {
  detectedSkills: string[];
  questions: QuestionDto[];
  count: number;
}

export const TAG_COLORS: Record<string, string> = {
  DSA: "#7c5cff",
  OOP: "#00b8d4",
  Logic: "#ffb300",
  DB: "#26a69a",
  Web: "#ef5350",
  "AI/ML": "#ab47bc",
  Communication: "#66bb6a",
};