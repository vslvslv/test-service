# Deployment Guide

## Table of Contents
- [Docker Deployment](#docker-deployment)
- [Kubernetes Deployment](#kubernetes-deployment)
- [Azure Deployment](#azure-deployment)
- [AWS Deployment](#aws-deployment)
- [CI/CD Pipeline](#cicd-pipeline)

---

## Docker Deployment

### Prerequisites
- Docker 20.10+
- Docker Compose 2.0+

### Build Images

**API:**
```bash
docker build -t testservice-api:latest -f Dockerfile .
```

**Web:**
```bash
cd testservice-web
docker build -t testservice-web:latest -f Dockerfile .
```

### Run with Docker Compose

```bash
# Start services
docker-compose -f docker-compose.prod.yml up -d

# View logs
docker-compose -f docker-compose.prod.yml logs -f

# Stop services
docker-compose -f docker-compose.prod.yml down
```

### Environment Configuration

Create `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  mongodb:
    image: mongo:latest
    environment:
      MONGO_INITDB_ROOT_USERNAME: admin
      MONGO_INITDB_ROOT_PASSWORD: ${MONGO_PASSWORD}
    volumes:
      - mongodb_data:/data/db
    networks:
      - backend

  rabbitmq:
    image: rabbitmq:3-management
    environment:
      RABBITMQ_DEFAULT_USER: admin
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    networks:
      - backend

  api:
    image: testservice-api:latest
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      MongoDB__ConnectionString: mongodb://admin:${MONGO_PASSWORD}@mongodb:27017/TestServiceDb?authSource=admin
      RabbitMQ__HostName: rabbitmq
      Jwt__Secret: ${JWT_SECRET}
    depends_on:
      - mongodb
      - rabbitmq
    networks:
      - backend
      - frontend
    restart: always

  web:
    image: testservice-web:latest
    environment:
      VITE_API_BASE_URL: http://api
    depends_on:
      - api
    networks:
      - frontend
    restart: always

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./certs:/etc/nginx/certs:ro
    depends_on:
      - web
      - api
    networks:
      - frontend
    restart: always

volumes:
  mongodb_data:
  rabbitmq_data:

networks:
  backend:
  frontend:
```

---

## Kubernetes Deployment

### Prerequisites
- Kubernetes 1.24+
- kubectl configured
- Helm 3+ (optional)

### Create Namespace

```bash
kubectl create namespace testservice
```

### Deploy MongoDB

```yaml
# mongodb-deployment.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: mongodb
  namespace: testservice
spec:
  serviceName: mongodb
  replicas: 1
  selector:
    matchLabels:
      app: mongodb
  template:
    metadata:
      labels:
        app: mongodb
    spec:
      containers:
      - name: mongodb
        image: mongo:latest
        ports:
        - containerPort: 27017
        env:
        - name: MONGO_INITDB_ROOT_USERNAME
          valueFrom:
            secretKeyRef:
              name: mongodb-secret
              key: username
        - name: MONGO_INITDB_ROOT_PASSWORD
          valueFrom:
            secretKeyRef:
              name: mongodb-secret
              key: password
        volumeMounts:
        - name: mongodb-data
          mountPath: /data/db
  volumeClaimTemplates:
  - metadata:
      name: mongodb-data
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 10Gi
---
apiVersion: v1
kind: Service
metadata:
  name: mongodb
  namespace: testservice
spec:
  ports:
  - port: 27017
  selector:
    app: mongodb
  clusterIP: None
```

### Deploy RabbitMQ

```yaml
# rabbitmq-deployment.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: rabbitmq
  namespace: testservice
spec:
  serviceName: rabbitmq
  replicas: 1
  selector:
    matchLabels:
      app: rabbitmq
  template:
    metadata:
      labels:
        app: rabbitmq
    spec:
      containers:
      - name: rabbitmq
        image: rabbitmq:3-management
        ports:
        - containerPort: 5672
        - containerPort: 15672
        env:
        - name: RABBITMQ_DEFAULT_USER
          valueFrom:
            secretKeyRef:
              name: rabbitmq-secret
              key: username
        - name: RABBITMQ_DEFAULT_PASS
          valueFrom:
            secretKeyRef:
              name: rabbitmq-secret
              key: password
        volumeMounts:
        - name: rabbitmq-data
          mountPath: /var/lib/rabbitmq
  volumeClaimTemplates:
  - metadata:
      name: rabbitmq-data
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 5Gi
---
apiVersion: v1
kind: Service
metadata:
  name: rabbitmq
  namespace: testservice
spec:
  ports:
  - name: amqp
    port: 5672
  - name: management
    port: 15672
  selector:
    app: rabbitmq
```

### Deploy API

```yaml
# api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: testservice-api
  namespace: testservice
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
        image: testservice-api:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: MongoDB__ConnectionString
          valueFrom:
            secretKeyRef:
              name: api-secrets
              key: mongodb-connection
        - name: RabbitMQ__HostName
          value: "rabbitmq"
        - name: Jwt__Secret
          valueFrom:
            secretKeyRef:
              name: api-secrets
              key: jwt-secret
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
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
  namespace: testservice
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 80
  selector:
    app: testservice-api
```

### Deploy Web

```yaml
# web-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: testservice-web
  namespace: testservice
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
        image: testservice-web:latest
        ports:
        - containerPort: 80
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
---
apiVersion: v1
kind: Service
metadata:
  name: testservice-web
  namespace: testservice
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 80
  selector:
    app: testservice-web
```

### Create Ingress

```yaml
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: testservice-ingress
  namespace: testservice
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/rewrite-target: /
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - testservice.example.com
    secretName: testservice-tls
  rules:
  - host: testservice.example.com
    http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: testservice-api
            port:
              number: 80
      - path: /
        pathType: Prefix
        backend:
          service:
            name: testservice-web
            port:
              number: 80
```

### Create Secrets

```bash
# MongoDB secret
kubectl create secret generic mongodb-secret \
  --from-literal=username=admin \
  --from-literal=password=your_secure_password \
  -n testservice

# RabbitMQ secret
kubectl create secret generic rabbitmq-secret \
  --from-literal=username=admin \
  --from-literal=password=your_secure_password \
  -n testservice

# API secrets
kubectl create secret generic api-secrets \
  --from-literal=mongodb-connection="mongodb://admin:password@mongodb:27017/TestServiceDb?authSource=admin" \
  --from-literal=jwt-secret="your_very_long_jwt_secret" \
  -n testservice
```

### Deploy All Resources

```bash
kubectl apply -f mongodb-deployment.yaml
kubectl apply -f rabbitmq-deployment.yaml
kubectl apply -f api-deployment.yaml
kubectl apply -f web-deployment.yaml
kubectl apply -f ingress.yaml
```

### Verify Deployment

```bash
# Check pods
kubectl get pods -n testservice

# Check services
kubectl get svc -n testservice

# Check ingress
kubectl get ingress -n testservice

# View logs
kubectl logs -f deployment/testservice-api -n testservice
```

---

## Azure Deployment

### Prerequisites
- Azure CLI installed
- Azure subscription
- Resource group created

### Deploy to Azure App Service

```bash
# Login to Azure
az login

# Create resource group
az group create --name testservice-rg --location eastus

# Create App Service plan
az appservice plan create \
  --name testservice-plan \
  --resource-group testservice-rg \
  --sku B1 \
  --is-linux

# Create Web App for API
az webapp create \
  --resource-group testservice-rg \
  --plan testservice-plan \
  --name testservice-api \
  --deployment-container-image-name testservice-api:latest

# Configure app settings
az webapp config appsettings set \
  --resource-group testservice-rg \
  --name testservice-api \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    MongoDB__ConnectionString="your_connection_string" \
    Jwt__Secret="your_jwt_secret"

# Create Web App for Frontend
az webapp create \
  --resource-group testservice-rg \
  --plan testservice-plan \
  --name testservice-web \
  --deployment-container-image-name testservice-web:latest
```

### Deploy to Azure Container Instances

```bash
# Deploy API
az container create \
  --resource-group testservice-rg \
  --name testservice-api \
  --image testservice-api:latest \
  --cpu 1 \
  --memory 1 \
  --ports 80 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    MongoDB__ConnectionString="your_connection_string"

# Deploy Web
az container create \
  --resource-group testservice-rg \
  --name testservice-web \
  --image testservice-web:latest \
  --cpu 1 \
  --memory 0.5 \
  --ports 80
```

---

## AWS Deployment

### Deploy to AWS ECS

```bash
# Create ECS cluster
aws ecs create-cluster --cluster-name testservice-cluster

# Register task definition
aws ecs register-task-definition --cli-input-json file://task-definition.json

# Create service
aws ecs create-service \
  --cluster testservice-cluster \
  --service-name testservice-api \
  --task-definition testservice-api:1 \
  --desired-count 2 \
  --launch-type FARGATE
```

---

## CI/CD Pipeline

### GitHub Actions

```yaml
# .github/workflows/deploy.yml
name: Deploy to Production

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Build API Image
      run: docker build -t testservice-api:${{ github.sha }} .
    
    - name: Build Web Image
      run: |
        cd testservice-web
        docker build -t testservice-web:${{ github.sha }} .
    
    - name: Push to Registry
      run: |
        echo ${{ secrets.DOCKER_PASSWORD }} | docker login -u ${{ secrets.DOCKER_USERNAME }} --password-stdin
        docker push testservice-api:${{ github.sha }}
        docker push testservice-web:${{ github.sha }}
    
    - name: Deploy to Kubernetes
      run: |
        kubectl set image deployment/testservice-api api=testservice-api:${{ github.sha }} -n testservice
        kubectl set image deployment/testservice-web web=testservice-web:${{ github.sha }} -n testservice
```

### Azure DevOps Pipeline

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
    - main

pool:
  vmImage: 'ubuntu-latest'

stages:
- stage: Build
  jobs:
  - job: BuildImages
    steps:
    - task: Docker@2
      inputs:
        command: 'buildAndPush'
        repository: 'testservice-api'
        dockerfile: 'Dockerfile'
        tags: '$(Build.BuildId)'

- stage: Deploy
  jobs:
  - job: DeployToKubernetes
    steps:
    - task: Kubernetes@1
      inputs:
        command: 'apply'
        manifests: 'k8s/*.yaml'
```

---

## Post-Deployment

### Verify Services

```bash
# Check API health
curl https://your-domain.com/api/health

# Check Web
curl https://your-domain.com

# Test API endpoints
curl https://your-domain.com/api/schemas
```

### Monitor Logs

```bash
# Kubernetes
kubectl logs -f deployment/testservice-api -n testservice

# Docker
docker logs -f testservice-api

# Azure
az webapp log tail --name testservice-api --resource-group testservice-rg
```

### Set Up Monitoring

1. Configure Application Insights (Azure)
2. Set up CloudWatch (AWS)
3. Install Prometheus + Grafana (Kubernetes)

---

**For infrastructure setup, see:** [INFRASTRUCTURE_SETUP.md](../infrastructure/INFRASTRUCTURE_SETUP.md)
