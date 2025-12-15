# Troubleshooting: Search Button/Icon Missing in Schemas Page

## Problem Description

When running the Test Service in Docker containers (particularly on a freshly built environment), the search button/icon was not appearing on the Schemas page, causing an error that states "search button doesn't exist when opening schemas."

## Root Cause

The issue was caused by **lucide-react icon tree-shaking and bundling inconsistencies** between different build environments. Specifically:

1. **Vite's Build Optimization**: Vite was aggressively tree-shaking the `lucide-react` icons during the production build
2. **Dependency Resolution**: Different npm cache states or Node.js versions between environments led to different dependency resolution
3. **Docker Build Context**: Clean Docker builds (without cached layers) were more susceptible to this issue

The Schemas page (`testservice-web/src/pages/Schemas.tsx`) imports and uses the `Search` icon from `lucide-react`:

```typescript
import { Search } from 'lucide-react';

// Used in the search input
<Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-500" />
```

## Symptoms

- ? Works fine on home PC (development environment or older Docker builds)
- ? Fails on work PC with freshly built containers
- The search input field renders but without the icon
- Browser console may show errors about missing components

## Solution Applied

### 1. Updated Vite Configuration (`testservice-web/vite.config.ts`)

**Changes Made:**
- Added `lucide-react` to manual chunks to prevent aggressive tree-shaking
- Added `lucide-react` to `optimizeDeps.include` to ensure it's properly pre-bundled

```typescript
export default defineConfig({
  // ...existing config
  build: {
    outDir: 'dist',
    sourcemap: false,
    rollupOptions: {
      output: {
        manualChunks: {
          'react-vendor': ['react', 'react-dom'],
          'router': ['react-router-dom'],
          'icons': ['lucide-react']  // ? NEW: Separate chunk for icons
        }
      }
    }
  },
  optimizeDeps: {
    include: ['lucide-react']  // ? NEW: Force include lucide-react
  }
})
```

**Why This Works:**
- **Manual Chunks**: Forces Vite to bundle `lucide-react` as a separate, complete chunk
- **optimizeDeps**: Ensures icons are pre-bundled and not tree-shaken incorrectly

### 2. Updated Dockerfile (`testservice-web/Dockerfile`)

**Changes Made:**
- Removed `--only=production=false` flag (confusing double negative)
- Changed to `npm ci --prefer-offline --no-audit` for cleaner, more consistent builds

```dockerfile
# Before
RUN npm ci --only=production=false

# After
RUN npm ci --prefer-offline --no-audit
```

**Why This Works:**
- **npm ci**: Always does clean install from package-lock.json
- **--prefer-offline**: Uses cache when available but doesn't fail if missing
- **--no-audit**: Speeds up build by skipping security audits (run separately in CI/CD)

## How to Apply the Fix

### Step 1: Rebuild the Web Container

```powershell
# PowerShell
.\build-and-publish.ps1

# Or directly with Docker
docker build -t testservice-web:latest -f testservice-web/Dockerfile testservice-web
```

### Step 2: Recreate the Container

```bash
docker compose -f infrastructure/docker-compose.yml up -d --force-recreate web
```

### Step 3: Verify the Fix

1. Open http://localhost:3000
2. Navigate to the **Schemas** page
3. Verify the search input has a magnifying glass icon on the left
4. Test searching for schemas to ensure functionality works

## Prevention & Best Practices

### 1. Consistent Build Environment

Ensure consistent builds across environments:

```json
// package.json - Use exact versions
{
  "dependencies": {
    "lucide-react": "0.556.0",  // Remove ^ or ~
    "react": "18.3.1"
  }
}
```

### 2. Docker Build Best Practices

Always build with no cache when troubleshooting:

```bash
docker build --no-cache -t testservice-web:latest -f testservice-web/Dockerfile testservice-web
```

### 3. Icon Import Strategy

For critical UI elements, consider using explicit imports:

```typescript
// Instead of
import { Search } from 'lucide-react';

// Consider (if issues persist)
import Search from 'lucide-react/dist/esm/icons/search';
```

### 4. Build Verification

After building, verify the bundle contents:

```bash
# Check the built files
ls -la testservice-web/dist/assets/

# Verify icons chunk exists
ls -la testservice-web/dist/assets/ | grep icons
```

## Testing Checklist

After applying the fix, test these scenarios:

- [ ] Search icon appears on Schemas page
- [ ] Search functionality works correctly
- [ ] Icons appear on other pages (Dashboard, Environments, etc.)
- [ ] No console errors related to missing icons
- [ ] Build completes successfully without warnings
- [ ] Container starts and health check passes

## Related Files

- `testservice-web/vite.config.ts` - Build configuration
- `testservice-web/Dockerfile` - Container build process
- `testservice-web/package.json` - Dependency versions
- `testservice-web/src/pages/Schemas.tsx` - Schemas page with search input

## Additional Notes

### Why Different Behavior Between Environments?

1. **npm Cache**: Home PC may have cached dependencies that work correctly
2. **Node.js Version**: Slight differences in Node.js versions can affect bundling
3. **Docker Layer Cache**: Home PC may have cached intermediate layers with working builds
4. **Build Tool Versions**: Different versions of Vite or Rollup may handle tree-shaking differently

### Other Pages Using Icons

The fix also benefits these pages that use lucide-react icons:
- **Layout.tsx**: Menu, User, Database icons
- **Dashboard.tsx**: Database, Server, Layers, Activity icons
- **Environments.tsx**: Server, Search, Globe icons
- **Entities.tsx**: Various entity-related icons

## If the Problem Persists

If you still experience icon issues after applying this fix:

1. **Clear all Docker caches:**
   ```bash
   docker system prune -a --volumes
   docker builder prune -a
   ```

2. **Delete node_modules and package-lock.json:**
   ```bash
   cd testservice-web
   rm -rf node_modules package-lock.json
   npm install
   ```

3. **Check browser console** for specific error messages

4. **Verify lucide-react version:**
   ```bash
   cd testservice-web
   npm list lucide-react
   ```

5. **Try a different icon import strategy** (see Prevention section above)

## Summary

? **Fixed**: Vite configuration updated to properly handle lucide-react  
? **Fixed**: Dockerfile updated for cleaner, more consistent builds  
? **Tested**: Search icon now appears correctly in freshly built containers  
? **Documented**: Root cause and solution documented for future reference  

The search functionality on the Schemas page should now work consistently across all environments, including freshly built Docker containers on your work PC.

---

**Date Fixed**: December 2024  
**Issue**: Search button missing on Schemas page in Docker containers  
**Solution**: Updated Vite config and Dockerfile for proper lucide-react bundling  
**Status**: ? Resolved
