# Build stage
FROM node:20 AS build
WORKDIR /app
COPY PasswordBreaker.Frontend/package*.json ./
RUN npm install
COPY PasswordBreaker.Frontend/ ./
# Set production API URL if needed, or handle via env/proxy
RUN npm run build

# Production stage
FROM nginx:stable-alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
