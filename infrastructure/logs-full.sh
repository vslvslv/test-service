#!/bin/bash

# ============================================================================
# Test Service - Logs Viewer for Full Stack (Linux/macOS)
# ============================================================================

BLUE='\033[0;34m'
NC='\033[0m'

echo ""
echo "??????????????????????????????????????????????????????????????????"
echo "?         Test Service - Full Stack Logs                        ?"
echo "??????????????????????????????????????????????????????????????????"
echo ""

# Determine compose command
if command -v docker-compose &> /dev/null; then
    COMPOSE_CMD="docker-compose"
else
    COMPOSE_CMD="docker compose"
fi

echo "Select service to view logs:"
echo ""
echo "  1. All services"
echo "  2. MongoDB"
echo "  3. RabbitMQ"
echo "  4. API"
echo "  5. Web UI"
echo "  6. Exit"
echo ""

read -p "Select option (1-6): " option

case $option in
    1)
        echo ""
        echo -e "${BLUE}Showing logs for all services (Ctrl+C to exit)...${NC}"
        echo ""
        $COMPOSE_CMD -f infrastructure/docker-compose.yml logs -f
        ;;
    2)
        echo ""
        echo -e "${BLUE}Showing MongoDB logs (Ctrl+C to exit)...${NC}"
        echo ""
        docker logs -f testservice-mongodb
        ;;
    3)
        echo ""
        echo -e "${BLUE}Showing RabbitMQ logs (Ctrl+C to exit)...${NC}"
        echo ""
        docker logs -f testservice-rabbitmq
        ;;
    4)
        echo ""
        echo -e "${BLUE}Showing API logs (Ctrl+C to exit)...${NC}"
        echo ""
        docker logs -f testservice-api
        ;;
    5)
        echo ""
        echo -e "${BLUE}Showing Web UI logs (Ctrl+C to exit)...${NC}"
        echo ""
        docker logs -f testservice-web
        ;;
    6)
        echo "Exiting..."
        exit 0
        ;;
    *)
        echo "Invalid option"
        exit 1
        ;;
esac
