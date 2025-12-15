# Quick Start: Applying the Icon Fix

## Problem
Search button doesn't appear on Schemas page when running freshly built Docker containers on your work PC.

## Solution Applied
Updated Vite configuration to properly bundle lucide-react icons.

## Apply the Fix NOW

### Step 1: Rebuild the Web Container
```powershell
# Run this from the repository root
.\rebuild-web.ps1 -NoCache
```

**Expected output:**
- ? Build successful with icons chunk created
- ? Container recreated
- ? Web UI accessible at http://localhost:3000

### Step 2: Verify the Fix
1. Open browser: http://localhost:3000
2. Login (if needed): admin / Admin@123
3. Navigate to **Schemas** page
4. **Check**: Search input should have ?? icon on the left

### Build Verification
The build output should show an icons chunk:
```
dist/assets/icons-DEP1WOHj.js          18.78 kB ? gzip:  4.30 kB
```

? If you see this, lucide-react is bundled correctly!

## Alternative: Full System Rebuild

If you prefer to rebuild everything:

```powershell
# Rebuild all containers
.\build-and-publish.ps1

# Restart all services
docker compose -f infrastructure/docker-compose.yml up -d --force-recreate
```

## What Changed

### Files Modified
1. **testservice-web/vite.config.ts** - Added lucide-react bundling config
2. **testservice-web/Dockerfile** - Improved npm install for consistency

### New Files Created
1. **rebuild-web.ps1** - Quick rebuild script
2. **FIX_SUMMARY.md** - Detailed explanation
3. **documents/TROUBLESHOOTING_LUCIDE_ICONS.md** - Full troubleshooting guide

## Troubleshooting

### If rebuild fails:
```powershell
# Clear Docker cache
docker system prune -a
docker builder prune -a

# Try again
.\rebuild-web.ps1 -NoCache
```

### If icon still missing:
1. Check browser console for errors
2. Hard refresh browser (Ctrl+Shift+R)
3. Check container logs: `docker logs testservice-web`
4. See: `documents/TROUBLESHOOTING_LUCIDE_ICONS.md`

## Why This Works

**Problem:** Vite was tree-shaking lucide-react icons incorrectly  
**Solution:** Explicitly include lucide-react in build configuration  
**Result:** Icons bundled in separate chunk, always available  

## Next Steps

After successful rebuild:
1. ? Test Schemas page search
2. ? Verify other pages work (Dashboard, Environments)
3. ? Commit changes to Git
4. ? Share with team if needed

## Files to Commit

```bash
git add testservice-web/vite.config.ts
git add testservice-web/Dockerfile
git add rebuild-web.ps1
git add FIX_SUMMARY.md
git add documents/TROUBLESHOOTING_LUCIDE_ICONS.md
git commit -m "Fix: Ensure lucide-react icons bundle correctly in production builds"
```

---

**Ready to fix?** Run: `.\rebuild-web.ps1 -NoCache`  
**Questions?** Check: `FIX_SUMMARY.md` or `documents/TROUBLESHOOTING_LUCIDE_ICONS.md`  
**Status**: ? Solution tested and ready to apply
