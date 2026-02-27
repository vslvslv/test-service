# Repository Guidelines

## Project Structure & Module Organization
- `TestService.Api/`: ASP.NET Core API (`Controllers/`, `Services/`, `Models/`, `Configuration/`, `BackgroundServices/`).
- `TestService.Tests/`: NUnit integration and end-to-end tests, organized by feature (for example `Integration/Entities/` and `EndToEnd/`).
- `testservice-web/`: React + TypeScript + Vite frontend (`src/components`, `src/pages`, `src/services`, `src/contexts`).
- `infrastructure/`: Docker Compose files and helper scripts for infra-only, full-stack, and dev-stack runs.
- `k8s/` and `documents/`: Kubernetes manifests and extended technical documentation.

## Build, Test, and Development Commands
- `dotnet build test-service.sln`: Build API and tests.
- `dotnet run --project TestService.Api`: Run API locally (Swagger at `http://localhost:5000/swagger` by default).
- `dotnet test TestService.Tests/TestService.Tests.csproj`: Run backend test suite.
- `cd testservice-web && npm ci`: Install frontend dependencies.
- `cd testservice-web && npm run dev`: Run frontend in dev mode (Vite).
- `cd testservice-web && npm run build`: Produce production frontend build.
- `cd testservice-web && npm run lint`: Run ESLint on TS/TSX code.
- `docker compose -f infrastructure/docker-compose.yml up -d`: Start full container stack.

## Coding Style & Naming Conventions
- C#: 4-space indentation, nullable reference types enabled, `PascalCase` for types/methods, `camelCase` for locals/parameters, interfaces prefixed with `I`.
- TypeScript/React: follow ESLint config (`testservice-web/eslint.config.js`), 2-space indentation, `PascalCase` for components, `camelCase` for hooks/utilities.
- Keep service and controller names aligned by feature (for example `SettingsController` + `SettingsRepository`).

## Testing Guidelines
- Framework: NUnit (`[TestFixture]`, `[Test]`) with `Microsoft.AspNetCore.Mvc.Testing`.
- Place tests near related feature folders and use descriptive names like `CreateEntity_WithAllFields_ReturnsCreated`.
- Run full tests with `dotnet test`; run a subset with `dotnet test --filter "FullyQualifiedName~EntityCrud"`.
- Coverlet collector is enabled; collect coverage when needed via `dotnet test --collect:"XPlat Code Coverage"`.

## Commit & Pull Request Guidelines
- Match existing commit style: short, imperative summaries (for example `Fix CORS for GitHub Pages login requests`).
- Keep subject lines focused on one change and below ~72 characters when possible.
- PRs should include: purpose, impacted areas (`Api`, `Tests`, `Web`, `Infrastructure`), test evidence (command output), and linked issue/task.
- Include screenshots for UI changes and note any config/env var updates (never commit secrets).
