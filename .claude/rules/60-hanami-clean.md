# 60. Ruby(Hanami) — クリーンアーキテクチャ構成ルール

対象: `services/hanami-clean/`
検証: `bin/verify-layers` + RuboCop + RSpec

## なぜ Rails ではなく Hanami か

**Rails にクリーンアーキテクチャを丸ごと被せるのは主流ではなく、
アンチパターン扱いされることが多い。** Active Record は
「Model が永続化を知っている」前提の設計なので、Repository 層を挟むと
フレームワークと戦い続けることになる。
（Rails 側の主流は Shopify 発の **packwerk によるモジュラモノリス** で、
層ではなく機能パッケージで切る方式）

**Hanami は最初からクリーンアーキテクチャ寄りに作られている。**

- **DI コンテナ内蔵**（`Deps[...]`）— 依存注入が言語機能のように使える
- **ROM** — Entity と永続化が分離している（Active Record と根本的に違う）
- **Dry::Operation** — ユースケースを Result（Success/Failure）で表現する

Ruby でクリーンアーキテクチャをやるなら、フレームワークと戦わずに済むこちらを使う。

## 層構成

```
app/actions/       ← 外部接点(HTTP)。Failure のタグ → HTTP ステータス変換のみ
    ↓
app/operations/    ← ユースケース。Dry::Operation。Result を返す
    ↓
app/repos/         ← 永続化。ROM に触れてよい唯一の層。Struct → Entity 変換
app/gateways/      ← 外部サービス(Cognito / S3)。AWS SDK はここだけ
    ↓
lib/app_core/domain/  ← 最内層。素の Ruby のみ
```

## ディレクトリと責務

```
services/hanami-clean/
├── lib/app_core/domain/     # 依存ゼロ。Hanami も ROM も知らない
│   ├── entities/            # エンティティ（同一性を持つ）
│   ├── value_objects/       # 不変・等価性は値（Email / UserId / DisplayName）
│   └── errors.rb            # ドメインエラー（HTTPステータスを持ち込まない）
├── app/
│   ├── actions/             # Hanami::Action。Operation を呼ぶだけ
│   ├── operations/          # Dry::Operation。ユースケース
│   ├── repos/               # Hanami::DB::Repo。Struct → Entity 変換
│   ├── relations/           # ROM リレーション（スキーマ宣言のみ）
│   ├── structs/             # ROM Struct（**エンティティではない**）
│   └── gateways/            # Cognito / S3
└── config/providers/        # DI コンテナへの登録（具象の結線）
```

### Struct と Entity を混同しないこと

Hanami/ROM で最も間違えやすい点。

| | 意味 | 置き場所 |
|---|---|---|
| **Struct** | DB から読んだ「行」 | `app/structs/` |
| **Entity** | 業務上の「ユーザー」 | `lib/app_core/domain/entities/` |

Active Record と違い、**この 2 つは別物**。変換は Repo が行い、その境界で
永続化の都合を断ち切る。Struct を Operation より上へ出さないこと。

## 依存ルール（bin/verify-layers が強制）

| 層 | 参照してよい |
|---|---|
| `lib/app_core/domain` | **なし**（素の Ruby のみ。Hanami / ROM / AWS 禁止） |
| `app/operations` | domain、Deps 経由の repos / gateways |
| `app/repos` | domain、ROM |
| `app/gateways` | domain、AWS SDK |
| `app/actions` | domain、Deps 経由の operations |

### 禁止事項（違反は CI で落ちる）

- ❌ `domain/` で `Hanami` / `ROM` / `Aws::` / `Sequel` を参照する
- ❌ `domain/` で `include Deps` する（ドメインは DI コンテナを知らない）
- ❌ `operations/` から Relation / AWS SDK を直接触る（Deps で注入する）
- ❌ `actions/` から Repo を直接触る（Operation 経由にする）
- ❌ `operations/` が Struct を返す（Entity へ変換してから返す）

## Dry::Operation の作法

- **`call` の戻り値を自分で `Success(...)` で包まないこと。**
  `Dry::Operation#call` が自動で包むため、二重の Success になる（実際に踏んだ）。
  素の値をそのまま返す。
- 失敗は `Failure[:tag, message]` の形で返し、Action 側でタグを HTTP ステータスへ変換する。
- `step` で繋ぐと、Failure が出た時点で以降がスキップされる。

## Hanami 3 固有の注意

- **Action の gem 名は `hanami-action`**（旧 `hanami-controller`）。
  `hanami-controller` は `hanami-utils ~> 2.x` を要求するため 3.0 とは解決できない。
- バリデーションは `dry-validation`（`hanami-validations` ではない）。
- **JSON ボディを受けるには `config.middleware.use :body_parser, :json` が必要。**
  無いと params が空になり、バリデーションが必ず 422 で落ちる（実際に踏んだ）。
- `aws-sdk` は XML パーサを要求するが、Ruby 3.4 では `rexml` が標準添付から外れている。
  **Gemfile に `rexml` を明示すること**（無いと起動時に落ちる）。

## Lambda での注意

- ファイルシステムは `/tmp` 以外書き込み不可。`TMPDIR=/tmp` を設定する。
- `lambda_handler.rb` が API Gateway payload v2 を Rack env へ変換する。
