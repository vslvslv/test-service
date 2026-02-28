# Test Plan And Coverage Gap Analysis

## Scope
- Backend API integration tests (`TestService.Tests`).
- Frontend automated tests (`testservice-web`).
- End-to-end critical flows and infrastructure smoke checks.

## Current Coverage Snapshot

### Backend: Strong Coverage
- Dynamic entities CRUD, filtering, sequencing, reset, uniqueness, duplicate behavior, and parallel access are heavily covered.
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/Entities/EntityCrudTests.cs`
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/Entities/EntityDuplicateTests.cs`
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/Entities/EntityEnvironmentTests.cs`
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/Entities/ParallelExecutionTests.cs`
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/Entities/EntityIsUniquePropertyTests.cs`
- Schemas API has strong create/read/update/delete validation and lifecycle coverage.
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/Schemas/SchemaTests.cs`
- Environments API has good CRUD, activation/deactivation, and statistics coverage.
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/Environments/EnvironmentTests.cs`
- Settings and API keys are strongly covered, including validation and lifecycle.
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/SettingsControllerTests.cs`
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/ApiKeysControllerTests.cs`
- End-to-end workflow exists for key schema/entity journeys.
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/EndToEnd/CompleteWorkflowTests.cs`

### Backend: Partial Coverage
- Auth is covered mainly for login and basic health checks.
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/ApiHealthAndAuthTests.cs`
- Users API has limited coverage (list, permissions catalog, simple create/list path).
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/UsersControllerTests.cs`
- Mocks API has minimal coverage (expectation match/no-match and verify count).
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/Integration/MocksControllerTests.cs`
- TestData API is covered but in legacy-style root test file rather than feature folder.
  - `/Users/vasilvasilev/Repositories/test-service/TestService.Tests/TestDataApiTests.cs`

### Backend: Missing Coverage
- Activities endpoints are not covered.
  - API: `/Users/vasilvasilev/Repositories/test-service/TestService.Api/Controllers/ActivitiesController.cs`
- Auth `change-password` endpoint has no direct test coverage.
  - API: `/Users/vasilvasilev/Repositories/test-service/TestService.Api/Controllers/AuthController.cs:104`

### Frontend: Missing Coverage
- No unit/component/browser automation tests are present.
- `testservice-web/package.json` has no test script (`vitest`, `jest`, `playwright`, or `cypress` are not configured).
  - `/Users/vasilvasilev/Repositories/test-service/testservice-web/package.json`

## Endpoint Coverage Matrix

### Covered or Mostly Covered
- `DynamicEntitiesController` (`/api/entities/*`): mostly covered.
- `SchemasController` (`/api/schemas/*`): mostly covered.
- `EnvironmentsController` (`/api/environments/*`): mostly covered.
- `SettingsController` (`/api/settings` + `/api/settings/api-keys/*`): mostly covered.
- `TestDataController` (`/api/testdata/*`): covered.

### Partial
- `AuthController`: `login` covered, `me` indirectly covered, `change-password` missing.
- `UsersController`: list/create/catalog covered; get-by-id, get-by-username, update, delete behavior matrix incomplete.
- `MocksController`: create + verify covered; list/update/delete expectations and request-log endpoints missing.

### Missing
- `ActivitiesController`: list/recent/stats missing.

## Risk-Based Gaps
- Authorization matrix is not systematically verified per endpoint and role.
- Input validation and error-contract assertions are uneven (400/401/403/404/409 consistency).
- Frontend regressions are currently caught only manually.
- Mockserver feature area is under-tested versus expected product behavior.

## Proposed Phased Plan

## Phase 1: Testing Foundation
- Standardize test taxonomy:
  - `Integration/<Feature>/...`
  - `EndToEnd/...`
  - `Contract/...` (optional)
- Add CI gates:
  - run backend tests on every PR.
  - enforce minimum backend coverage threshold (start with 70%, then raise).
- Move/align legacy tests (for example `TestDataApiTests`) into feature folders.

## Phase 2: Close API Blind Spots (High Priority)
- Add `AuthController` tests for:
  - `POST /api/auth/change-password` success and failure paths.
  - token/identity continuity after password change.
- Expand `UsersController` tests for:
  - `GET /api/users/{id}`, `GET /api/users/username/{username}`.
  - `PUT /api/users/{id}` happy path and invalid payloads.
  - `DELETE /api/users/{id}` success/not-found/forbidden behavior.
- Expand `MocksController` tests for:
  - `GET /expectations`, `PUT /expectations/{id}`, `DELETE /expectations/{id}`.
  - `GET /requests`, `DELETE /requests` with environment filters.
  - verify with ranges (`atLeast`/`atMost`) and mismatch diagnostics.
- Add full `ActivitiesController` tests:
  - list pagination/filter behavior.
  - recent feed.
  - stats calculation and boundaries.

## Phase 3: Security And Negative Matrix
- Add parameterized tests across major controllers for:
  - unauthorized (no token), invalid token, insufficient permissions.
  - invalid IDs, malformed payloads, unknown resources.
- Ensure all endpoints return consistent error shape and codes.

## Phase 4: Frontend Unit/Component Tests
- Introduce `vitest` + React Testing Library.
- Add tests for:
  - `Users` page table alignment/rendering states and create-user permissions UI behavior.
  - `Mocks` page environment selectors, forms, logs filtering, and graph time-range controls.
  - `ProtectedRoute`, `Layout`, and auth-context guard paths.
- Add basic service-layer tests for API client and error handling.

## Phase 5: Frontend E2E Flows
- Introduce Playwright for critical journeys:
  - login/logout.
  - schema creation and entity CRUD.
  - users management create/edit/delete.
  - mocks expectation creation + request verification + logs graph filtering.
- Run E2E on PR smoke subset and nightly full suite.

## Phase 6: Non-Functional And Reliability
- Add targeted concurrency tests for mocks and users endpoints.
- Add performance baselines for high-traffic routes.
- Add containerized smoke tests for deployed stack:
  - app boot health.
  - auth + users + mocks key flow probe.

## Done Criteria
- Every controller endpoint has at least one happy-path and one negative-path integration test.
- High-risk endpoints (auth/users/mocks/activities) have authorization and validation matrix coverage.
- Frontend has both component-level and E2E safety net for critical screens.
- CI reports coverage trend and blocks regressions below agreed thresholds.
