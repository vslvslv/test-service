# Container Quick Reference

Quick commands for building and deploying Test Service containers.

## ?? Build Commands

### Local Build (Single Platform)
```powershell
# PowerShell
./build-and-publish.ps1

# Bash
./build-and-publish.sh
```

### Build Multi-Platform
```powershell
./build-and-publish.ps1 -Platform "linux/amd64,linux/arm64"
```

### Build with Custom Tag
```powershell
./build-and-publish.ps1 -Tag "v1.0.0"
```

## ?? Publish Commands

### Docker Hub
```powershell
# Login
docker login

# Build and push
./build-and-publish.ps1 -Registry "docker.io/yourusername" -Tag "v1.0.0" -Push
```

### GitHub Container Registry
```powershell
# Login
docker login ghcr.io -u yourusername

# Build and push
./build-and-publish.ps1 -Registry "ghcr.io/yourorg" -Tag "v1.0.0" -Push
```

### Azure Container Registry
```powershell
# Login
az acr login --name yourregistry

# Build and push
./build-and-publish.ps1 -Registry "yourregistry.azurecr.io" -Tag "v1.0.0" -Push
```

## ?? Run Commands

### Start All Services
```bash
docker compose -f infrastructure/docker-compose.yml up -d
```

### View Logs
```bash
# All services
docker compose -f infrastructure/docker-compose.yml logs -f

# Specific service
docker logs -f testservice-api
docker logs -f testservice-web
```

### Stop All Services
```bash
docker compose -f infrastructure/docker-compose.yml down
```

### Restart Service
```bash
docker compose -f infrastructure/docker-compose.yml restart api
docker compose -f infrastructure/docker-compose.yml restart web
```

## ?? Inspect Commands

### List Images
```bash
docker images | grep testservice
```

### List Running Containers
```bash
docker ps --filter "name=testservice"
```

### Check Container Health
```bash
docker inspect testservice-api --format='{{.State.Health.Status}}'
docker inspect testservice-web --format='{{.State.Health.Status}}'
```

### View Container Stats
```bash
docker stats testservice-api testservice-web
```

## ?? Cleanup Commands

### Stop and Remove Containers
```bash
docker compose -f infrastructure/docker-compose.yml down
```

### Remove Containers and Volumes
```bash
docker compose -f infrastructure/docker-compose.yml down -v
```

### Remove Images
```bash
docker rmi testservice-api:latest
docker rmi testservice-web:latest
```

### Prune Unused Resources
```bash
docker system prune -a
```

## ?? Access URLs

| Service | URL | Credentials |
|---------|-----|-------------|
| Web UI | http://localhost:3000 | (See Login page) |
| API | http://localhost:5000 | (JWT Token) |
| Swagger | http://localhost:5000/swagger | - |
| RabbitMQ Management | http://localhost:15672 | guest/guest |

## ?? Verify Deployment

### Quick Health Check
```bash
# API
curl http://localhost:5000/health

# Web
curl http://localhost:3000/health
```

### Test API
```bash
# Get schemas
curl http://localhost:5000/api/schemas

# Login (if configured)
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}'
```

## ?? Update Workflow

### 1. Make Changes
Edit code files as needed

### 2. Rebuild
```powershell
./build-and-publish.ps1
```

### 3. Restart
```bash
docker compose -f infrastructure/docker-compose.yml up -d --force-recreate
```

## ?? Production Deployment

### 1. Setup Environment
```bash
cp infrastructure/.env.example infrastructure/.env
# Edit .env with production values
```

### 2. Deploy
```bash
docker compose -f infrastructure/docker-compose.prod.yml up -d
```

### 3. Verify
```bash
docker ps
docker logs testservice-api
docker logs testservice-web
```

## ?? Troubleshooting

### Port Already in Use
```bash
# Find process using port
netstat -ano | findstr :5000
netstat -ano | findstr :3000

# Stop the process or change port in docker-compose.yml
```

### Container Won't Start
```bash
# Check logs
docker logs testservice-api

# Check events
docker events --filter container=testservice-api
```

### Database Connection Issues
```bash
# Check MongoDB
docker logs testservice-mongodb

# Test connection
docker exec -it testservice-mongodb mongosh -u admin -p password123
```

### RabbitMQ Connection Issues
```bash
# Check RabbitMQ
docker logs testservice-rabbitmq

# Check management UI
# http://localhost:15672
```

## ?? Related Documentation

- [Container Deployment Guide](./CONTAINER_DEPLOYMENT_GUIDE.md)
- [Container Build Success](./CONTAINER_BUILD_SUCCESS.md)
- [Docker Compose Dev](../infrastructure/docker-compose.yml)
- [Docker Compose Prod](../infrastructure/docker-compose.prod.yml)

---

**Pro Tip:** Add aliases to your shell profile for frequently used commands!

```bash
# ~/.bashrc or ~/.zshrc
alias dcup='docker compose -f infrastructure/docker-compose.yml up -d'
alias dcdown='docker compose -f infrastructure/docker-compose.yml down'
alias dclogs='docker compose -f infrastructure/docker-compose.yml logs -f'
alias dcrestart='docker compose -f infrastructure/docker-compose.yml restart'
```
