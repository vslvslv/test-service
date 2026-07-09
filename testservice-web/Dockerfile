# API base path: relative so same host as page (override with --build-arg for local Docker).
# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Override with --build-arg VITE_API_BASE_URL=http://localhost:5000 for local Docker.
ARG VITE_API_BASE_URL=/testservice
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

# Base path for the SPA. Default to '/testservice/ui/' so BrowserRouter
# generates URLs like /testservice/ui/login instead of /login, which
# prevents 404s on refresh behind the /testservice/ui ingress path.
# For Docker/root hosting, override with:
#   --build-arg VITE_BASE_PATH=/
ARG VITE_BASE_PATH=/testservice/ui/
ENV VITE_BASE_PATH=$VITE_BASE_PATH

# Version metadata surfaced in the About dialog. Injected by CI / the publish
# script (e.g. --build-arg VITE_GIT_SHA=$(git rev-parse --short HEAD)); defaults
# make a plain local `docker build` show a "dev" / local build.
ARG VITE_GIT_SHA=dev
ENV VITE_GIT_SHA=$VITE_GIT_SHA
ARG VITE_BUILD_DATE=
ENV VITE_BUILD_DATE=$VITE_BUILD_DATE

# Copy package files
COPY package*.json ./

# Clean install with no cache to ensure consistent builds
RUN npm ci --prefer-offline --no-audit

# Copy source files
COPY . .

# Build application with clean output
RUN npm run build

# Production stage
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Remove default nginx static assets
RUN rm -rf ./*

# Copy built app from build stage
COPY --from=build /app/dist .

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port 80
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://localhost/health || exit 1

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
