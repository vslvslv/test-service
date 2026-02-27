#!/bin/bash

# ============================================================================
# Test Service Infrastructure - Start Script (Linux/macOS)
# ============================================================================
# This script starts MongoDB and RabbitMQ containers for local development
# ============================================================================

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo ""
echo "??????????????????????????????????????????????????????????????????"
echo "?         Test Service - Infrastructure Startup                 ?"
echo "??????????????????????????????????????????????????????????????????"
echo ""

# Check if Docker is installed
echo "[1/5] Checking Docker installation..."
if ! command -v docker &> /dev/null; then
    echo -e "${RED}? Docker is not installed!${NC}"
    echo ""
    echo "Please install Docker first:"
    echo "  - Visit: https://docs.docker.com/get-docker/"
    echo ""
    exit 1
fi

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}? Docker is not running!${NC}"
    echo ""
    echo "Please start Docker first:"
    echo "  - macOS: Open Docker Desktop"
    echo "  - Linux: sudo systemctl start docker"
    echo ""
    exit 1
fi

echo -e "${GREEN}? Docker is running${NC}"
echo ""

# Check if docker-compose is available
echo "[2/5] Checking Docker Compose..."
if command -v docker-compose &> /dev/null; then
    COMPOSE_CMD="docker-compose"
elif docker compose version &> /dev/null; then
    COMPOSE_CMD="docker compose"
else
    echo -e "${RED}? Docker Compose is not installed!${NC}"
    echo ""
    echo "Please install Docker Compose:"
    echo "  - Visit: https://docs.docker.com/compose/install/"
    echo ""
    exit 1
fi
echo -e "${GREEN}? Docker Compose available${NC}"
echo ""

# Check if containers are already running
echo "[3/5] Checking existing containers..."
if docker ps --filter "name=testservice-mongodb" --filter "name=testservice-rabbitmq" --format "{{.Names}}" | grep -q "testservice"; then
    echo -e "${YELLOW}??  Containers are already running${NC}"
    echo ""
    read -p "Do you want to restart them? (y/n) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "Stopping existing containers..."
        $COMPOSE_CMD -f infrastructure/docker-compose.yml down
        echo ""
    else
        echo "Keeping existing containers running..."
        sleep 1
        SKIP_START=true
    fi
fi

# Start services
if [ "$SKIP_START" != "true" ]; then
    echo "[4/5] Starting infrastructure services..."
    $COMPOSE_CMD -f infrastructure/docker-compose.yml up -d

    if [ $? -ne 0 ]; then
        echo -e "${RED}? Failed to start services!${NC}"
        echo ""
        echo "Troubleshooting:"
        echo "  - Check if ports 27017, 5672, 15672 are available"
        echo "  - Run: netstat -tuln | grep '27017\\|5672\\|15672'"
        echo "  - Check Docker logs: docker-compose -f infrastructure/docker-compose.yml logs"
        echo ""
        exit 1
    fi
    echo -e "${GREEN}? Services started${NC}"
    echo ""

    # Wait for services to be ready
    echo "[5/5] Waiting for services to be healthy..."
    sleep 5

    # Wait for health checks (max 30 seconds)
    count=0
    while [ $count -lt 30 ]; do
        mongo_health=$(docker inspect --format "{{.State.Health.Status}}" testservice-mongodb 2>/dev/null)
        rabbitmq_health=$(docker inspect --format "{{.State.Health.Status}}" testservice-rabbitmq 2>/dev/null)
        
        if [ "$mongo_health" = "healthy" ] && [ "$rabbitmq_health" = "healthy" ]; then
            echo -e "${GREEN}? All services are healthy!${NC}"
            echo ""
            break
        fi
        
        sleep 1
        ((count++))
    done

    if [ $count -ge 30 ]; then
        echo -e "${YELLOW}??  Timeout waiting for services. They may still be starting...${NC}"
        echo ""
    fi
fi

# Display service status
echo "[Service Status]"
echo "????????????????????????????????????????????????????????????????"
echo ""

docker ps --filter "name=testservice" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo ""

# Check MongoDB
if docker ps | grep -q "testservice-mongodb"; then
    echo -e "${GREEN}? MongoDB${NC}"
    echo "   ?? Connection: mongodb://admin:password123@localhost:27017/TestServiceDb"
    echo "   ?? Database: TestServiceDb"
else
    echo -e "${RED}? MongoDB is not running${NC}"
fi
echo ""

# Check RabbitMQ
if docker ps | grep -q "testservice-rabbitmq"; then
    echo -e "${GREEN}? RabbitMQ${NC}"
    echo "   ?? AMQP Port: localhost:5672"
    echo "   ?? Management UI: http://localhost:15672"
    echo "   ?? Credentials: guest / guest"
else
    echo -e "${RED}? RabbitMQ is not running${NC}"
fi
echo ""

echo "????????????????????????????????????????????????????????????????"
echo -e "${GREEN}Infrastructure is ready! ??${NC}"
echo "????????????????????????????????????????????????????????????????"
echo ""
echo "Next Steps:"
echo ""
echo "  1. Start the API:"
echo "     cd TestService.Api"
echo "     dotnet run"
echo ""
echo "  2. Access Swagger UI:"
echo "     https://localhost:5001/swagger"
echo ""
echo "  3. Run tests:"
echo "     cd TestService.Tests"
echo "     dotnet test"
echo ""
echo "  4. Stop infrastructure:"
echo "     ./infrastructure/stop.sh"
echo ""
echo "  5. View logs:"
echo "     ./infrastructure/logs.sh"
echo ""
