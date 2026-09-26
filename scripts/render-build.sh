#!/usr/bin/env bash
# Render build command: produces a single deployable service that serves both the
# API and the React app from one origin.
#
#   Root directory : (repository root)
#   Build command  : bash scripts/render-build.sh
#   Start command  : dotnet out/AIInterviewPlatform.Api.dll
#
# VITE_API_BASE is intentionally left unset. With the SPA served from the same
# origin, api.ts falls back to relative "/api" paths, so the browser never needs
# CORS and there is nothing to re-bake when the host name changes.
set -euo pipefail

echo "==> [1/4] Building the React frontend"
npm ci --prefix frontend
npm run build --prefix frontend

echo "==> [2/4] Restoring backend packages"
dotnet restore backend/AIInterviewPlatform.sln

echo "==> [3/4] Publishing the API"
dotnet publish backend/src/AIInterviewPlatform.Api/AIInterviewPlatform.Api.csproj \
  -c Release -o out --no-restore

echo "==> [4/4] Copying the frontend bundle into wwwroot"
mkdir -p out/wwwroot
cp -r frontend/dist/. out/wwwroot/

test -f out/wwwroot/index.html || { echo "ERROR: out/wwwroot/index.html is missing"; exit 1; }

echo "==> Build complete. Starting with: dotnet out/AIInterviewPlatform.Api.dll"
