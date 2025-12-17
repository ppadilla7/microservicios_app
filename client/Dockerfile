## Multi-stage build for Vue 3 (Vite)

# Build stage
FROM node:20-alpine AS build
WORKDIR /app/client

# Variables Vite configurables vía build args
ARG VITE_COURSES_API
ARG VITE_STUDENTS_API
ARG VITE_ENROLLMENT_API
ARG VITE_NOTIFICATIONS_API
ARG VITE_SECURITY_API

ENV VITE_COURSES_API=${VITE_COURSES_API}
ENV VITE_STUDENTS_API=${VITE_STUDENTS_API}
ENV VITE_ENROLLMENT_API=${VITE_ENROLLMENT_API}
ENV VITE_NOTIFICATIONS_API=${VITE_NOTIFICATIONS_API}
ENV VITE_SECURITY_API=${VITE_SECURITY_API}

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