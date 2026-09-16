# Hanami (クリーンアーキテクチャ) — 生成手順

対象: `services/hanami-clean/`
ルール本文: `.claude/rules/60-hanami-clean.md`（**先に読むこと**）
検証: `make verify-hanami` / `bin/verify-layers`

## 雛形にする既存ファイル

| 作るもの | 雛形 |
|---|---|
| 値オブジェクト・エンティティ | `lib/app_core/domain/` 配下（`spec/domain/` のテストから辿る） |
| ドメインエラー | `lib/app_core/domain/errors.rb` |
| Operation（ユースケース） | `app/operations/auth/sign_in.rb` |
| Repo | `app/repos/user_repo.rb` |
| Relation | `app/relations/users.rb` |
| Struct | `app/structs/user.rb` |
| Gateway | `app/gateways/cognito_authenticator.rb` |
| Action | `app/actions/auth/sign_in.rb` |
| DI 登録 | `config/providers/gateways.rb` |
| ルーティング | `config/routes.rb` |
| テスト | `spec/domain/user_spec.rb` |

## 生成順序

```
1. lib/app_core/domain/value_objects/   素の Ruby のみ
2. lib/app_core/domain/entities/
3. lib/app_core/domain/errors.rb        HTTP ステータスを持ち込まない
4. app/relations/                       ROM リレーション（スキーマ宣言のみ）
5. app/structs/                         ROM Struct（**エンティティではない**）
6. app/repos/                           Struct → Entity 変換はここ
7. app/gateways/                        Cognito / S3。AWS SDK はここだけ
8. app/operations/                      Dry::Operation。Result を返す
9. app/actions/                         Failure のタグ → HTTP ステータス変換
10. config/routes.rb / config/providers/
11. spec/
```

## Struct と Entity を混同しない（最頻出のミス）

Hanami/ROM で最も間違えやすい点。Active Record と違い、**この 2 つは別物**。

| | 意味 | 置き場所 |
|---|---|---|
| **Struct** | DB から読んだ「行」 | `app/structs/` |
| **Entity** | 業務上の「ユーザー」 | `lib/app_core/domain/entities/` |

変換は **Repo が行い、その境界で永続化の都合を断ち切る**。
`bin/verify-layers` は「operations が `AppCore::Structs::` を参照していないか」を見ている。
**Struct を Operation より上へ出さない。**

## Dry::Operation の作法

- **`call` の戻り値を自分で `Success(...)` で包まない。**
  `Dry::Operation#call` が自動で包むため、二重の Success になる（実際に踏んだ）。
  **素の値をそのまま返す。**
- 失敗は `Failure[:tag, message]` の形で返し、Action 側でタグを HTTP ステータスへ変換する。
- `step` で繋ぐと、Failure が出た時点で以降がスキップされる。

## 依存注入は `Deps[...]`

Hanami の DI コンテナを使う。**ただし domain は `include Deps` してはならない**
（ドメインが DI コンテナを知ってはいけない。`bin/verify-layers` が検知する）。

- `operations` は Deps 経由で repos / gateways を受け取る
- `actions` は Deps 経由で operations を受け取る（**Repo を直接触らない**）

## bin/verify-layers に新しいルールを足す

このチェッカは**正規表現ベース**で、`RULES` 配列に宣言を足す形で拡張する。

新しい層や禁止パターンを追加したら:

1. `bin/verify-layers` の `RULES` に追加
2. `.claude/rules/60-hanami-clean.md` の依存表も更新
3. **違反コードをわざと書いて、実際に落ちることを確認する**
   （落ちないルールは、書いていないのと同じ）

## Hanami 3 固有の注意（すべて実際に踏んだもの）

- **Action の gem 名は `hanami-action`**（旧 `hanami-controller`）。
  `hanami-controller` は `hanami-utils ~> 2.x` を要求し 3.0 と解決できない。
- バリデーションは `dry-validation`（`hanami-validations` ではない）。
- **JSON ボディを受けるには `config.middleware.use :body_parser, :json` が必要。**
  無いと params が空になり、バリデーションが必ず 422 で落ちる。
- `aws-sdk` は XML パーサを要求するが、Ruby 3.4 では `rexml` が標準添付から外れている。
  **Gemfile に `rexml` を明示する**（無いと起動時に落ちる）。

## Lambda での注意

- `/tmp` 以外書き込み不可。`TMPDIR=/tmp` を設定する。
- `lambda_handler.rb` が API Gateway payload v2 を Rack env へ変換する。

## 検証

```bash
make verify-hanami
# 内訳: bin/verify-layers（層）→ RuboCop → RSpec（spec/domain）
```
