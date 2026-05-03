#!/usr/bin/env bash
# pre-pr.sh — local pre-PR pipeline
#
# Usage:
#   ./pre-pr.sh                      # full run
#   ./pre-pr.sh --skip-build         # reuse existing Docker images
#   ./pre-pr.sh --skip-e2e           # unit + integration only (no Docker needed)
#   ./pre-pr.sh --keep-stack         # leave Docker stack running after run
#   ./pre-pr.sh --fail-fast          # stop on first failure
#   ./pre-pr.sh --help
set -euo pipefail

# ── Paths ──────────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

COMPOSE_FILE="infrastructure/docker-compose.dev.yml"
RESULTS_DIR="$SCRIPT_DIR/test-results"
E2E_SETTINGS="TestService.E2E/playwright.runsettings"
API_URL="http://localhost:5000"
WEB_URL="http://localhost:3000"
REPORT_FILE="$SCRIPT_DIR/pre-pr-report-$(date +%Y%m%d-%H%M%S).md"

# ── Flags ──────────────────────────────────────────────────────────────────────
SKIP_BUILD=false
SKIP_UNIT=false
SKIP_INTEGRATION=false
SKIP_E2E=false
KEEP_STACK=false
FAIL_FAST=false

for arg in "$@"; do
  case $arg in
    --skip-build)       SKIP_BUILD=true ;;
    --skip-unit)        SKIP_UNIT=true ;;
    --skip-integration) SKIP_INTEGRATION=true ;;
    --skip-e2e)         SKIP_E2E=true ;;
    --keep-stack)       KEEP_STACK=true ;;
    --fail-fast)        FAIL_FAST=true ;;
    --help|-h)
      grep '^#' "$0" | head -10 | sed 's/^# \?//'
      exit 0 ;;
    *) echo "Unknown flag: $arg" >&2; exit 1 ;;
  esac
done

# ── Colors ────────────────────────────────────────────────────────────────────
if [ -t 1 ]; then
  RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
  CYAN='\033[0;36m'; BOLD='\033[1m'; DIM='\033[2m'; RESET='\033[0m'
else
  RED=''; GREEN=''; YELLOW=''; CYAN=''; BOLD=''; DIM=''; RESET=''
fi

# ── Helpers ───────────────────────────────────────────────────────────────────
section() { echo -e "\n${BOLD}${CYAN}▶ $1${RESET}"; }
ok()      { echo -e "  ${GREEN}✔${RESET}  $1"; }
warn()    { echo -e "  ${YELLOW}⚠${RESET}  $1"; }
fail()    { echo -e "  ${RED}✖${RESET}  $1"; }
info()    { echo -e "  ${DIM}   $1${RESET}"; }

elapsed() {
  local s=$(( $(date +%s) - $1 ))
  printf '%dm%02ds' $(( s/60 )) $(( s%60 ))
}

# Docker compose command (v2 preferred, v1 fallback)
if docker compose version &>/dev/null 2>&1; then
  COMPOSE="docker compose"
elif command -v docker-compose &>/dev/null; then
  COMPOSE="docker-compose"
else
  fail "Neither 'docker compose' nor 'docker-compose' found"; exit 1
fi

# Parse the dotnet test summary lines from a captured log file
parse_dotnet_results() {
  local log="$1"
  local total passed failed skipped
  total=$(grep -E '^\s*Total tests?:' "$log" | tail -1 | grep -o '[0-9]*' | tail -1 || true)
  passed=$(grep -E '^\s*Passed:' "$log" | tail -1 | grep -o '[0-9]*' | tail -1 || true)
  failed=$(grep -E '^\s*Failed:' "$log" | tail -1 | grep -o '[0-9]*' | tail -1 || true)
  skipped=$(grep -E '^\s*(Skipped|Not run):' "$log" | tail -1 | grep -o '[0-9]*' | tail -1 || true)
  total="${total:-0}"; passed="${passed:-0}"; failed="${failed:-0}"; skipped="${skipped:-0}"
  if [[ "$total" == "0" ]]; then
    total=$(( passed + failed + skipped ))
  fi
  echo "$total $passed $failed $skipped"
}

# ── State tracking ────────────────────────────────────────────────────────────
PIPELINE_START=$(date +%s)
STAGE_RESULTS=()
OVERALL_STATUS=0

