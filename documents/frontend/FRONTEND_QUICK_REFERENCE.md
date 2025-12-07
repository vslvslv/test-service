# Frontend Application - Quick Reference

## Overview

The TestService Web application is a React-based dashboard with a Grafana-inspired design for visualizing and managing the Test Service API.

**Location:** `testservice-web/`

**Tech Stack:**
- React 19.2.0
- TypeScript 5.9.3
- Vite 7.2.5
- TailwindCSS 3.4.17

---

## Quick Start

### Prerequisites
- Node.js 20.19+ or 22.12+
- npm 10+ or pnpm 9+

### Installation

```bash
cd testservice-web
npm install
```

### Development

```bash
npm run dev
```

Open: http://localhost:5173

### Build

```bash
npm run build
npm run preview
```

---

## Project Structure

```
testservice-web/
??? src/
?   ??? components/       # UI components
?   ??? pages/            # Page components
?   ??? services/         # API services
?   ??? hooks/            # Custom React hooks
?   ??? types/            # TypeScript types
?   ??? utils/            # Utilities
??? public/               # Static assets
??? package.json
```

---

## Key Features

### Dashboard
- Real-time statistics
- Interactive charts
- Activity feed
- Environment health

### Entity Management
- Browse entities
- Filter by environment
- Create/Edit/Delete
- Bulk operations

### Schema Management
- Visual schema editor
- Field configuration
- Validation

### Environment Management
- Environment dashboard
- Statistics per environment
- Configuration

---

## Configuration

Create `.env`:

```env
VITE_API_BASE_URL=http://localhost:5000
```

---

## Components

### Layout Components
- `AppLayout.tsx` - Main layout
- `Sidebar.tsx` - Navigation
- `Header.tsx` - Top bar

### UI Components
- `Button.tsx`
- `Card.tsx`
- `Table.tsx`
- `Modal.tsx`

### Feature Components
- `EntityTable.tsx`
- `SchemaEditor.tsx`
- `EnvironmentSelector.tsx`

---

## API Integration

### Services

```typescript
// src/services/entities.service.ts
export const entitiesService = {
  getAll: (entityType, environment?) => {...},
  getById: (entityType, id) => {...},
  create: (entityType, data) => {...},
  // ...
};
```

### Hooks

```typescript
// src/hooks/useEntities.ts
export const useEntities = (entityType, environment) => {
  return useQuery({
    queryKey: ['entities', entityType, environment],
    queryFn: () => entitiesService.getAll(entityType, environment),
  });
};
```

---

## Styling

### Tailwind Configuration

Custom colors defined in `tailwind.config.js`:

```javascript
colors: {
  primary: '#3d71ff',
  success: '#56d364',
  warning: '#e3b341',
  error: '#f85149',
  // Dark theme colors
  dark: { ... },
  // Light theme colors
  light: { ... },
}
```

### Theme Toggle

Dark/Light theme support with system preference detection.

---

## Routing

```typescript
/                     ? Dashboard
/entities             ? Entity browser
/entities/:type       ? Specific entity type
/entities/:type/:id   ? Entity details
/schemas              ? Schema management
/environments         ? Environment dashboard
/settings             ? Settings
```

---

## Build & Deploy

### Docker

```dockerfile
FROM node:20-alpine as build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### Build Image

```bash
docker build -t testservice-web:latest .
```

### Run Container

```bash
docker run -p 3000:80 testservice-web:latest
```

---

## Development Tips

### Hot Reload
Changes are automatically reflected in browser

### TypeScript
Full type safety throughout

### React Query
Automatic caching and refetching

### TailwindCSS
Utility-first styling

---

## Testing

```bash
# Run tests
npm test

# Coverage
npm run test:coverage
```

---

## Troubleshooting

### Node Version Error
Upgrade to Node.js 20.19+ or 22.12+

### Port Already in Use
Change port in `vite.config.ts`:

```typescript
export default defineConfig({
  server: {
    port: 3001
  }
})
```

### Build Errors
Clear cache and reinstall:

```bash
rm -rf node_modules dist
npm install
npm run build
```

---

**For complete documentation, see:** [Complete Web Application Guide](WEB_APPLICATION_GUIDE.md)
