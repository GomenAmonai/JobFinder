# Build context = repo root.
# docker build -f frontend/Dockerfile --build-arg VITE_API_URL=https://api.example.com -t jobradar-web .
FROM node:20-alpine AS build
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
# Vite inlines env at BUILD time — the API URL must be known here, not at runtime.
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

FROM node:20-alpine AS runtime
WORKDIR /app
RUN npm i -g serve@14
COPY --from=build /app/dist ./dist
# -s = SPA fallback to index.html; bind to $PORT (Railway) or 3000 locally.
ENTRYPOINT ["sh", "-c", "serve -s dist -l tcp://0.0.0.0:${PORT:-3000}"]
