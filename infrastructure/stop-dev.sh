#!/bin/bash

# ============================================================================
# Test Service - Stop local dev stack
# ============================================================================

COMPOSE_FILE="infrastructure/docker-compose.dev.yml"
if command -v docker &> /dev/null && docker compose version &> /dev/null 2>&1; then
    docker compose -f "$COMPOSE_FILE" down
elif command -v docker-compose &> /dev/null; then
    docker-compose -f "$COMPOSE_FILE" down
else
    echo "Docker Compose not found."
    exit 1
fi
echo "Dev stack stopped. To remove volumes: docker compose -f $COMPOSE_FILE down -v"
