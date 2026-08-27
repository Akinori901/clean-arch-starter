# syntax=docker/dockerfile:1

FROM node:22-alpine AS dev
WORKDIR /app
COPY package*.json ./
RUN npm ci 2>/dev/null || npm install
EXPOSE 5173

# ── ビルド（S3 へ配る静的成果物を作る）─────────────────────────
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci 2>/dev/null || npm install
COPY . .
# VITE_* はビルド時に埋め込まれる。実行時には差し替えられないため、
# 環境ごとにビルドし直すこと（S3 のバケットを環境ごとに分ける）。
ARG VITE_API_BASE_URL
ARG VITE_COGNITO_AUTHORITY
ARG VITE_COGNITO_CLIENT_ID
ARG VITE_COGNITO_REDIRECT_URI
RUN npm run build

# 成果物だけを取り出すためのステージ（CI で --output に使う）
FROM scratch AS dist
COPY --from=build /app/dist /