record() {
  # record <name> <status:pass|fail|skip> <total> <passed> <failed> <duration_secs>
  STAGE_RESULTS+=("$1|$2|$3|$4|$5|$6")
  if [[ "$2" == "fail" ]]; then
    OVERALL_STATUS=1
  fi
}

fail_fast_check() {
  if [[ "$FAIL_FAST" == "true" ]]; then
    fail "Stopping early (--fail-fast)"
    exit 1
  fi
}

# ── Cleanup trap ──────────────────────────────────────────────────────────────
cleanup() {
  if [[ "$KEEP_STACK" == "false" ]]; then
    section "Cleanup — bringing stack down"
    $COMPOSE -f "$COMPOSE_FILE" down --remove-orphans 2>/dev/null && ok "Stack down" || true
  else
    warn "Stack left running (--keep-stack). Stop with: $COMPOSE -f $COMPOSE_FILE down"
  fi
}
trap cleanup EXIT

# ── Step 1: Docker build + compose up ────────────────────────────────────────
step_docker() {
  section "Step 1 — Docker build & compose up"
  local t0=$(date +%s)
  local log="$RESULTS_DIR/docker.log"
  local compose_exit=0

  # --wait is docker compose v2 only; v1 relies on the curl health-check below
  local wait_flag=""
  [[ "$COMPOSE" == "docker compose" ]] && wait_flag="--wait"

  if [[ "$SKIP_BUILD" == "true" ]]; then
    warn "Skipping build (--skip-build). Starting existing images."
    $COMPOSE -f "$COMPOSE_FILE" up -d $wait_flag 2>&1 | tee "$log" || compose_exit=$?
  else
    info "Building images and starting services..."
    $COMPOSE -f "$COMPOSE_FILE" up -d --build $wait_flag 2>&1 | tee "$log" || compose_exit=$?
  fi

  if [[ $compose_exit -ne 0 ]]; then
    fail "Docker build / compose up failed (exit $compose_exit) — see $log"
    record "Docker" "fail" 0 0 1 $(( $(date +%s) - t0 ))
    fail_fast_check
    return
  fi

  # Verify API is reachable (belt-and-suspenders after --wait)
  info "Verifying API at $API_URL/health ..."
  local deadline=$(( $(date +%s) + 30 ))
  until curl -sf "$API_URL/health" -o /dev/null 2>/dev/null; do
    if (( $(date +%s) > deadline )); then
      fail "API did not respond within 30s after compose --wait"
      $COMPOSE -f "$COMPOSE_FILE" logs api --tail=20 2>/dev/null || true
      record "Docker" "fail" 0 0 1 $(( $(date +%s) - t0 ))
      fail_fast_check
      return
    fi
    sleep 2
  done
  ok "API healthy at $API_URL"

  if curl -sf "$WEB_URL" -o /dev/null 2>/dev/null; then
    ok "Web UI healthy at $WEB_URL"
  else
    warn "Web UI not responding at $WEB_URL — E2E tests may fail"
  fi

  record "Docker" "pass" 0 0 0 $(( $(date +%s) - t0 ))
}

# ── Step 2: Unit tests ────────────────────────────────────────────────────────
step_unit() {
  section "Step 2 — Unit tests"
  if [[ "$SKIP_UNIT" == "true" ]]; then
    warn "Skipped (--skip-unit)"
    record "Unit" "skip" 0 0 0 0
    return
  fi

  local t0=$(date +%s)
  local log="$RESULTS_DIR/unit.log"
  local trx="$RESULTS_DIR/unit-results.trx"
  local test_exit=0

  dotnet test TestService.Unit/TestService.Unit.csproj \
    --configuration Release \
    --no-build \
    --logger "trx;LogFileName=$trx" \
    --logger "console;verbosity=normal" \
    2>&1 | tee "$log" || test_exit=$?

  local dur=$(( $(date +%s) - t0 ))
  read -r total passed failed skipped <<< "$(parse_dotnet_results "$log")"

  if [[ $test_exit -eq 0 ]]; then
    ok "Unit: $passed/$total passed in $(elapsed $t0)"
    record "Unit" "pass" "$total" "$passed" "$failed" "$dur"
  else
    fail "Unit: $failed/$total failed"
    record "Unit" "fail" "$total" "$passed" "$failed" "$dur"
    fail_fast_check
  fi
}

