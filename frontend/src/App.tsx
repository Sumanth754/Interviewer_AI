import { Routes, Route, Navigate } from "react-router-dom";
import Layout from "@/components/Layout";
import { Protected, AdminOnly } from "@/components/Protected";
import Home from "@/pages/Home";
import Login from "@/pages/Login";
import Register from "@/pages/Register";
import Dashboard from "@/pages/Dashboard";
import Interview from "@/pages/Interview";
import Results from "@/pages/Results";
import ResumeCoach from "@/pages/ResumeCoach";
import Admin from "@/pages/Admin";

export default function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route
          path="/dashboard"
          element={
            <Protected>
              <Dashboard />
            </Protected>
          }
        />
        <Route
          path="/interview/:id"
          element={
            <Protected>
              <Interview />
            </Protected>
          }
        />
        <Route
          path="/results/:id"
          element={
            <Protected>
              <Results />
            </Protected>
          }
        />
        <Route
          path="/resume"
          element={
            <Protected>
              <ResumeCoach />
            </Protected>
          }
        />
        <Route
          path="/admin"
          element={
            <AdminOnly>
              <Admin />
            </AdminOnly>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Layout>
  );
}