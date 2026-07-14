# syntax=docker/dockerfile:1

# Build stage
FROM node:22-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN --mount=type=cache,target=/root/.npm npm ci --ignore-scripts
COPY . .
RUN npm run build

# Serve stage — unprivileged nginx (runs as non-root on port 8080)
FROM nginxinc/nginx-unprivileged:1.30.1-alpine
# apk upgrade: pull Alpine security fixes that land between base-image rebuilds
# (same rationale as the frontend image, which the Trivy CI gate scans).
USER root
RUN apk upgrade --no-cache
USER nginx
COPY --from=builder /app/build /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
# Probe 127.0.0.1 explicitly — "localhost" can resolve to IPv6 (::1), which
# nginx's IPv4 "listen 8080" does not bind.
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/ || exit 1
