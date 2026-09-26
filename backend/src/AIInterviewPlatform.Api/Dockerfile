# Frontend stage (the .NET SDK image has no Node, so build the SPA separately)
FROM node:22-alpine AS frontend
WORKDIR /fe
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/AIInterviewPlatform.sln ./
COPY backend/src/AIInterviewPlatform.Api/AIInterviewPlatform.Api.csproj backend/src/AIInterviewPlatform.Api/
RUN dotnet restore backend/src/AIInterviewPlatform.Api/AIInterviewPlatform.Api.csproj

COPY backend/src/ ./backend/src/
RUN dotnet publish backend/src/AIInterviewPlatform.Api/AIInterviewPlatform.Api.csproj -c Release -o /app/publish --no-restore

# Ship the SPA inside wwwroot so the API serves app + API on one origin/port
COPY --from=frontend /fe/dist /app/publish/wwwroot

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AIInterviewPlatform.Api.dll"]