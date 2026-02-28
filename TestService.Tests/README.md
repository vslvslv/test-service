# Test Project Guide

## Structure
- `Integration/<Feature>/`: API integration tests grouped by feature area.
- `EndToEnd/`: multi-feature workflow tests.
- `Infrastructure/`: shared test base, helpers, and utility builders.

## CI Gate
- CI runs backend tests on each push/PR for `main` and `develop`.
- Coverage is collected with Coverlet (`cobertura` output).
- Current enforced gate: `67%` total line coverage.
- Planned next target: raise gate to `70%` after additional gap closure.

## Local Commands
- Run all backend tests:
  - `dotnet test TestService.Tests/TestService.Tests.csproj`
- Run with coverage summary:
  - `dotnet test TestService.Tests/TestService.Tests.csproj /p:CollectCoverage=true /p:CoverletOutput=./TestResults/Coverage/ /p:CoverletOutputFormat=cobertura`
