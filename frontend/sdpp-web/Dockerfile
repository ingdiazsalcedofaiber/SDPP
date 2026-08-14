FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Static assets served by nginx inside the intranet — never a CDN or public host (see
# docs/00-overview.md §3, "fuera de alcance": nada del procesamiento ni del hosting sale de la
# red corporativa).
FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080
