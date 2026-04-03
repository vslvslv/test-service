#!/bin/bash

# ============================================================================
# Test Service - Full Stack Startup (Linux/macOS)
# ============================================================================
# Starts all services: MongoDB, RabbitMQ, API, and Web UI
# ============================================================================

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo ""
echo "??????????????????????????????????????????????????????????????????"
echo "?         Test Service - Full Stack Startup                     ?"
echo "??????????????????????????????????????????????????????????????????"
echo ""

# Check if Docker is installed and running
echo "[1/5] Checking Docker status..."
if ! command -v docker &> /dev/null; then
    echo -e "${RED}? Docker is not installed!${NC}"
    exit 1
fi

if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}? Docker is not running!${NC}"
    exit 1
fi

echo -e "${GREEN}? Docker is running${NC}"
echo ""

# Determine compose command
if command -v docker-compose &> /dev/null; then
    COMPOSE_CMD="docker-compose"
else
    COMPOSE_CMD="docker compose"
fi

# Check if docker-compose file exists
if [ ! -f "infrastructure/docker-compose.yml" ]; then
    echo -e "${RED}? docker-compose.yml not found!${NC}"
    echo ""
    echo "Please run from the solution root directory."
    exit 1
fi

# Build images
echo "[2/5] Building Docker images..."
echo "This may take a few minutes on first run..."
$COMPOSE_CMD -f infrastructure/docker-compose.yml build --no-cache

if [ $? -ne 0 ]; then
    echo -e "${RED}? Failed to build images!${NC}"
    exit 1
fi

echo -e "${GREEN}? Images built successfully${NC}"
echo ""

# Start all services
echo "[3/5] Starting all services..."
$COMPOSE_CMD -f infrastructure/docker-compose.yml up -d

if [ $? -ne 0 ]; then
    echo -e "${RED}? Failed to start services!${NC}"
    exit 1
fi

echo -e "${GREEN}? Services started${NC}"
echo ""

# Wait for services to be healthy
echo "[4/5] Waiting for services to be healthy..."
echo "This may take 30-60 seconds..."
sleep 10

# Wait for health checks (max 60 seconds)
count=0
while [ $count -lt 60 ]; do
    mongo_health=$(docker inspect --format "{{.State.Health.Status}}" testservice-mongodb 2>/dev/null)
    rabbitmq_health=$(docker inspect --format "{{.State.Health.Status}}" testservice-rabbitmq 2>/dev/null)
    api_health=$(docker inspect --format "{{.State.Health.Status}}" testservice-api 2>/dev/null)
    web_health=$(docker inspect --format "{{.State.Health.Status}}" testservice-web 2>/dev/null)
    
    if [ "$mongo_health" = "healthy" ] && [ "$rabbitmq_health" = "healthy" ] && [ "$api_health" = "healthy" ] && [ "$web_health" = "healthy" ]; then
        echo -e "${GREEN}? All services are healthy!${NC}"
        echo ""
        break
    fi
    
    sleep 1
    ((count++))
done

if [ $count -ge 60 ]; then
    echo -e "${YELLOW}??  Timeout waiting for services. They may still be starting...${NC}"
    echo ""
fi

# Display service status
echo "[5/5] Service Status:"
echo "????????????????????????????????????????????????????????????????"
echo ""

docker ps --filter "name=testservice" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo ""

echo "????????????????????????????????????????????????????????????????"
echo -e "${GREEN}Full Stack is running! ??${NC}"
echo "????????????????????????????????????????????????????????????????"
echo ""
echo "?? Services:"
echo ""
echo -e "${GREEN}   ? MongoDB${NC}"
echo "      ?? mongodb://admin:password123@localhost:27017/TestServiceDb"
echo ""
echo -e "${GREEN}   ? RabbitMQ${NC}"
echo "      ?? AMQP: localhost:5672"
echo "      ?? Management: http://localhost:15672 (guest/guest)"
echo ""
echo -e "${GREEN}   ? API Service${NC}"
echo "      ?? API: http://localhost:5000"
echo "      ?? Swagger: http://localhost:5000/swagger"
echo "      ?? Health: http://localhost:5000/health"
echo ""
echo -e "${GREEN}   ? Web UI${NC}"
echo "      ?? Application: http://localhost:3000"
echo "      ?? Health: http://localhost:3000/health"
echo ""
echo "????????????????????????????????????????????????????????????????"
echo ""
echo "?? Management Commands:"
echo ""
echo "   • View logs:        ./infrastructure/logs-full.sh"
echo "   • Check status:     ./infrastructure/status-full.sh"
echo "   • Stop all:         ./infrastructure/stop-full.sh"
echo "   • Restart all:      ./infrastructure/restart-full.sh"
echo ""
