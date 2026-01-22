# GitHub Pages Deployment Guide

## 🌐 Overview

This guide explains how to deploy your Test Service web application to **GitHub Pages** for free, publicly accessible hosting.

**Important Notes:**
- ✅ **Web App (React)**: Can be deployed to GitHub Pages
- ⚠️ **API (.NET)**: Cannot run on GitHub Pages (static hosting only) - needs separate deployment

## 🚀 Quick Setup (5 minutes)

### Step 1: Enable GitHub Pages

1. Go to your repository on GitHub
2. Click **Settings** → **Pages**
3. Under "Build and deployment":
   - Source: **GitHub Actions**
4. Save the settings

### Step 2: Configure Your API Endpoint

Since GitHub Pages only hosts static files, you need to deploy your API separately. Choose one option:

#### Option A: Free Cloud Hosting (Recommended for Testing)

Deploy your API to one of these free platforms:

**Railway.app** (Recommended)
```bash
# Install Railway CLI
npm i -g @railway/cli

# Login and deploy
railway login
railway init
railway up
```
Your API will be at: `https://your-app.railway.app`

**Render.com**
1. Go to [render.com](https://render.com)
2. Connect your GitHub repo
3. Create a new "Web Service"
4. Set build command: `dotnet publish -c Release`
5. Set start command: `dotnet TestService.Api/bin/Release/net10.0/TestService.Api.dll`
6. Deploy!

**Heroku**
```bash
heroku login
heroku create your-testservice-api
heroku container:push web --app your-testservice-api
heroku container:release web --app your-testservice-api
```

#### Option B: GitHub Codespaces (For Development)
Run your API in a Codespace and expose it publicly (free tier available).

### Step 3: Update API Configuration

After deploying your API, update the endpoint:

**Method 1: Using GitHub Variables (Recommended)**
1. Go to **Settings** → **Secrets and variables** → **Actions** → **Variables**
2. Click "New repository variable"
3. Name: `VITE_API_BASE_URL`
4. Value: `https://your-api-endpoint.com` (your deployed API URL)
5. Save

**Method 2: Edit .env.production**
Update [testservice-web/.env.production](testservice-web/.env.production):
```bash
VITE_API_BASE_URL=https://your-api.railway.app
```

### Step 4: Deploy!

```bash
# Commit your changes
git add .
git commit -m "Configure GitHub Pages deployment"
git push origin main
```

The GitHub Action will automatically:
1. Build your React app
2. Deploy to GitHub Pages
3. Make it publicly accessible

### Step 5: Access Your Site

Your site will be available at:
```
https://YOUR_USERNAME.github.io/test-service/
```

Or if you're using an organization:
```
https://YOUR_ORG.github.io/test-service/
```

⏱️ **First deployment takes 2-5 minutes**

## 📋 Deployment Workflow

The workflow (`.github/workflows/deploy-github-pages.yml`) runs automatically on:
- ✅ Push to `main` branch
- ✅ Changes in `testservice-web/` folder
- ✅ Manual trigger via Actions tab

### Manual Deployment
1. Go to **Actions** tab
2. Select "Deploy to GitHub Pages"
3. Click "Run workflow"
4. Select branch (usually `main`)
5. Click "Run workflow"

## 🔧 Advanced Configuration

### Custom Domain

1. **Add CNAME record** in your DNS:
   ```
   CNAME: www.yourdomain.com → YOUR_USERNAME.github.io
   ```

2. **Configure in GitHub**:
   - Settings → Pages → Custom domain
   - Enter: `www.yourdomain.com`
   - Check "Enforce HTTPS"

3. **Update vite.config.ts**:
   ```typescript
   base: '/' // Remove base path for custom domain
   ```

### Update Base Path

If your repository name is different, update [testservice-web/vite.config.ts](testservice-web/vite.config.ts):

```typescript
base: process.env.GITHUB_PAGES === 'true' ? '/YOUR-REPO-NAME/' : '/testservice/ui/',
```

### Environment-Specific Builds

Create different builds for different environments:

```bash
# Development
cd testservice-web
npm run dev

# Production (GitHub Pages)
GITHUB_PAGES=true npm run build

# Production (Custom)
VITE_API_BASE_URL=https://api.example.com npm run build
```

## 🏗️ API Deployment Options

### Option 1: Railway.app (Easiest)

**Pros:** Free tier, automatic HTTPS, easy deployment
**Cons:** Free tier has usage limits

```bash
railway login
railway init
railway link
railway up
```

### Option 2: Azure App Service

**Pros:** Integrated with Azure, scalable
**Cons:** Requires Azure account

```bash
az login
az webapp up --name testservice-api --resource-group myResourceGroup
```

### Option 3: Docker Container Registry + Cloud Run

**Pros:** Serverless, pay-per-use
**Cons:** Slightly more complex

1. Build and push Docker image (already configured in GitHub Actions)
2. Deploy to Google Cloud Run, Azure Container Instances, or AWS ECS

### Option 4: Self-Hosted

Run on your own server with reverse proxy:
```bash
docker-compose -f infrastructure/docker-compose.yml up -d
```

## 🔒 Security Considerations

### CORS Configuration

Update your API's CORS settings to allow your GitHub Pages domain:

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGitHubPages",
        builder => builder
            .WithOrigins(
                "https://YOUR_USERNAME.github.io",
                "http://localhost:5173" // For local dev
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

app.UseCors("AllowGitHubPages");
```

### API Authentication

If your API requires authentication:
1. Use environment variables for API keys
2. Store in GitHub Secrets
3. Never commit credentials to repository

```bash
# GitHub Settings → Secrets and variables → Actions
API_KEY=your-secret-key
```

## 📊 Monitoring Deployment

### Check Deployment Status
1. Go to **Actions** tab
2. Click on latest workflow run
3. View logs for each step

### Troubleshooting

**Deployment fails:**
```bash
# Check build locally
cd testservice-web
npm run build
```

**404 errors on routes:**
- GitHub Pages serves `index.html` for root only
- Configure 404.html redirect (already included in workflow)

**API connection fails:**
- Verify CORS settings on API
- Check API URL in browser console
- Ensure API is running and accessible

## 🎯 Checklist

Before deploying, ensure:

- [ ] GitHub Pages is enabled in repository settings
- [ ] API is deployed and accessible
- [ ] API URL is configured in GitHub variables or `.env.production`
- [ ] CORS is configured on API to allow GitHub Pages domain
- [ ] Base path in vite.config.ts matches repository name
- [ ] All changes are committed and pushed to `main` branch

## 🔄 Continuous Deployment

Every push to `main` that changes the web app will:
1. ✅ Automatically build
2. ✅ Run tests and linting
3. ✅ Deploy to GitHub Pages
4. ✅ Update live site in ~2 minutes

## 📱 Testing

After deployment, test:
1. Visit your GitHub Pages URL
2. Check browser console for errors
3. Test API connectivity
4. Verify all routes work correctly
5. Test on mobile devices

## 🆘 Common Issues

### Issue: 404 on page refresh
**Solution:** GitHub Pages handles this automatically with fallback to `index.html`

### Issue: API CORS errors
**Solution:** Add your GitHub Pages URL to API CORS policy

### Issue: Assets not loading
**Solution:** Check base path in vite.config.ts matches your repo name

### Issue: Old version shows after deployment
**Solution:** 
- Hard refresh: `Ctrl+Shift+R` (Windows) or `Cmd+Shift+R` (Mac)
- Clear browser cache
- Wait 5 minutes for CDN propagation

## 🎉 Success!

Your application is now:
- ✅ Publicly accessible via GitHub Pages
- ✅ Automatically deployed on every push
- ✅ Served over HTTPS
- ✅ Free hosting forever (within GitHub's limits)

**Live URL:** `https://YOUR_USERNAME.github.io/test-service/`

---

**Need Help?**
- Check GitHub Actions logs for deployment errors
- Review API connectivity in browser console
- Consult [GitHub Pages documentation](https://docs.github.com/pages)
