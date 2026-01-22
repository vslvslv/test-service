# Deployment Guide

This guide covers deploying the TestService application using GitHub Actions to various platforms.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Deployment Options](#deployment-options)
  - [Option 1: Kubernetes](#option-1-kubernetes)
  - [Option 2: Azure Container Apps](#option-2-azure-container-apps)
  - [Option 3: AWS ECS](#option-3-aws-ecs)
- [GitHub Actions Configuration](#github-actions-configuration)
- [Manual Deployment](#manual-deployment)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

### General Requirements
- GitHub repository with Actions enabled
- Docker images published to GitHub Container Registry (GHCR)
- Secrets configured in GitHub repository settings

### Platform-Specific Requirements

**Kubernetes:**
- Kubernetes cluster (v1.25+)
- `kubectl` configured locally
- Ingress controller installed (nginx recommended)
- Optional: cert-manager for SSL certificates

**Azure:**
- Azure subscription
- Azure CLI installed
- Container Apps environment created

**AWS:**
- AWS account
- ECS cluster created
- ECR repositories created

---

## Deployment Options

### Option 1: Kubernetes

#### Automatic Deployment via GitHub Actions

The application automatically deploys to Kubernetes when Docker images are pushed to GHCR.

**Setup Steps:**

1. **Configure Secrets in GitHub**
   
   Go to: `Settings → Secrets and variables → Actions → New repository secret`
   
   Add these secrets:
   ```
   KUBE_CONFIG          # Base64-encoded kubeconfig file
   MONGODB_PASSWORD     # MongoDB admin password
   RABBITMQ_PASSWORD    # RabbitMQ password
   ```

2. **Get kubeconfig (base64 encoded)**
   ```bash
   cat ~/.kube/config | base64 | pbcopy  # macOS
   cat ~/.kube/config | base64 -w 0     # Linux
   ```

3. **Update Kubernetes manifests**
   
   Edit [k8s/ingress.yaml](../k8s/ingress.yaml):
   ```yaml
   spec:
     tls:
     - hosts:
       - your-actual-domain.com  # Replace with your domain
   ```

4. **Trigger Deployment**
   ```bash
   git push origin main
   # Or manually: Actions → Deploy to Kubernetes → Run workflow
   ```

5. **Monitor Deployment**
   ```bash
   kubectl get pods -n testservice -w
   kubectl logs -f deployment/testservice-api -n testservice
   ```

#### Manual Kubernetes Deployment

```bash
# Make deploy script executable
chmod +x k8s/deploy.sh

# Run deployment script
./k8s/deploy.sh
```

Or deploy manually:

```bash
# 1. Update images in manifests
cd k8s
sed -i "s|YOUR_ORG|your-github-username|g" api.yaml web.yaml
sed -i "s|YOUR_REPO|your-repo-name|g" api.yaml web.yaml

# 2. Create namespace
kubectl apply -f namespace.yaml

# 3. Create secrets
kubectl create secret generic mongodb-secret \
  --from-literal=password="your-password" \
  --from-literal=connection-string="mongodb://admin:your-password@mongodb:27017/TestService?authSource=admin" \
  --namespace=testservice

kubectl create secret generic rabbitmq-secret \
  --from-literal=password="your-password" \
  --namespace=testservice

kubectl create secret docker-registry ghcr-secret \
  --docker-server=ghcr.io \
  --docker-username=your-github-username \
  --docker-password=your-github-token \
  --namespace=testservice

# 4. Deploy infrastructure
kubectl apply -f mongodb.yaml
kubectl apply -f rabbitmq.yaml

# 5. Wait for infrastructure
kubectl wait --for=condition=ready pod -l app=mongodb -n testservice --timeout=300s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n testservice --timeout=300s

# 6. Deploy application
kubectl apply -f api.yaml
kubectl apply -f web.yaml

# 7. Apply ingress
kubectl apply -f ingress.yaml

# 8. Check status
kubectl get all -n testservice
```

#### Access Application

```bash
# Get ingress address
kubectl get ingress -n testservice

# Access URLs:
# - Web: https://your-domain.com/testservice/ui
# - API: https://your-domain.com/testservice/api
# - Swagger: https://your-domain.com/testservice/api/swagger
```

---

### Option 2: Azure Container Apps

#### Setup Steps

1. **Create Azure Resources**
   ```bash
   # Login to Azure
   az login
   
   # Create resource group
   az group create --name testservice-rg --location eastus
   
   # Create Container Apps environment
   az containerapp env create \
     --name testservice-env \
     --resource-group testservice-rg \
     --location eastus
   
   # Create Log Analytics workspace (optional but recommended)
   az monitor log-analytics workspace create \
     --resource-group testservice-rg \
     --workspace-name testservice-logs
   ```

2. **Configure GitHub Secrets**
   
   Add these secrets:
   ```
   AZURE_CREDENTIALS       # Service principal JSON
   AZURE_RESOURCE_GROUP    # testservice-rg
   AZURE_CONTAINER_ENV     # testservice-env
   MONGODB_PASSWORD        # MongoDB password
   RABBITMQ_PASSWORD       # RabbitMQ password
   RABBITMQ_HOST           # RabbitMQ hostname
   RABBITMQ_USER           # RabbitMQ username
   ```

3. **Get Azure Credentials**
   ```bash
   az ad sp create-for-rbac \
     --name "testservice-github-actions" \
     --role contributor \
     --scopes /subscriptions/{subscription-id}/resourceGroups/testservice-rg \
     --sdk-auth
   ```
   Copy the entire JSON output to `AZURE_CREDENTIALS` secret.

4. **Deploy**
   
   Push to main branch or manually trigger:
   ```
   Actions → Deploy to Azure → Run workflow
   ```

#### Manual Azure Deployment

```bash
# Deploy API
az containerapp create \
  --name testservice-api \
  --resource-group testservice-rg \
  --environment testservice-env \
  --image ghcr.io/your-org/your-repo/api:latest \
  --target-port 80 \
  --ingress external \
  --registry-server ghcr.io \
  --registry-username your-github-username \
  --registry-password your-github-token \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    MongoDB__ConnectionString=secretref:mongodb-connection \
    RabbitMQ__HostName=your-rabbitmq-host

# Deploy Web
az containerapp create \
  --name testservice-web \
  --resource-group testservice-rg \
  --environment testservice-env \
  --image ghcr.io/your-org/your-repo/web:latest \
  --target-port 80 \
  --ingress external \
  --registry-server ghcr.io \
  --registry-username your-github-username \
  --registry-password your-github-token
```

---

### Option 3: AWS ECS

#### Setup Steps

1. **Create ECS Cluster**
   ```bash
   aws ecs create-cluster --cluster-name testservice-cluster
   ```

2. **Create ECR Repositories**
   ```bash
   aws ecr create-repository --repository-name testservice-api
   aws ecr create-repository --repository-name testservice-web
   ```

3. **Configure GitHub Secrets**
   ```
   AWS_ACCESS_KEY_ID          # AWS access key
   AWS_SECRET_ACCESS_KEY      # AWS secret key
   AWS_REGION                 # e.g., us-east-1
   ECS_CLUSTER                # testservice-cluster
   ```

4. **Create Task Definitions** (in AWS Console or CLI)

5. **Deploy**
   
   Push to main or manually trigger:
   ```
   Actions → Deploy to AWS ECS → Run workflow
   ```

---

## GitHub Actions Configuration

### Workflow Overview

```
┌─────────────────┐
│  Push to main   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Run Tests     │ (api-ci.yml, web-ci.yml)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Build Docker   │ (docker-build-and-push.yml)
│  Push to GHCR   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Deploy to K8s   │ (deploy-k8s.yml)
│ or Azure/AWS    │ (deploy-azure.yml, deploy-aws.yml)
└─────────────────┘
```

### Enable/Disable Workflows

To use specific deployment:

1. Keep only the deployment workflow you need
2. Or disable workflows in `.github/workflows/`:
   ```yaml
   # Add to top of workflow to disable:
   on:
     workflow_dispatch:  # Manual only
   ```

### Required Secrets by Platform

| Secret | Kubernetes | Azure | AWS |
|--------|-----------|-------|-----|
| `KUBE_CONFIG` | ✅ | ❌ | ❌ |
| `MONGODB_PASSWORD` | ✅ | ✅ | ✅ |
| `RABBITMQ_PASSWORD` | ✅ | ✅ | ✅ |
| `AZURE_CREDENTIALS` | ❌ | ✅ | ❌ |
| `AZURE_RESOURCE_GROUP` | ❌ | ✅ | ❌ |
| `AZURE_CONTAINER_ENV` | ❌ | ✅ | ❌ |
| `AWS_ACCESS_KEY_ID` | ❌ | ❌ | ✅ |
| `AWS_SECRET_ACCESS_KEY` | ❌ | ❌ | ✅ |
| `AWS_REGION` | ❌ | ❌ | ✅ |
| `ECS_CLUSTER` | ❌ | ❌ | ✅ |

---

## Manual Deployment

### Using Docker Compose (Local/VM)

```bash
# Pull latest images
docker pull ghcr.io/your-org/your-repo/api:latest
docker pull ghcr.io/your-org/your-repo/web:latest

# Update docker-compose.yml with image references
# Then run:
docker compose up -d
```

---

## Troubleshooting

### Common Issues

**1. Images not pulling from GHCR**

```bash
# Make sure package is public or create proper secret
kubectl get secret ghcr-secret -n testservice -o yaml

# Or make package public:
# GitHub → Package settings → Change visibility → Public
```

**2. Pods crashing**

```bash
# Check logs
kubectl logs -f deployment/testservice-api -n testservice

# Check events
kubectl get events -n testservice --sort-by='.lastTimestamp'

# Describe pod
kubectl describe pod <pod-name> -n testservice
```

**3. Database connection issues**

```bash
# Verify MongoDB is running
kubectl get pods -n testservice -l app=mongodb

# Check connection string secret
kubectl get secret mongodb-secret -n testservice -o jsonpath='{.data.connection-string}' | base64 -d
```

**4. Ingress not working**

```bash
# Check ingress controller is installed
kubectl get pods -n ingress-nginx

# Check ingress resource
kubectl describe ingress testservice-ingress -n testservice

# Check service endpoints
kubectl get endpoints -n testservice
```

**5. Health checks failing**

```bash
# Test API health endpoint
kubectl port-forward svc/api 8080:80 -n testservice
curl http://localhost:8080/health

# Check API logs for startup errors
kubectl logs deployment/testservice-api -n testservice --tail=100
```

### Rollback Deployment

**Kubernetes:**
```bash
# Rollback to previous version
kubectl rollout undo deployment/testservice-api -n testservice
kubectl rollout undo deployment/testservice-web -n testservice

# Check rollout history
kubectl rollout history deployment/testservice-api -n testservice
```

**Azure:**
```bash
# List revisions
az containerapp revision list \
  --name testservice-api \
  --resource-group testservice-rg

# Activate previous revision
az containerapp revision activate \
  --name testservice-api \
  --revision <revision-name> \
  --resource-group testservice-rg
```

### View Logs

**Kubernetes:**
```bash
# API logs
kubectl logs -f deployment/testservice-api -n testservice

# Web logs
kubectl logs -f deployment/testservice-web -n testservice

# All logs
kubectl logs -f -l app=testservice-api -n testservice --all-containers=true
```

**Azure:**
```bash
az containerapp logs show \
  --name testservice-api \
  --resource-group testservice-rg \
  --follow
```

---

## Monitoring

### Kubernetes Dashboard

```bash
# Install dashboard
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml

# Create admin user and get token
kubectl create serviceaccount dashboard-admin -n kubernetes-dashboard
kubectl create clusterrolebinding dashboard-admin \
  --clusterrole=cluster-admin \
  --serviceaccount=kubernetes-dashboard:dashboard-admin

# Get token
kubectl create token dashboard-admin -n kubernetes-dashboard

# Access dashboard
kubectl proxy
# Open: http://localhost:8001/api/v1/namespaces/kubernetes-dashboard/services/https:kubernetes-dashboard:/proxy/
```

### Prometheus & Grafana (Optional)

```bash
# Install using Helm
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus prometheus-community/kube-prometheus-stack -n monitoring --create-namespace
```

---

## Next Steps

1. ✅ Set up SSL certificates (cert-manager)
2. ✅ Configure monitoring and alerting
3. ✅ Set up backup strategy for MongoDB
4. ✅ Configure auto-scaling (HPA)
5. ✅ Implement blue-green deployments
6. ✅ Add performance testing

## Support

For issues or questions:
- Check GitHub Actions logs
- Review Kubernetes events: `kubectl get events -n testservice`
- Check application logs
- Create an issue in the repository
