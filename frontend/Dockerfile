# syntax=docker/dockerfile:1

# Pinned nginx version — must match the FROM tags below so the compiled
# brotli module is ABI-compatible with the runtime nginx binary.
ARG NGINX_VERSION=1.30.1

# Stage 1: Build the Angular application
FROM node:22-alpine AS build

WORKDIR /app

# Copy dependency definitions
COPY package*.json ./

# Upgrade npm to match the version declared in packageManager field
RUN npm install -g npm@11.11.0 --ignore-scripts

# npm ci for a deterministic, lockfile-locked install (the lock file records the
# platform-specific @tailwindcss/oxide binaries for all platforms, so Linux resolves
# correctly). The BuildKit cache mount keeps the npm cache warm across builds.
RUN --mount=type=cache,target=/root/.npm npm ci --ignore-scripts

# Copy the rest of the application
COPY . .

# Build the application
RUN npm run build --configuration=production

# Precompress static assets (>1 KiB) to .gz and .br so nginx can serve them via
# gzip_static / brotli_static instead of compressing on every request.
RUN apk add --no-cache brotli gzip \
    && find /app/dist/frontend/browser -type f -size +1024c \
       \( -name '*.js' -o -name '*.css' -o -name '*.html' -o -name '*.svg' \
          -o -name '*.json' -o -name '*.txt' \) \
       -exec gzip -9 -k -f {} \; \
       -exec brotli -q 11 -f {} \;

# Stage 2: Compile the ngx_brotli dynamic modules.
# Built FROM the runtime image so the Alpine/musl/compiler toolchain — and thus
# the module ABI — matches the nginx binary that will load them.
FROM nginxinc/nginx-unprivileged:${NGINX_VERSION}-alpine AS brotli-build
ARG NGINX_VERSION
USER root
RUN apk add --no-cache build-base cmake git pcre2-dev zlib-dev openssl-dev linux-headers curl
WORKDIR /build
# --proto "=https" ensures any redirect stays on HTTPS (no clear-text downgrade).
RUN curl --proto "=https" --tlsv1.2 -fsSL "https://nginx.org/download/nginx-${NGINX_VERSION}.tar.gz" | tar xz \
    && git clone --depth 1 --recurse-submodules https://github.com/google/ngx_brotli.git
# Build the bundled brotli static libraries that ngx_brotli links against;
# linking them statically keeps the runtime image free of libbrotli.
RUN cd ngx_brotli/deps/brotli && mkdir out && cd out \
    && cmake -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=OFF -DCMAKE_C_FLAGS="-fPIC" .. \
    && cmake --build . --config Release
WORKDIR /build/nginx-${NGINX_VERSION}
# --with-compat lets the resulting dynamic module load into the pre-built
# nginx binary; `make modules` builds only the .so files, not all of nginx.
RUN ./configure --with-compat --add-dynamic-module=/build/ngx_brotli \
    && make -j"$(nproc)" modules

# Stage 3: Serve the application with nginx-unprivileged (runs as non-root on port 8080)
FROM nginxinc/nginx-unprivileged:${NGINX_VERSION}-alpine AS final

# Switch to root temporarily for file setup
USER root

# Brotli dynamic modules — loaded via load_module in nginx-main.conf
COPY --from=brotli-build \
    /build/nginx-${NGINX_VERSION}/objs/ngx_http_brotli_filter_module.so \
    /build/nginx-${NGINX_VERSION}/objs/ngx_http_brotli_static_module.so \
    /usr/lib/nginx/modules/

# Copy the Angular build output (including the precompressed .gz/.br variants)
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

# Copy nginx configs: main context (worker + module loading) and server block
COPY nginx-main.conf /etc/nginx/nginx.conf
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY entrypoint.sh /entrypoint.sh
# Strip Windows CRLF line endings (defensive: script must be pure LF for the shebang to work on Linux)
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh

# Switch back to the unprivileged nginx user for runtime
USER nginx

EXPOSE 8080

# Probe 127.0.0.1 explicitly — "localhost" can resolve to IPv6 (::1), which
# nginx's IPv4 "listen 8080" does not bind.
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/ || exit 1

ENTRYPOINT ["/entrypoint.sh"]
