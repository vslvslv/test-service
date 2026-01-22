# Quick Reference: Deployment Commands

## GitHub Actions (Recommended)

### Automatic Deployment
```bash
# Simply push to main branch
git push origin main

# Or create a release tag
git tag v1.0.0
git push origin v1.0.0
```

### Manual Deployment Trigger
```bash
# Via GitHub UI:
# 1. Go to Actions tab
# 2. Select "Deploy to Kubernetes" (or Azure/AWS)
# 3. Click "Run workflow"
# 4. Select environment (production/staging)
# 5. Click "Run workflow"

# Via GitHub CLI:
gh workflow run deploy-k8s.yml -f environment=production
```

---

## Kubernetes

### Quick Deploy
```bash
chmod +x k8s/deploy.sh
./k8s/deploy.sh
```

### Manual Steps
```bash
# 1. Create secrets
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

# 2. Deploy all
kubectl apply -f k8s/

# 3. Check status
kubectl get all -n testservice
```

### Update Deployment
```bash
# Roll out new version
kubectl set image deployment/testservice-api \
  api=ghcr.io/your-org/your-repo/api:new-tag \
  -n testservice

kubectl set image deployment/testservice-web \
  web=ghcr.io/your-org/your-repo/web:new-tag \
  -n testservice

# Check rollout status
kubectl rollout status deployment/testservice-api -n testservice
kubectl rollout status deployment/testservice-web -n testservice
```

### Rollback
```bash
kubectl rollout undo deployment/testservice-api -n testservice
kubectl rollout undo deployment/testservice-web -n testservice
```

### View Logs
```bash
# API logs
kubectl logs -f deployment/testservice-api -n testservice

# Web logs
kubectl logs -f deployment/testservice-web -n testservice

# All containers
kubectl logs -f -l app=testservice-api -n testservice --all-containers
```

### Port Forward (for testing)
```bash
# API
kubectl port-forward svc/api 8080:80 -n testservice
# Access: http://localhost:8080

# Web
kubectl port-forward svc/web 3000:80 -n testservice
# Access: http://localhost:3000
```

---

## Azure Container Apps

### Deploy
```bash
# Login
az login

# Deploy API
az containerapp up \
  --name testservice-api \
  --resource-group testservice-rg \
  --environment testservice-env \
  --image ghcr.io/your-org/your-repo/api:latest \
  --target-port 80 \
  --ingress external

# Deploy Web
az containerapp up \
  --name testservice-web \
  --resource-group testservice-rg \
  --environment testservice-env \
  --image ghcr.io/your-org/your-repo/web:latest \
  --target-port 80 \
  --ingress external
```

### View Logs
```bash
az containerapp logs show \
  --name testservice-api \
  --resource-group testservice-rg \
  --follow
```

### Get URLs
```bash
az containerapp show \
  --name testservice-api \
  --resource-group testservice-rg \
  --query properties.configuration.ingress.fqdn
```

---

## Docker Compose (Local)

### Pull and Run
```bash
# Pull latest images
docker pull ghcr.io/your-org/your-repo/api:latest
docker pull ghcr.io/your-org/your-repo/web:latest

# Run with docker-compose
docker compose -f infrastructure/docker-compose.yml up -d

# View logs
docker compose -f infrastructure/docker-compose.yml logs -f

# Stop
docker compose -f infrastructure/docker-compose.yml down
```

---

## Health Checks

### API Health
```bash
curl http://your-domain.com/testservice/api/health
# or
curl http://localhost:5000/health
```

### Check All Services
```bash
# Kubernetes
kubectl get pods -n testservice
kubectl get svc -n testservice
kubectl get ingress -n testservice

# Docker
docker compose ps
docker compose logs
```

---

## Secrets Management

### Create GitHub Secrets
```bash
# Via GitHub CLI
gh secret set KUBE_CONFIG < ~/.kube/config
gh secret set MONGODB_PASSWORD --body "your-password"
gh secret set RABBITMQ_PASSWORD --body "your-password"

# Via GitHub UI
# Settings → Secrets and variables → Actions → New repository secret
```

### Encode kubeconfig for GitHub
```bash
# macOS
cat ~/.kube/config | base64 | pbcopy

# Linux
cat ~/.kube/config | base64 -w 0

# Paste the output as KUBE_CONFIG secret in GitHub
```

---

## Troubleshooting

### Check Deployment Status
```bash
# GitHub Actions
# Go to: Repository → Actions → Latest workflow run

# Kubernetes
kubectl describe deployment testservice-api -n testservice
kubectl get events -n testservice --sort-by='.lastTimestamp'

# Docker
docker compose logs api
docker compose logs web
```

### Restart Services
```bash
# Kubernetes
kubectl rollout restart deployment/testservice-api -n testservice
kubectl rollout restart deployment/testservice-web -n testservice

# Docker
docker compose restart api
docker compose restart web
```

### Delete and Redeploy
```bash
# Kubernetes
kubectl delete namespace testservice
./k8s/deploy.sh

# Docker
docker compose down -v
docker compose up -d
```

---

## URLs After Deployment

### Kubernetes (with Ingress)
- Web UI: `https://your-domain.com/testservice/ui`
- API: `https://your-domain.com/testservice/api`
- Swagger: `https://your-domain.com/testservice/api/swagger`
- RabbitMQ: `https://your-domain.com/rabbitmq` (if exposed)

### Local Docker
- Web UI: `http://localhost:3000`
- API: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`
- RabbitMQ: `http://localhost:15672`

### Azure Container Apps
- Get URLs: `az containerapp list --resource-group testservice-rg --query "[].{name:name, url:properties.configuration.ingress.fqdn}"`

---

## Monitoring

### Watch Deployments
```bash
# Kubernetes - watch pods
kubectl get pods -n testservice -w

# Kubernetes - watch events
kubectl get events -n testservice -w

# Docker - follow logs
docker compose logs -f
```

### Resource Usage
```bash
# Kubernetes
kubectl top pods -n testservice
kubectl top nodes

# Docker
docker stats
```

---

## Complete CI/CD Flow

```
1. Make changes to code
   ↓
2. Commit and push to GitHub
   git add .
   git commit -m "Your changes"
   git push origin main
   ↓
3. GitHub Actions automatically:
   - Runs tests
   - Builds Docker images
   - Pushes to GHCR
   - Deploys to your platform
   ↓
4. Monitor deployment
   - GitHub Actions: Repository → Actions
   - Kubernetes: kubectl get pods -n testservice -w
   - Azure: az containerapp logs show ...
   ↓
5. Verify application
   - Access Web UI
   - Check API health endpoint
   - Test functionality
```

---

For detailed documentation, see [DEPLOYMENT_GUIDE.md](.github/DEPLOYMENT_GUIDE.md)
