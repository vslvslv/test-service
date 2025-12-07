# Infrastructure Scripts

This folder contains all infrastructure management scripts for the Test Service project.

## ?? Contents

### Core Files
| File | Description | Platform |
|------|-------------|----------|
| `docker-compose.yml` | **Full Stack** - All services (MongoDB, RabbitMQ, API, Web) | All |
| `docker-compose.dev.yml` | **Development** - Infrastructure only (MongoDB, RabbitMQ) | All |

### Infrastructure Only Scripts
| File | Description | Platform |
|------|-------------|----------|
| `start.bat` / `start.sh` | Start infrastructure only | Windows / Linux/macOS |
| `stop.bat` / `stop.sh` | Stop infrastructure | Windows / Linux/macOS |
| `status.bat` / `status.sh` | Check infrastructure status | Windows / Linux/macOS |
| `logs.bat` / `logs.sh` | View infrastructure logs | Windows / Linux/macOS |

### Full Stack Scripts
| File | Description | Platform |
|------|-------------|----------|
| `start-full.bat` / `start-full.sh` | Start all services (MongoDB, RabbitMQ, API, Web) | Windows / Linux/macOS |
| `stop-full.bat` / `stop-full.sh` | Stop all services | Windows / Linux/macOS |
| `logs-full.bat` / `logs-full.sh` | View all service logs | Windows / Linux/macOS |

### Maintenance
| File | Description | Platform |
|------|-------------|----------|
| `clean.bat` / `clean.sh` | Clean/reset everything | Windows / Linux/macOS |

## ?? Quick Start

### Development Mode (Infrastructure Only)

Run infrastructure only, then start API and Web from IDE/terminal:

**Windows:**
```cmd
infrastructure\start.bat
```

**Linux/macOS:**
```bash
chmod +x infrastructure/*.sh  # First time only
./infrastructure/start.sh
```

Then start services manually:
```bash
# Terminal 1 - API
cd TestService.Api
dotnet run

# Terminal 2 - Web
cd testservice-web
npm run dev
```

### Full Stack Mode (Everything in Docker)

Run everything in Docker containers:

**Windows:**
```cmd
infrastructure\start-full.bat
```

**Linux/macOS:**
```bash
chmod +x infrastructure/*.sh  # First time only
./infrastructure/start-full.sh
```

## ?? Services

### Infrastructure Mode

| Service | Port | Access |
|---------|------|--------|
| MongoDB | 27017 | `mongodb://admin:password123@localhost:27017/TestServiceDb` |
| RabbitMQ | 5672, 15672 | http://localhost:15672 (guest/guest) |

### Full Stack Mode

| Service | Port | Access |
|---------|------|--------|
| MongoDB | 27017 | `mongodb://admin:password123@localhost:27017/TestServiceDb` |
| RabbitMQ | 5672, 15672 | http://localhost:15672 (guest/guest) |
| API | 5000 | http://localhost:5000/swagger |
| Web UI | 3000 | http://localhost:3000 |

## ?? Commands

### Infrastructure Only

```bash
# Start
infrastructure\start.bat      # Windows
./infrastructure/start.sh     # Linux/macOS

# Status
infrastructure\status.bat
./infrastructure/status.sh

# Logs
infrastructure\logs.bat
./infrastructure/logs.sh

# Stop
infrastructure\stop.bat
./infrastructure/stop.sh
```

### Full Stack

```bash
# Start all services
infrastructure\start-full.bat      # Windows
./infrastructure/start-full.sh     # Linux/macOS

# View logs
infrastructure\logs-full.bat
./infrastructure/logs-full.sh

# Stop all services
infrastructure\stop-full.bat
./infrastructure/stop-full.sh

# Clean everything
infrastructure\clean.bat
./infrastructure/clean.sh
```

## ??? Architecture

### Development Mode
```
Local Machine
??? Docker Containers
?   ??? MongoDB (port 27017)
?   ??? RabbitMQ (ports 5672, 15672)
??? Local Processes
    ??? API (dotnet run) ? port 5000
    ??? Web (npm run dev) ? port 5173
```

### Full Stack Mode
```
Docker Containers
??? MongoDB (port 27017)
??? RabbitMQ (ports 5672, 15672)
??? API (port 5000)
??? Web (port 3000)
```

## ?? When to Use Each Mode

