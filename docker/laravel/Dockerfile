# syntax=docker/dockerfile:1

# ── 開発用 ───────────────────────────────────────────────────
FROM php:8.4-cli AS dev
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
        git unzip libzip-dev libicu-dev \
    && docker-php-ext-install -j"$(nproc)" pdo_mysql zip intl bcmath \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

COPY --from=composer:2 /usr/bin/composer /usr/bin/composer

# 依存だけ先に入れてレイヤキャッシュを効かせる
COPY composer.json composer.lock* ./
RUN composer install --no-interaction --no-scripts --no-autoloader 2>/dev/null || true

EXPOSE 8001

# ── 本番（Lambda コンテナイメージ / Bref）──────────────────────
# Bref が提供する Lambda 向け PHP ランタイムを使う。
FROM bref/php-84-fpm:3 AS lambda
COPY . /var/task/
# Lambda では /tmp 以外書き込み不可のため、storage を /tmp へ逃がす。
# これを忘れるとログ書き込みで 500 になる（本番でだけ落ちる典型）。
ENV APP_STORAGE=/tmp
RUN rm -rf /var/task/storage/logs /var/task/storage/framework/cache
CMD ["public/index.php"]
