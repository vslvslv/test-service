#!/bin/bash

# ============================================================================
# Test Service Infrastructure - Clean/Reset (Linux/macOS)
# ============================================================================

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo ""
echo "??????????????????????????????????????????????????????????????????"
echo "?         Test Service - Clean Infrastructure                   ?"
echo "??????????????????????????????????????????????????????????????????"
echo ""

echo -e "${YELLOW}??  WARNING: This will remove all containers, volumes, and data!${NC}"
echo ""
read -p "Are you sure you want to continue? (yes/no) " -r
echo ""

if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
    echo -e "${RED}? Operation cancelled${NC}"
    exit 0
fi

# Determine compose command
if command -v docker-compose &> /dev/null; then
    COMPOSE_CMD="docker-compose"
else
    COMPOSE_CMD="docker compose"
fi

echo "Stopping and removing containers..."
$COMPOSE_CMD -f infrastructure/docker-compose.yml down -v

if [ $? -eq 0 ]; then
    echo -e "${GREEN}? Containers and volumes removed${NC}"
else
    echo -e "${RED}? Failed to clean infrastructure${NC}"
    exit 1
fi

echo ""
echo "Removing unused Docker resources..."
docker system prune -f

echo ""
echo -e "${GREEN}? Infrastructure cleaned successfully!${NC}"
echo ""
echo "To start fresh:"
echo "  ./infrastructure/start.sh"
echo ""
