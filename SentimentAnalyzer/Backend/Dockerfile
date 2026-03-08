# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy .csproj files first for layer caching
COPY Backend/SentimentAnalyzer.API.csproj Backend/
COPY Agents/SentimentAnalyzer.Agents.csproj Agents/
COPY Domain/SentimentAnalyzer.Domain.csproj Domain/

# Restore dependencies
RUN dotnet restore Backend/SentimentAnalyzer.API.csproj

# Copy everything else (including .claude/agents/*.md referenced by .csproj Content items)
COPY Backend/ Backend/
COPY Agents/ Agents/
COPY Domain/ Domain/
COPY .claude/ .claude/

# Publish release build (agent .md files are copied to output by .csproj Content Include)
RUN dotnet publish Backend/SentimentAnalyzer.API.csproj -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

# Copy published output (includes agents/*.md from .csproj Content Include)
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SentimentAnalyzer.API.dll"]
