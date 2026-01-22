# GitHub Actions Setup Guide

## Overview

This project includes comprehensive GitHub Actions workflows for:
- ✅ **Continuous Integration (CI)** - Building and testing
- 🔒 **Security Scanning** - Vulnerability detection
- 🐳 **Docker Building** - Container image creation and push
- 📦 **Release Management** - Version releases and changelog generation
- 🔍 **Code Quality** - Code scanning with CodeQL

## Workflows

### 1. API CI (`api-ci.yml`)
**Triggers:** Push/PR on `TestService.Api/` or `TestService.Tests/`

**Steps:**
- Setup .NET 10
- Restore and build
- Run unit tests
- Security scanning with Trivy

### 2. Web App CI (`web-ci.yml`)
**Triggers:** Push/PR on `testservice-web/`

**Steps:**
- Setup Node.js 20
- Install dependencies
- Run ESLint
- Build application
- Security scanning with Trivy
- Upload build artifacts

### 3. Docker Build and Push (`docker-build-and-push.yml`)
**Triggers:** Push to `main` branch or manual trigger

**Features:**
- Builds both API and Web Docker images
- Pushes to GitHub Container Registry (GHCR)
- Automatic tagging:
  - `latest` for main branch
  - Git commit SHA
  - Git branch name
- Docker layer caching for faster builds

**Images:**
- `ghcr.io/YOUR_ORG/your-repo/api:latest`
- `ghcr.io/YOUR_ORG/your-repo/web:latest`

### 4. Release (`release.yml`)
**Triggers:** Manual workflow dispatch or git tag push

**Features:**
- Creates GitHub releases with changelog
- Generates commit log automatically
- Includes Docker image references

**Usage:**
```bash
git tag v1.0.0
git push origin v1.0.0
```

### 5. CodeQL (`codeql.yml`)
**Triggers:** Push to main/develop, PRs, weekly schedule

**Features:**
- Scans C# and JavaScript/TypeScript code
- Detects security vulnerabilities
- Results in GitHub Security tab

### 6. CI Master (`ci.yml`)
**Orchestrates** the entire CI/CD pipeline

## Setup Instructions

### 1. Push to GitHub
```bash
git init
git add .
git commit -m "Initial commit"
git remote add origin https://github.com/YOUR_ORG/test-service.git
git branch -M main
git push -u origin main
```

### 2. Enable Features in GitHub

#### Container Registry Access
The workflows use GitHub Container Registry (GHCR) which requires:
1. Go to **Settings → Packages**
2. Ensure container registry is enabled
3. PAT (Personal Access Token) is automatically provided via `GITHUB_TOKEN`

#### Branch Protection
Recommended for main branch:
1. Go to **Settings → Branches → Add rule**
2. Branch name pattern: `main`
3. Enable:
   - ✅ Require status checks to pass
   - ✅ Require pull request reviews
   - ✅ Require code reviews from code owners (optional)

#### Required Status Checks
Select these as required:
- `build-and-test (API CI)`
- `security-scan (API CI)`
- `build-and-test (Web App CI)`
- `security-scan (Web App CI)`
- `analyze (CodeQL)`

### 3. Configure Secrets (if needed)
If you need external deployment:

1. Go to **Settings → Secrets and variables → Actions**
2. Add secrets as needed:
   - `DOCKER_USERNAME` - For Docker Hub (optional)
   - `DOCKER_PASSWORD` - For Docker Hub (optional)
   - `AZURE_CREDENTIALS` - For Azure deployment (optional)

### 4. Customize Workflows

#### Change Container Registry
In `docker-build-and-push.yml`, change:
```yaml
REGISTRY: ghcr.io  # Change to docker.io, registry.gitlab.com, etc.
```

#### Add to Docker Hub
```yaml
- uses: docker/login-action@v3
  with:
    username: ${{ secrets.DOCKER_USERNAME }}
    password: ${{ secrets.DOCKER_PASSWORD }}
```

#### Change .NET Version
In `api-ci.yml`:
```yaml
dotnet-version: '10.0.x'  # Update as needed
```

#### Change Node Version
In `web-ci.yml`:
```yaml
node-version: '20'  # Update as needed
```

## Usage Examples

### Automatic Testing on PR
1. Create feature branch: `git checkout -b feature/my-feature`
2. Make changes and push
3. Create Pull Request
4. GitHub Actions will automatically run CI checks
5. Merge only after all checks pass ✅

### Manual Docker Build
```yaml
# Trigger manually via GitHub UI:
# Actions → Docker Build and Push → Run workflow
```

### Create Release
```bash
# Option 1: Via git tag
git tag v1.0.0
git push origin v1.0.0

# Option 2: Via GitHub UI
# Actions → Release → Run workflow
# Input version: 1.0.0
```

### View Results
1. **CI/CD Status:** Repository → Actions
2. **Build Artifacts:** Actions → Workflow → Download artifacts
3. **Security Issues:** Repository → Security → Code scanning alerts
4. **Releases:** Repository → Releases

## Monitoring & Troubleshooting

### View Workflow Logs
1. Go to **Actions** tab
2. Click on the workflow run
3. Click on a job to see detailed logs

### Common Issues

**Workflow not triggering?**
- Check branch name matches trigger conditions
- Verify file paths match the `paths:` filter
- Ensure `.github/workflows/` files are on your branch

**Docker push failing?**
- Ensure `GITHUB_TOKEN` has `packages: write` permission (default)
- Check image naming: `ghcr.io/owner/repo/name:tag`

**Tests failing?**
- Review test logs in workflow output
- Ensure local environment works: `dotnet test`, `npm test`

**CodeQL taking too long?**
- It's normal for first run
- Subsequent runs use caching and are faster

## Next Steps

1. ✅ Push to GitHub
2. ✅ Enable branch protection
3. ✅ Monitor first workflow runs
4. ✅ Create releases using git tags
5. ✅ Set up deployment (Kubernetes, Azure, AWS, etc.)

## Advanced: Adding Deployment

To add deployment after Docker build, you can extend the workflows:

```yaml
deploy:
  needs: [build-and-push-api, build-and-push-web]
  if: github.event_name == 'push' && github.ref == 'refs/heads/main'
  uses: ./.github/workflows/deploy-to-k8s.yml
  with:
    api-image: ghcr.io/${{ github.repository }}/api:latest
    web-image: ghcr.io/${{ github.repository }}/web:latest
```

See GitHub Actions documentation for deployment strategies specific to your hosting platform.