# ── Step 3: Integration tests ─────────────────────────────────────────────────
step_integration() {
  section "Step 3 — Integration tests"
  if [[ "$SKIP_INTEGRATION" == "true" ]]; then
    warn "Skipped (--skip-integration)"
    record "Integration" "skip" 0 0 0 0
    return
  fi

  local t0=$(date +%s)
  local log="$RESULTS_DIR/integration.log"
  local trx="$RESULTS_DIR/integration-results.trx"
  local test_exit=0

  info "Uses Testcontainers — independent of the Docker stack"
  dotnet test TestService.Tests/TestService.Tests.csproj \
    --configuration Release \
    --no-build \
    --logger "trx;LogFileName=$trx" \
    --logger "console;verbosity=normal" \
    2>&1 | tee "$log" || test_exit=$?

  local dur=$(( $(date +%s) - t0 ))
  read -r total passed failed skipped <<< "$(parse_dotnet_results "$log")"

  if [[ $test_exit -eq 0 ]]; then
    ok "Integration: $passed/$total passed in $(elapsed $t0)"
    record "Integration" "pass" "$total" "$passed" "$failed" "$dur"
  else
    fail "Integration: $failed/$total failed"
    record "Integration" "fail" "$total" "$passed" "$failed" "$dur"
    fail_fast_check
  fi
}

# ── Step 4: E2E tests ─────────────────────────────────────────────────────────
step_e2e() {
  section "Step 4 — E2E tests (Playwright)"
  if [[ "$SKIP_E2E" == "true" ]]; then
    warn "Skipped (--skip-e2e)"
    record "E2E" "skip" 0 0 0 0
    return
  fi

  local t0=$(date +%s)
  local log="$RESULTS_DIR/e2e.log"
  local trx="$RESULTS_DIR/e2e-results.trx"
  local test_exit=0

  # Install Playwright browsers (idempotent — skips if already cached)
  local pw_script="TestService.E2E/bin/Release/net10.0/playwright.ps1"
  if [[ ! -f "$pw_script" ]]; then
    info "Building E2E project for Playwright install script..."
    dotnet build TestService.E2E/TestService.E2E.csproj --configuration Release -o "$RESULTS_DIR/e2e-build" \
      2>&1 | tail -3
    pw_script="$RESULTS_DIR/e2e-build/playwright.ps1"
  fi
  if [[ -f "$pw_script" ]]; then
    if command -v pwsh &>/dev/null; then
      info "Verifying Playwright browsers..."
      pwsh "$pw_script" install chromium --with-deps 2>&1 | tail -3 || true
    else
      warn "pwsh not found — skipping browser install (assumes browsers are cached)"
    fi
  fi

  BASE_URL="$WEB_URL" E2E_USER="admin" E2E_PASSWORD="Admin@123" \
  dotnet test TestService.E2E/TestService.E2E.csproj \
    --configuration Release \
    --settings "$E2E_SETTINGS" \
    --logger "trx;LogFileName=$trx" \
    --logger "console;verbosity=normal" \
    2>&1 | tee "$log" || test_exit=$?

  local dur=$(( $(date +%s) - t0 ))
  read -r total passed failed skipped <<< "$(parse_dotnet_results "$log")"

  if [[ $test_exit -eq 0 ]]; then
    ok "E2E: $passed/$total passed in $(elapsed $t0)"
    record "E2E" "pass" "$total" "$passed" "$failed" "$dur"
  else
    fail "E2E: $failed/$total failed — see $log"
    record "E2E" "fail" "$total" "$passed" "$failed" "$dur"
    fail_fast_check
  fi
}

