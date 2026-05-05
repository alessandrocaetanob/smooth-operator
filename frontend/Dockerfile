# Stage 1: Build the Angular application
FROM node:22-alpine AS build

WORKDIR /app

# Copy dependency definitions
COPY package*.json ./

# Upgrade npm to match the version declared in packageManager field
RUN npm install -g npm@11.11.0

# Use npm install instead of npm ci: @tailwindcss/postcss bundles @tailwindcss/oxide-wasm32-wasi
# whose bundled WASM dependencies resolve differently on Linux vs Windows, causing lock file
# sync failures with npm ci. npm install resolves platform-appropriate packages correctly.
RUN npm install

# Copy the rest of the application
COPY . .

# Build the application
RUN npm run build --configuration=production

# Stage 2: Serve the application with nginx-unprivileged (runs as non-root on port 8080)
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

# Switch to root temporarily for file setup
USER root

# Copy the Angular build output
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

# Copy nginx server-block config and startup script
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY entrypoint.sh /entrypoint.sh
# Strip Windows CRLF line endings (defensive: script must be pure LF for the shebang to work on Linux)
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh

# Create the runtime-config directory AFTER the build copy so COPY can't reset ownership.
# entrypoint.sh writes config.json here at container start from env vars.
RUN mkdir -p /usr/share/nginx/html/config \
    && chown -R nginx:nginx /usr/share/nginx/html/config

# Switch back to the unprivileged nginx user for runtime
USER nginx

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/ || exit 1

ENTRYPOINT ["/entrypoint.sh"]
