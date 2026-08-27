# syntax=docker/dockerfile:1

# ── 共通ベース ────────────────────────────────────────────────
# 本番の Lambda と開発で同じベースを使い、「手元では動いた」を減らす。
FROM public.ecr.aws/lambda/python:3.13 AS base

# mysqlclient のビルドに必要なもの。ここで入れておくと dev/prod 両方で使える。
RUN dnf install -y gcc python3-devel mariadb-connector-c-devel pkgconf-pkg-config \
    && dnf clean all

# ── 依存インストール ──────────────────────────────────────────
FROM base AS deps
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# ── 開発用 ───────────────────────────────────────────────────
# ソースはマウントするので COPY しない（編集が即反映される）。
FROM deps AS dev
WORKDIR /app
# requirements-dev.txt の先頭が `-r requirements.txt` のため、
# 同じディレクトリに両方が揃っている必要がある（deps で WORKDIR を揃えてある）。
COPY requirements-dev.txt .
RUN pip install --no-cache-dir -r requirements-dev.txt
ENV PYTHONPATH=/app/src \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1
# Lambda のベースイメージは ENTRYPOINT が固定されているため、
# 開発時は素の python を使えるよう上書きする。
ENTRYPOINT []
EXPOSE 8000

# ── 本番（Lambda コンテナイメージ）─────────────────────────────
FROM deps AS lambda
# Lambda は LAMBDA_TASK_ROOT 配下を読む
COPY src/ ${LAMBDA_TASK_ROOT}/
ENV PYTHONPATH=${LAMBDA_TASK_ROOT}
# config/lambda_handler.py の handler（Mangum）を起点にする
CMD ["config.lambda_handler.handler"]