# ── Step 5: Report ────────────────────────────────────────────────────────────
write_report() {
  section "Step 5 — Writing report"

  local branch commit date_str
  branch=$(git branch --show-current 2>/dev/null || echo "unknown")
  commit=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
  date_str=$(date '+%Y-%m-%d %H:%M:%S')

  local overall_label status_icon
  if [[ $OVERALL_STATUS -eq 0 ]]; then
    overall_label="READY TO MERGE"; status_icon="✅"
  else
    overall_label="FAILURES — DO NOT MERGE"; status_icon="❌"
  fi

  {
    echo "# Pre-PR Report — $date_str"
    echo ""
    echo "**Branch:** \`$branch\`  |  **Commit:** \`$commit\`  |  **Total time:** $(elapsed "$PIPELINE_START")"
    echo ""
    echo "## $status_icon Overall: $overall_label"
    echo ""
    echo "## Stage Results"
    echo ""
    echo "| Stage | Result | Total | Passed | Failed | Duration |"
    echo "|-------|--------|------:|-------:|-------:|----------|"

    for entry in "${STAGE_RESULTS[@]}"; do
      IFS='|' read -r name status total passed failed dur <<< "$entry"
      local icon
      case $status in
        pass) icon="✅ PASS" ;;
        fail) icon="❌ FAIL" ;;
        skip) icon="⏭ SKIP" ;;
        *)    icon="❓" ;;
      esac
      local dur_str
      dur_str=$(printf '%dm%02ds' $(( dur/60 )) $(( dur%60 )))
      echo "| $name | $icon | $total | $passed | $failed | $dur_str |"
    done

    echo ""
    echo "## Artifacts"
    echo ""
    echo "| File | Description |"
    echo "|------|-------------|"
    if [[ -f "$RESULTS_DIR/unit-results.trx" ]]; then
      echo "| \`test-results/unit-results.trx\` | Unit test TRX |"
    fi
    if [[ -f "$RESULTS_DIR/integration-results.trx" ]]; then
      echo "| \`test-results/integration-results.trx\` | Integration test TRX |"
    fi
    if [[ -f "$RESULTS_DIR/e2e-results.trx" ]]; then
      echo "| \`test-results/e2e-results.trx\` | E2E test TRX |"
    fi
    if [[ -f "$RESULTS_DIR/docker.log" ]]; then
      echo "| \`test-results/docker.log\` | Docker build & compose output |"
    fi

    echo ""
    echo "## How to Re-Run"
    echo ""
    echo "\`\`\`bash"
    echo "# Full run:"
    echo "./pre-pr.sh"
    echo ""
    echo "# Skip Docker build (reuse existing images):"
    echo "./pre-pr.sh --skip-build --keep-stack"
    echo ""
    echo "# Unit + integration only (no Docker needed):"
    echo "./pre-pr.sh --skip-e2e"
    echo ""
    echo "# Debug E2E failures against a running stack:"
    echo "./pre-pr.sh --skip-unit --skip-integration --keep-stack"
    echo "\`\`\`"

    if [[ $OVERALL_STATUS -ne 0 ]]; then
      echo ""
      echo "## Failed Stages"
      echo ""
      for entry in "${STAGE_RESULTS[@]}"; do
        IFS='|' read -r name status _ <<< "$entry"
        if [[ "$status" == "fail" ]]; then
          echo "- **$name**: \`test-results/$(echo "$name" | tr '[:upper:]' '[:lower:]').log\`"
        fi
      done
    fi

  } > "$REPORT_FILE"

  ok "Report: $(basename "$REPORT_FILE")"
}

# ── Main ──────────────────────────────────────────────────────────────────────
main() {
  echo -e "${BOLD}═══════════════════════════════════════════"
  echo -e " pre-pr.sh — $(date '+%Y-%m-%d %H:%M:%S')"
  echo -e " branch: $(git branch --show-current 2>/dev/null || echo 'unknown')"
  echo -e " commit: $(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
  echo -e "═══════════════════════════════════════════${RESET}"

  command -v dotnet &>/dev/null || { fail "dotnet SDK not found"; exit 1; }
  command -v docker  &>/dev/null || { fail "docker not found"; exit 1; }

  # Build solution first so --no-build works for each test step
  section "Building solution"
  dotnet build test-service.sln --configuration Release 2>&1 | tail -5

  mkdir -p "$RESULTS_DIR"

  step_docker
  step_unit
  step_integration
  step_e2e
  write_report

  echo ""
  echo -e "${BOLD}═══════════════════════════════════════════${RESET}"
  if [[ $OVERALL_STATUS -eq 0 ]]; then
    echo -e "${GREEN}${BOLD}  ✅  All stages passed — ready to push${RESET}"
  else
    echo -e "${RED}${BOLD}  ❌  One or more stages failed — see report${RESET}"
  fi
  echo -e "${DIM}  $(basename "$REPORT_FILE")${RESET}"
  echo -e "${BOLD}═══════════════════════════════════════════${RESET}"

  exit $OVERALL_STATUS
}

main "$@"
