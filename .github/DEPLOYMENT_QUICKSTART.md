# 🚀 Quick Deployment Guide

## GitHub Pages (Web App Only)

### 1️⃣ Enable GitHub Pages
- Repository → Settings → Pages
- Source: **GitHub Actions**

### 2️⃣ Deploy API First
Choose one option:
- **Railway**: `railway login && railway init && railway up`
- **Render**: Connect repo at render.com
- **Heroku**: `heroku create && heroku container:push web`

### 3️⃣ Configure API URL
Add repository variable:
- Settings → Secrets and variables → Actions → Variables
- Name: `VITE_API_BASE_URL`
- Value: Your API URL (e.g., `https://your-api.railway.app`)

### 4️⃣ Deploy
```bash
git push origin main
```

### 5️⃣ Access
Your site: `https://YOUR_USERNAME.github.io/test-service/`

⏱️ Takes 2-5 minutes

## Manual Trigger
- Actions tab → "Deploy to GitHub Pages" → Run workflow

## Troubleshooting
- **404s**: Check base path in vite.config.ts
- **CORS errors**: Update API CORS to allow GitHub Pages domain
- **Old version**: Hard refresh (Ctrl+Shift+R)

📖 **Full Guide**: [.github/GITHUB_PAGES_DEPLOYMENT.md](.github/GITHUB_PAGES_DEPLOYMENT.md)
