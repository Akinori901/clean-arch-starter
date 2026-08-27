# 規約検証・開発環境のエントリポイント。
#
# CI（.github/workflows/verify.yml）と同じコマンドをローカルでも流せるようにする。
# 「CI でだけ落ちる」状態を作らないため。
.DEFAULT_GOAL := help
.PHONY: help up down logs seed verify verify-django verify-laravel verify-front fmt migrate clean

DC := docker compose

help: ## このヘルプを表示
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
	  | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-16s\033[0m %s\n", $$1, $$2}'

## ── 開発環境 ─────────────────────────────────────────────
up: ## 全サービスを起動（初回は seed も実行すること）
	$(DC) up -d
	# バケット作成は冪等。ボリュームを消した後の起動でも確実に作られるようにする。
	$(DC) up storage-init
	@echo "  Django   : http://localhost:8000/api/health"
	@echo "  Laravel  : http://localhost:8001/api/health"
	@echo "  Frontend : http://localhost:5173"

down: ## 停止（ボリュームは残す）
	$(DC) down

clean: ## 停止してボリュームも削除（DBを初期化する）
	$(DC) down -v --remove-orphans

logs: ## ログを追う
	$(DC) logs -f

seed: ## ローカル Cognito にプールとテストユーザーを作る
	$(DC) up -d cognito storage
	$(DC) run --rm --entrypoint bash \
	  -e COGNITO_ENDPOINT_URL=http://cognito:9229 \
	  -v $(PWD)/scripts:/s storage-init /s/seed-cognito.sh

migrate: ## Django のマイグレーション
	$(DC) run --rm django python manage.py migrate

## ── 規約検証（CI と同じ内容）──────────────────────────────
verify: verify-django verify-laravel verify-front ## 全スタックの層検証 + 静的解析 + テスト

verify-django: ## Django: 層検証(import-linter) + ruff + mypy + pytest
	@echo "==> Django DDD 層検証"
	$(DC) run --rm -w /app/src django lint-imports --config /app/.importlinter
	$(DC) run --rm django ruff check src tests
	$(DC) run --rm django mypy src
	$(DC) run --rm django pytest tests

verify-laravel: ## Laravel: 層検証(deptrac) + PHPStan + PHPUnit
	@echo "==> Laravel クリーンアーキ層検証"
	$(DC) run --rm laravel ./vendor/bin/deptrac analyse --config-file=depfile.yaml
	$(DC) run --rm laravel ./vendor/bin/phpstan analyse --no-progress
	$(DC) run --rm laravel ./vendor/bin/phpunit --testsuite Unit

verify-front: ## Frontend: 境界検証(eslint-boundaries) + 型検査
	@echo "==> フロント境界検証"
	$(DC) run --rm frontend npm run lint
	$(DC) run --rm frontend npm run typecheck

fmt: ## フォーマット
	$(DC) run --rm django ruff format src tests
	$(DC) run --rm laravel ./vendor/bin/pint
