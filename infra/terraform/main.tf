# 土台（寿命の長いリソース）だけを Terraform で持つ構成。
# アプリ層（Lambda / API Gateway）は SAM 側が管理する。
#
# 同じリソースを 2 つの IaC で管理しないこと。境界は SSM で受け渡す。

terraform {
  required_version = ">= 1.9"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region = var.region
}

locals {
  name = "${var.project}-${var.env}"
  tags = {
    Project   = var.project
    Env       = var.env
    ManagedBy = "terraform"
  }
}

# ── コンテナイメージの置き場 ──────────────────────────────────
resource "aws_ecr_repository" "django" {
  name                 = "${local.name}-django"
  image_tag_mutability = "IMMUTABLE" # 同じタグの上書きを禁じ、何が動いているかを追える状態にする
  image_scanning_configuration {
    scan_on_push = true
  }
  tags = local.tags
}

resource "aws_ecr_repository" "laravel" {
  name                 = "${local.name}-laravel"
  image_tag_mutability = "IMMUTABLE"
  image_scanning_configuration {
    scan_on_push = true
  }
  tags = local.tags
}

# ── 認証 ────────────────────────────────────────────────────
resource "aws_cognito_user_pool" "main" {
  name                     = "${local.name}-users"
  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]

  password_policy {
    minimum_length    = 12
    require_uppercase = true
    require_lowercase = true
    require_numbers   = true
    require_symbols   = true
  }

  deletion_protection = "ACTIVE"

  lifecycle {
    # User Pool は消えるとユーザーが復旧できない。
    # plan を読み違えて destroy が通る事故を型で止める。
    prevent_destroy = true
  }

  tags = local.tags
}

resource "aws_cognito_user_pool_client" "web" {
  name         = "${local.name}-web"
  user_pool_id = aws_cognito_user_pool.main.id

  # SPA なのでシークレットを持たせない（ブラウザでは隠せないため）
  generate_secret = false

  explicit_auth_flows = [
    "ALLOW_USER_PASSWORD_AUTH",
    "ALLOW_REFRESH_TOKEN_AUTH",
  ]

  allowed_oauth_flows                  = ["code"] # implicit は使わない
  allowed_oauth_scopes                 = ["openid", "email", "profile"]
  allowed_oauth_flows_user_pool_client = true
  supported_identity_providers         = ["COGNITO"]
  callback_urls                        = var.callback_urls
  logout_urls                          = var.callback_urls
}

# ── SAM へ渡す値を SSM へ書き出す ─────────────────────────────
# ARN を手でコピペして SAM テンプレートへ直書きしないこと。
# Terraform 側で作り直したときに静かに壊れる。
resource "aws_ssm_parameter" "cognito_user_pool_id" {
  name  = "/${var.project}/${var.env}/cognito_user_pool_id"
  type  = "String"
  value = aws_cognito_user_pool.main.id
  tags  = local.tags
}

resource "aws_ssm_parameter" "cognito_client_id" {
  name  = "/${var.project}/${var.env}/cognito_client_id"
  type  = "String"
  value = aws_cognito_user_pool_client.web.id
  tags  = local.tags
}
