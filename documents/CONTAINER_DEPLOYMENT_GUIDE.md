# Container Deployment Guide

This guide explains how to build, publish, and deploy the Test Service as containers.

## Table of Contents
- [Quick Start](#quick-start)
- [Build Scripts](#build-scripts)
- [Container Registries](#container-registries)
- [Deployment Options](#deployment-options)
- [Configuration](#configuration)
- [Monitoring](#monitoring)

## Quick Start

### Local Development

```bash
# Build and run all services locally
docker compose -f infrastructure/docker-compose.yml up -d

# View logs
docker compose -f infrastructure/docker-compose.yml logs -f

# Stop services
docker compose -f infrastructure/docker-compose.yml down
```

### Build for Production

```bash
# Using PowerShell
./build-and-publish.ps1 -Registry "docker.io/yourusername" -Tag "v1.0.0" -Push

# Using Bash
chmod +x build-and-publish.sh
./build-and-publish.sh -r docker.io/yourusername -t v1.0.0 -p
```

## Build Scripts

Two build scripts are provided for cross-platform compatibility:

### PowerShell Script (Windows/Linux/macOS)

```powershell
./build-and-publish.ps1 [OPTIONS]

Options:
  -Registry <string>     Container registry URL (e.g., docker.io/username)
  -Tag <string>         Image tag (default: latest)
  -Push                 Push images to registry after building
  -NoBuild              Skip building, only push existing images
  -Platform <string>    Target platform (default: linux/amd64,linux/arm64)
```

**Examples:**

```powershell
# Build locally only
./build-and-publish.ps1

# Build and push to Docker Hub
./build-and-publish.ps1 -Registry "docker.io/myuser" -Tag "v1.0.0" -Push

# Build and push to GitHub Container Registry
./build-and-publish.ps1 -Registry "ghcr.io/myorg" -Push

# Build for specific platform
./build-and-publish.ps1 -Platform "linux/amd64" -Tag "v1.0.0"

# Push existing images without rebuilding
./build-and-publish.ps1 -Registry "docker.io/myuser" -NoBuild -Push
```

### Bash Script (Linux/macOS)

```bash
./build-and-publish.sh [OPTIONS]

Options:
  -r, --registry REGISTRY   Container registry URL
  -t, --tag TAG            Image tag (default: latest)
  -p, --push               Push images to registry
  --no-build               Skip building, only push
  --platform PLATFORM      Target platform (default: linux/amd64,linux/arm64)
  -h, --help               Show help message
```

**Examples:**

```bash
# Build locally only
./build-and-publish.sh

# Build and push to Docker Hub
./build-and-publish.sh -r docker.io/myuser -t v1.0.0 -p

# Build and push to GitHub Container Registry
./build-and-publish.sh -r ghcr.io/myorg -p

# Build for specific platform
./build-and-publish.sh --platform linux/amd64 -t v1.0.0

# Push existing images without rebuilding
./build-and-publish.sh -r docker.io/myuser --no-build -p
```

## Container Registries

### Docker Hub

1. **Login:**
   ```bash
   docker login
   ```

2. **Build and push:**
   ```bash
   ./build-and-publish.ps1 -Registry "docker.io/yourusername" -Tag "v1.0.0" -Push
   ```

3. **Pull and run:**
   ```bash
   docker pull docker.io/yourusername/testservice-api:v1.0.0
   docker pull docker.io/yourusername/testservice-web:v1.0.0
   ```

### GitHub Container Registry (ghcr.io)

1. **Create Personal Access Token (PAT):**
   - Go to GitHub Settings ? Developer settings ? Personal access tokens
   - Generate new token with `write:packages` scope

2. **Login:**
   ```bash
   echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin
   ```

3. **Build and push:**
   ```bash
   ./build-and-publish.ps1 -Registry "ghcr.io/yourusername" -Tag "v1.0.0" -Push
   ```

4. **Make images public** (optional):
   - Go to package settings on GitHub
   - Change visibility to public

### Azure Container Registry (ACR)

1. **Login:**
   ```bash
   az acr login --name yourregistryname
   ```

2. **Build and push:**
   ```bash
   ./build-and-publish.ps1 -Registry "yourregistryname.azurecr.io" -Tag "v1.0.0" -Push
   ```

### AWS Elastic Container Registry (ECR)

1. **Login:**
   ```bash
   aws ecr get-login-password --region region | docker login --username AWS --password-stdin aws_account_id.dkr.ecr.region.amazonaws.com
   ```

2. **Build and push:**
   ```bash
   ./build-and-publish.ps1 -Registry "aws_account_id.dkr.ecr.region.amazonaws.com" -Tag "v1.0.0" -Push
   ```

## Deployment Options

### Option 1: Docker Compose (Recommended for Single Server)

Create a production `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  mongodb:
    image: mongo:latest
    restart: always
    environment:
      MONGO_INITDB_ROOT_USERNAME: ${MONGO_USER:-admin}
      MONGO_INITDB_ROOT_PASSWORD: ${MONGO_PASSWORD}
      MONGO_INITDB_DATABASE: TestServiceDb
    volumes:
      - mongodb_data:/data/db
    networks:
      - testservice-network

  rabbitmq:
    image: rabbitmq:3-management
    restart: always
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER:-guest}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    networks:
      - testservice-network

  api:
    image: yourusername/testservice-api:v1.0.0
    restart: always
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - MongoDbSettings__ConnectionString=mongodb://${MONGO_USER:-admin}:${MONGO_PASSWORD}@mongodb:27017/TestServiceDb?authSource=admin
      - RabbitMqSettings__HostName=rabbitmq
      - RabbitMqSettings__UserName=${RABBITMQ_USER:-guest}
      - RabbitMqSettings__Password=${RABBITMQ_PASSWORD}
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
    depends_on:
      - mongodb
      - rabbitmq
    networks:
      - testservice-network

  web:
    image: yourusername/testservice-web:v1.0.0
    restart: always
    ports:
      - "3000:80"
    environment:
      - VITE_API_BASE_URL=http://your-server-ip:5000
    depends_on:
      - api
    networks:
      - testservice-network

volumes:
  mongodb_data:
  rabbitmq_data:

networks:
  testservice-network:
    driver: bridge
```

Create `.env` file:

```env
MONGO_USER=admin
MONGO_PASSWORD=your-secure-password
RABBITMQ_USER=testservice
RABBITMQ_PASSWORD=your-rabbitmq-password
JWT_SECRET_KEY=your-super-secret-jwt-key-at-least-32-characters-long
```

Deploy:

```bash
docker compose -f docker-compose.prod.yml up -d
```

### Option 2: Kubernetes

Create Kubernetes manifests:

**api-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: testservice-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: testservice-api
  template:
    metadata:
      labels:
        app: testservice-api
    spec:
      containers:
      - name: api
        image: yourusername/testservice-api:v1.0.0
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: MongoDbSettings__ConnectionString
          valueFrom:
            secretKeyRef:
              name: testservice-secrets
              key: mongodb-connection-string
        - name: RabbitMqSettings__HostName
          value: "rabbitmq-service"
        - name: JwtSettings__SecretKey
          valueFrom:
            secretKeyRef:
              name: testservice-secrets
              key: jwt-secret-key
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: testservice-api
spec:
  selector:
    app: testservice-api
  ports:
  - port: 80
    targetPort: 80
  type: LoadBalancer
```

**web-deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: testservice-web
spec:
  replicas: 2
  selector:
    matchLabels:
      app: testservice-web
  template:
    metadata:
      labels:
        app: testservice-web
    spec:
      containers:
      - name: web
        image: yourusername/testservice-web:v1.0.0
        ports:
        - containerPort: 80
        env:
        - name: VITE_API_BASE_URL
          value: "http://testservice-api"
---
apiVersion: v1
kind: Service
metadata:
  name: testservice-web
spec:
  selector:
    app: testservice-web
  ports:
  - port: 80
    targetPort: 80
  type: LoadBalancer
```

Deploy:

```bash
kubectl apply -f api-deployment.yaml
kubectl apply -f web-deployment.yaml
```

### Option 3: Cloud Services

#### Azure Container Instances

```bash
# API
az container create \
  --resource-group myResourceGroup \
  --name testservice-api \
  --image yourusername/testservice-api:v1.0.0 \
  --dns-name-label testservice-api \
  --ports 80 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    MongoDbSettings__ConnectionString="your-connection-string"

# Web
az container create \
  --resource-group myResourceGroup \
  --name testservice-web \
  --image yourusername/testservice-web:v1.0.0 \
  --dns-name-label testservice-web \
  --ports 80 \
  --environment-variables \
    VITE_API_BASE_URL="http://testservice-api.region.azurecontainer.io"
```

#### AWS ECS/Fargate

Use AWS Console or CLI to create ECS services with the container images.

#### Google Cloud Run

```bash
# Deploy API
gcloud run deploy testservice-api \
  --image yourusername/testservice-api:v1.0.0 \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated

# Deploy Web
gcloud run deploy testservice-web \
  --image yourusername/testservice-web:v1.0.0 \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated
```

## Configuration

### Environment Variables

#### API Service

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Production` |
| `MongoDbSettings__ConnectionString` | MongoDB connection string | Required |
| `MongoDbSettings__DatabaseName` | Database name | `TestServiceDb` |
| `RabbitMqSettings__HostName` | RabbitMQ host | Required |
| `RabbitMqSettings__Port` | RabbitMQ port | `5672` |
| `RabbitMqSettings__UserName` | RabbitMQ username | `guest` |
| `RabbitMqSettings__Password` | RabbitMQ password | Required |
| `JwtSettings__SecretKey` | JWT signing key | Required |
| `JwtSettings__Issuer` | JWT issuer | `TestServiceApi` |
| `JwtSettings__Audience` | JWT audience | `TestServiceClients` |
| `JwtSettings__ExpirationMinutes` | Token expiration | `60` |

#### Web Service

| Variable | Description | Default |
|----------|-------------|---------|
| `VITE_API_BASE_URL` | API base URL | Required |

### Secrets Management

**Docker Compose with secrets:**

```yaml
services:
  api:
    secrets:
      - mongo_password
      - jwt_secret

secrets:
  mongo_password:
    file: ./secrets/mongo_password.txt
  jwt_secret:
    file: ./secrets/jwt_secret.txt
```

**Kubernetes secrets:**

```bash
kubectl create secret generic testservice-secrets \
  --from-literal=mongodb-connection-string="mongodb://..." \
  --from-literal=jwt-secret-key="your-secret-key"
```

## Monitoring

### Health Checks

Both services expose health check endpoints:

- **API:** `http://api:80/health`
- **Web:** `http://web:80/health`

### Logging

View container logs:

```bash
# Docker Compose
docker compose logs -f api
docker compose logs -f web

# Docker standalone
docker logs -f testservice-api
docker logs -f testservice-web

# Kubernetes
kubectl logs -f deployment/testservice-api
kubectl logs -f deployment/testservice-web
```

### Metrics

The API exposes metrics at:
- Swagger UI: `http://api/swagger`
- RabbitMQ Management: `http://rabbitmq:15672`

## Troubleshooting

### Container won't start

```bash
# Check logs
docker logs testservice-api

# Check environment variables
docker inspect testservice-api

# Access container shell
docker exec -it testservice-api /bin/sh
```

### Network issues

```bash
# Check networks
docker network ls

# Inspect network
docker network inspect testservice-network

# Test connectivity
docker exec -it testservice-api ping mongodb
```

### Database connection issues

```bash
# Check MongoDB
docker logs testservice-mongodb

# Test connection
docker exec -it testservice-mongodb mongosh -u admin -p password123
```

### Image size optimization

The current Dockerfiles use multi-stage builds for optimized image sizes:
- **API Image:** ~210 MB
- **Web Image:** ~45 MB

## CI/CD Integration

### GitHub Actions

Create `.github/workflows/docker-publish.yml`:

```yaml
name: Build and Push Docker Images

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v2
      
      - name: Login to GitHub Container Registry
        uses: docker/login-action@v2
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      
      - name: Build and push API
        uses: docker/build-push-action@v4
        with:
          context: .
          file: ./TestService.Api/Dockerfile
          push: true
          tags: ghcr.io/${{ github.repository }}/testservice-api:${{ github.ref_name }}
      
      - name: Build and push Web
        uses: docker/build-push-action@v4
        with:
          context: ./testservice-web
          file: ./testservice-web/Dockerfile
          push: true
          tags: ghcr.io/${{ github.repository }}/testservice-web:${{ github.ref_name }}
```

### Azure DevOps

Create `azure-pipelines.yml`:

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

variables:
  containerRegistry: 'yourregistry.azurecr.io'

steps:
- task: Docker@2
  inputs:
    command: buildAndPush
    repository: 'testservice-api'
    dockerfile: 'TestService.Api/Dockerfile'
    containerRegistry: $(containerRegistry)
    tags: $(Build.BuildNumber)

- task: Docker@2
  inputs:
    command: buildAndPush
    repository: 'testservice-web'
    dockerfile: 'testservice-web/Dockerfile'
    containerRegistry: $(containerRegistry)
    tags: $(Build.BuildNumber)
```

## Security Best Practices

1. **Use specific base image tags** (not `latest`)
2. **Scan images for vulnerabilities:**
   ```bash
   docker scan yourusername/testservice-api:v1.0.0
   ```
3. **Run containers as non-root user**
4. **Use secrets management** (not environment variables for sensitive data)
5. **Enable TLS/HTTPS** in production
6. **Implement network policies** in Kubernetes
7. **Regular updates** of base images and dependencies

## Performance Tuning

### Resource Limits

**Docker Compose:**

```yaml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '0.5'
          memory: 512M
```

**Kubernetes:**

```yaml
resources:
  requests:
    memory: "512Mi"
    cpu: "500m"
  limits:
    memory: "2Gi"
    cpu: "2000m"
```

### Scaling

```bash
# Docker Compose
docker compose up --scale api=3

# Kubernetes
kubectl scale deployment testservice-api --replicas=5
```

---

**Next Steps:**
1. Review and customize the docker-compose.prod.yml
2. Set up your container registry
3. Configure secrets and environment variables
4. Run the build script
5. Deploy to your environment
6. Set up monitoring and logging
