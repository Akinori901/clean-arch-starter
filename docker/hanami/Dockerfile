# syntax=docker/dockerfile:1

# ── 共通ベース ────────────────────────────────────────────────
FROM ruby:3.4-slim AS base
WORKDIR /app

# mysql2 のビルドに必要なもの
RUN apt-get update -qq && apt-get install -y --no-install-recommends \
        build-essential default-libmysqlclient-dev pkg-config git \
    && rm -rf /var/lib/apt/lists/*

ENV BUNDLE_PATH=/usr/local/bundle \
    BUNDLE_JOBS=4

# ── 依存インストール ──────────────────────────────────────────
FROM base AS deps
COPY Gemfile Gemfile.lock* ./
RUN bundle install

# ── 開発用 ───────────────────────────────────────────────────
# ソースはマウントするので COPY しない（編集が即反映される）。
FROM deps AS dev
EXPOSE 8003

# ── 本番（Lambda コンテナイメージ）─────────────────────────────
# Ruby の Lambda ランタイムを使い、Rack アプリを handler で受ける。
FROM public.ecr.aws/lambda/ruby:3.4 AS lambda
COPY --from=deps /usr/local/bundle /usr/local/bundle
COPY . ${LAMBDA_TASK_ROOT}/
ENV BUNDLE_PATH=/usr/local/bundle
# Lambda では /tmp 以外書き込み不可。tmp をそこへ逃がす。
ENV TMPDIR=/tmp
CMD ["lambda_handler.handler"]
