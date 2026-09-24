# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AIInterviewPlatform.sln ./
COPY src/AIInterviewPlatform.Api/AIInterviewPlatform.Api.csproj src/AIInterviewPlatform.Api/
RUN dotnet restore src/AIInterviewPlatform.Api/AIInterviewPlatform.Api.csproj

COPY src/ ./src/
RUN dotnet publish src/AIInterviewPlatform.Api/AIInterviewPlatform.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AIInterviewPlatform.Api.dll"]