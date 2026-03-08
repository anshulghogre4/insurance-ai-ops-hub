#!/bin/bash
# Post-deploy smoke tests for Insurance AI Operations Hub
# Exit on first failure
set -e

API_URL="${API_URL:-http://localhost:8080}"
FRONTEND_URL="${FRONTEND_URL:-https://app1.anshulghogre.co.in}"
TIMEOUT=10
PASS=0
FAIL=0

check() {
  local name="$1"
  local url="$2"
  local expected="$3"

  response=$(curl -sf --max-time "$TIMEOUT" "$url" 2>/dev/null) || {
    echo "FAIL: $name — $url (request failed or timed out)"
    FAIL=$((FAIL + 1))
    return 1
  }

  if echo "$response" | grep -qi "$expected"; then
    echo "PASS: $name"
    PASS=$((PASS + 1))
  else
    echo "FAIL: $name — expected '$expected' in response"
    FAIL=$((FAIL + 1))
    return 1
  fi
}

echo "=== Smoke Tests ==="
echo "API:      $API_URL"
echo "Frontend: $FRONTEND_URL"
echo ""

# Backend health checks
check "Liveness probe"  "$API_URL/health"       "Healthy"
check "Readiness probe" "$API_URL/health/ready"  "Ready"
check "Provider health" "$API_URL/api/insurance/health/providers" "providers"

# Frontend check
check "Frontend loads" "$FRONTEND_URL" "app-root"

echo ""
echo "=== Results: $PASS passed, $FAIL failed ==="

if [ "$FAIL" -gt 0 ]; then
  exit 1
fi
