FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build
RUN ls -la /app/dist

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
RUN echo "=== Вміст /usr/share/nginx/html після копіювання ===" && ls -la /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]