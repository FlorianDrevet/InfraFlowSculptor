# syntax=docker/dockerfile:1

# ---- build ----
FROM node:24-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# ---- runtime ----
FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist/ifs-web/browser /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
# nginx's own image runs every executable script under /docker-entrypoint.d/
# before starting — this one regenerates config.json from env vars, so the
# same image can be deployed to every environment ("build once, deploy
# anywhere"). See src/app/core/config/provide-app-config.ts.
COPY docker/docker-entrypoint.sh /docker-entrypoint.d/30-generate-config.sh
RUN chmod +x /docker-entrypoint.d/30-generate-config.sh

EXPOSE 80
