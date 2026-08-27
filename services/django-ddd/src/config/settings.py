"""Django settings。

環境変数の読み取りはここに集約する。
各層が os.environ を直接読むと、設定の全体像が追えなくなる。
"""
from __future__ import annotations

import os
from pathlib import Path
from typing import Any

BASE_DIR = Path(__file__).resolve().parent.parent

SECRET_KEY = os.environ.get("DJANGO_SECRET_KEY", "dev-only-insecure-key")
DEBUG = os.environ.get("DJANGO_DEBUG", "false").lower() == "true"
ALLOWED_HOSTS = os.environ.get("DJANGO_ALLOWED_HOSTS", "*").split(",")

INSTALLED_APPS = [
    "django.contrib.contenttypes",
    "django.contrib.auth",
    "rest_framework",
    "corsheaders",
    # Model を保持するためだけに infrastructure を app として登録する。
    # ここに domain/application を登録しないこと（Django に依存させない）。
    "infrastructure.django_orm",
]

MIDDLEWARE = [
    "corsheaders.middleware.CorsMiddleware",
    "django.middleware.common.CommonMiddleware",
]

ROOT_URLCONF = "config.urls"
WSGI_APPLICATION = "config.wsgi.application"

DATABASES = {
    "default": {
        "ENGINE": "django.db.backends.mysql",
        "NAME": os.environ.get("DB_NAME", "app"),
        "USER": os.environ.get("DB_USER", "app"),
        "PASSWORD": os.environ.get("DB_PASSWORD", "app"),
        "HOST": os.environ.get("DB_HOST", "127.0.0.1"),
        "PORT": os.environ.get("DB_PORT", "3306"),
        "OPTIONS": {"charset": "utf8mb4"},
        # Lambda では実行環境が再利用されるため、短めに保持して再接続コストを抑える。
        # RDS Proxy を挟む場合は 0 にしてコネクションを都度返すこと。
        "CONN_MAX_AGE": int(os.environ.get("DB_CONN_MAX_AGE", "60")),
    }
}

# --- Cognito ---------------------------------------------------------
# ENDPOINT_URL はローカルの cognito-local を指すときのみ設定する。
# 本番では空にして、boto3 の既定エンドポイント（AWS）を使う。
COGNITO = {
    "USER_POOL_ID": os.environ.get("COGNITO_USER_POOL_ID", ""),
    "CLIENT_ID": os.environ.get("COGNITO_CLIENT_ID", ""),
    "CLIENT_SECRET": os.environ.get("COGNITO_CLIENT_SECRET", ""),
    "REGION": os.environ.get("AWS_REGION", "ap-northeast-1"),
    "ENDPOINT_URL": os.environ.get("COGNITO_ENDPOINT_URL", ""),
    # ローカルのエミュレータで JWT を検証するときだけ設定する。
    # 本番では空のままにすること（実 Cognito の issuer が使われる）。
    "ISSUER_OVERRIDE": os.environ.get("COGNITO_ISSUER_OVERRIDE", ""),
    # 同上。エミュレータへコンテナ名で到達するために使う。本番は空。
    "JWKS_URL_OVERRIDE": os.environ.get("COGNITO_JWKS_URL_OVERRIDE", ""),
}

# --- オブジェクトストレージ -------------------------------------------
# 本番は S3、ローカルは SeaweedFS。endpoint_url の差し替えのみで両対応する。
OBJECT_STORAGE = {
    "BUCKET": os.environ.get("S3_BUCKET", "app-static"),
    "ENDPOINT_URL": os.environ.get("S3_ENDPOINT_URL", ""),
}

CORS_ALLOWED_ORIGINS = [
    o for o in os.environ.get("CORS_ALLOWED_ORIGINS", "").split(",") if o
]

REST_FRAMEWORK: dict[str, Any] = {
    # 認証は Cognito の JWT を View 側で検証する。DRF の認証機構は使わない。
    "DEFAULT_AUTHENTICATION_CLASSES": [],
    "DEFAULT_PERMISSION_CLASSES": [],
    "UNAUTHENTICATED_USER": None,
}

DEFAULT_AUTO_FIELD = "django.db.models.BigAutoField"
USE_TZ = True
TIME_ZONE = "Asia/Tokyo"
LANGUAGE_CODE = "ja"

# Lambda では /tmp のみ書き込み可能。ログはファイルに出さず標準出力へ。
LOGGING = {
    "version": 1,
    "disable_existing_loggers": False,
    "handlers": {"console": {"class": "logging.StreamHandler"}},
    "root": {"handlers": ["console"], "level": os.environ.get("LOG_LEVEL", "INFO")},
}