### Use **Infrastructure Only** (`start.bat`) when:
- ? Developing and debugging API or Web UI
- ? Running tests locally
- ? Hot reload needed for fast development
- ? Using IDE debugger

### Use **Full Stack** (`start-full.bat`) when:
- ? Testing complete system integration
- ? Demonstrating the application
- ? Simulating production environment
- ? Testing Docker builds
- ? CI/CD pipeline testing

## ?? Health Checks

All services include health checks:

**Check Service Health:**
```bash
# MongoDB
docker inspect testservice-mongodb --format "{{.State.Health.Status}}"

# RabbitMQ
docker inspect testservice-rabbitmq --format "{{.State.Health.Status}}"

# API (Full Stack only)
docker inspect testservice-api --format "{{.State.Health.Status}}"
curl http://localhost:5000/health

# Web (Full Stack only)
docker inspect testservice-web --format "{{.State.Health.Status}}"
curl http://localhost:3000/health
```

## ?? Configuration

### Environment Variables (Full Stack)

API Configuration:
- `ASPNETCORE_ENVIRONMENT` - Environment (Development/Production)
- `MongoDbSettings__ConnectionString` - MongoDB connection
- `RabbitMqSettings__HostName` - RabbitMQ host
- `JwtSettings__SecretKey` - JWT signing key

Web UI Configuration:
- `VITE_API_BASE_URL` - API base URL

### Customizing Settings

Create a `.env` file in the infrastructure folder:

```env
# MongoDB
MONGO_USER=admin
MONGO_PASSWORD=your_secure_password

# RabbitMQ
RABBITMQ_USER=admin
RABBITMQ_PASSWORD=your_secure_password

# JWT
JWT_SECRET=your_very_long_and_secure_jwt_secret_key

# Ports
API_PORT=5000
WEB_PORT=3000
```

Then update docker-compose.yml to use these variables.

## ?? Troubleshooting

### Infrastructure Only Issues

**MongoDB Connection Failed:**
```bash
# Check MongoDB is running
docker ps | grep testservice-mongodb

# View logs
docker logs testservice-mongodb

# Restart
infrastructure\stop.bat && infrastructure\start.bat
```

### Full Stack Issues

**Build Failures:**
```bash
# Clean and rebuild
docker-compose -f infrastructure\docker-compose.yml build --no-cache

# Check build logs
docker-compose -f infrastructure\docker-compose.yml build
```

**API Not Starting:**
```bash
# Check API logs
docker logs testservice-api

# Check health
curl http://localhost:5000/health

# Restart API only
docker restart testservice-api
```

**Web UI Not Loading:**
```bash
# Check Web logs
docker logs testservice-web

# Verify nginx is running
docker exec testservice-web nginx -t

# Restart Web only
docker restart testservice-web
```

### Port Conflicts

```bash
# Check what's using ports
netstat -ano | findstr "5000"  # Windows
lsof -i :5000                   # macOS/Linux

# Change ports in docker-compose.yml if needed
```

## ?? CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Start Infrastructure
        run: ./infrastructure/start.sh
      
      - name: Run Tests
        run: dotnet test
      
      - name: Stop Infrastructure
        run: ./infrastructure/stop.sh
```

### Full Stack Integration Test

```yaml
  integration-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Build and Start Full Stack
        run: ./infrastructure/start-full.sh
      
      - name: Wait for Health
        run: |
          timeout 60 bash -c 'until curl -f http://localhost:5000/health; do sleep 1; done'
          timeout 60 bash -c 'until curl -f http://localhost:3000/health; do sleep 1; done'
      
      - name: Run E2E Tests
        run: npm run test:e2e
      
      - name: Stop Full Stack
        run: ./infrastructure/stop-full.sh
```

## ?? Additional Documentation

- **Infrastructure Setup:** [../documents/infrastructure/INFRASTRUCTURE_SETUP.md](../documents/infrastructure/INFRASTRUCTURE_SETUP.md)
- **Deployment Guide:** [../documents/deployment/DEPLOYMENT_GUIDE.md](../documents/deployment/DEPLOYMENT_GUIDE.md)
- **Quick Access:** [../documents/QUICK_ACCESS.md](../documents/QUICK_ACCESS.md)

---

**Last Updated:** 2025-01-07  
**Version:** 2.0 - Full Stack Support
