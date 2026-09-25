# Angular 18 needs Node ^18.19.1 || ^20.11.1 || ^22 (T1).
FROM node:20-alpine AS build

WORKDIR /app

# Install dependencies first so this layer is cached until package.json changes.
COPY package*.json ./
RUN npm install

# Copy the app and build the production bundle.
COPY . .
RUN npm run build -- --configuration production

# Serve with nginx. nginx.conf adds the SPA fallback and proxies /api to the backend (T2).
FROM nginx:alpine

COPY nginx.conf /etc/nginx/conf.d/default.conf

# The Angular 17+ application builder emits the browser bundle under dist/<project>/browser.
COPY --from=build /app/dist/web-client/browser /usr/share/nginx/html

EXPOSE 80
