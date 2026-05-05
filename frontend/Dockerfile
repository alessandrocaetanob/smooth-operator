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

# Stage 2: Serve the application with Nginx
FROM nginx:alpine

# Copy the build output from the previous stage to Nginx's html directory
# Note: Ensure the dist path matches the output path defined in angular.json
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

# Copy a custom nginx configuration (optional, recommended for Angular routing)
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy the entrypoint that injects runtime config from environment variables
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:80/ || exit 1

ENTRYPOINT ["/entrypoint.sh"]
