## Multi-stage build for Vue 3 (Vite)

# Build stage
FROM node:20-alpine AS build
WORKDIR /app/client

# Install deps
COPY client/package.json client/package-lock.json ./
RUN npm ci

# Copy source and build
COPY client/ ./
RUN npm run build

# Runtime stage: Nginx to serve static files
FROM nginx:alpine AS final

# Copy SPA-friendly nginx config
COPY client/nginx.conf /etc/nginx/conf.d/default.conf

# Copy built assets
COPY --from=build /app/client/dist /usr/share/nginx/html

EXPOSE 80