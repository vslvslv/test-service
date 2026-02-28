#!/bin/bash

# ============================================================================
# Test Service - Local dev stack (pre-Kubernetes verification)
# ============================================================================
# Starts full stack from docker-compose.dev.yml: MongoDB, RabbitMQ, API, Web.
# Use this to verify changes locally before deploying to Kubernetes.
# Run from repository root.
# ============================================================================

set -e
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo ""
echo "============================================================================"
echo "  Test Service - Local dev stack (verify before Kubernetes deploy)"
echo "============================================================================"
echo ""

if ! command -v docker &> /dev/null; then
    echo -e "${RED}Docker is not installed or not in PATH.${NC}"
    exit 1
fi
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Docker is not running.${NC}"
    exit 1
fi

COMPOSE_CMD="docker compose"
if ! docker compose version &> /dev/null; then
    COMPOSE_CMD="docker-compose"
fi

COMPOSE_FILE="infrastructure/docker-compose.dev.yml"
if [ ! -f "$COMPOSE_FILE" ]; then
    echo -e "${RED}$COMPOSE_FILE not found. Run from repository root.${NC}"
    exit 1
fi

echo "[1/4] Building images (API + Web)..."
$COMPOSE_CMD -f "$COMPOSE_FILE" build --no-cache
echo ""

echo "[2/4] Starting all services..."
$COMPOSE_CMD -f "$COMPOSE_FILE" up -d
echo ""

echo "[3/4] Waiting for services (up to 90s)..."
sleep 5
for i in $(seq 1 90); do
    m=$(docker inspect --format '{{.State.Health.Status}}' testservice-mongodb-dev 2>/dev/null || true)
    r=$(docker inspect --format '{{.State.Health.Status}}' testservice-rabbitmq-dev 2>/dev/null || true)
    a=$(docker inspect --format '{{.State.Health.Status}}' testservice-api-dev 2>/dev/null || true)
    w=$(docker inspect --format '{{.State.Health.Status}}' testservice-web-dev 2>/dev/null || true)
    if [ "$m" = "healthy" ] && [ "$r" = "healthy" ] && [ "$a" = "healthy" ] && [ "$w" = "healthy" ]; then
        echo -e "${GREEN}All services healthy.${NC}"
        break
    fi
    [ $i -eq 90 ] && echo -e "${YELLOW}Timeout; some services may still be starting.${NC}"
    sleep 1
done
echo ""

echo "[4/4] Status"
$COMPOSE_CMD -f "$COMPOSE_FILE" ps
echo ""
echo "============================================================================"
echo -e "${GREEN}Local dev stack is running.${NC}"
echo "============================================================================"
echo "  Web UI:    http://localhost:3000"
echo "  API:       http://localhost:5000"
echo "  Swagger:   http://localhost:5000/swagger"
echo "  MongoDB:   mongodb://admin:password123@localhost:27017/TestServiceDb"
echo "  RabbitMQ:  http://localhost:15672 (guest/guest)"
echo "============================================================================"
echo "  Stop:      docker compose -f $COMPOSE_FILE down"
echo "  Logs:      docker compose -f $COMPOSE_FILE logs -f"
echo "============================================================================"
echo ""
