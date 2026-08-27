# clean-arch-starter

**アーキテクチャ規約を「文章」ではなく「CI で落ちる仕組み」として持つスターターテンプレート。**

Django は DDD、Laravel はクリーンアーキテクチャで構成し、
**同じ規約を機械検証ツールの設定として持つ**ことで、レビューに依存せず構造を維持します。

AI にコードを書かせる前提で設計しています。
AI は「動くコード」を最短で書こうとするため、放っておくと層を貫通します。
それを止めるのが `.claude/rules/` と CI の役割です。

| スタック | 構成 | 検証ツール |
|---|---|---|
| **Django** | DDD | import-linter |
| **Laravel** | クリーンアーキテクチャ | deptrac |
| **Go** | クリーンアーキテクチャ | go-arch-lint |
| **Hanami** (Ruby) | クリーンアーキテクチャ | `bin/verify-layers` |
| **React** | feature-sliced | eslint-plugin-boundaries |

**どの検証も「違反を注入したら実際に落ちること」を確認済みです。**
落ちないルールは、書いていないのと同じだからです。

## 何が入っているか

Cognito 認証（サインイン / 現在ユーザー取得）とヘルスチェックを、
**全層を貫通する形で**両フレームワークに実装した最小構成です。

| 対象 | ローカル | AWS |
|---|---|---|
| DB | MySQL 8.4 | RDS MySQL |
| オブジェクトストレージ | SeaweedFS（S3 API 互換） | S3 |
| 認証 | cognito-local | Cognito User Pool |
| Django / Laravel / Go / Hanami | Docker コンテナ | **Lambda**（コンテナイメージ） |
| React | Vite dev server | **S3 + CloudFront** |

ローカルと本番で**同じ SDK・同じコードパス**を通します。
差分は環境変数（エンドポイント URL）だけで吸収し、
アプリコードに `if (local)` を書きません。

## クイックスタート

```bash
make up          # 全サービス起動
make seed        # ローカル Cognito にプールとテストユーザーを作成
                 # → 出力された値を .env へ書き写す
make migrate     # DB マイグレーション
make verify      # 層検証 + 静的解析 + テスト（CI と同じ内容）
```

動作確認:

```bash
# 4サービスとも同じ API・同じ users テーブルを共有します
curl localhost:8000/api/health   # Django (DDD)
curl localhost:8001/api/health   # Laravel (クリーンアーキ)
curl localhost:8002/api/health   # Go (クリーンアーキ)
curl localhost:8003/api/health   # Hanami (クリーンアーキ)
# {"healthy":true,"components":[{"name":"database","state":"up"}, ...]}

curl -X POST localhost:8000/api/auth/sign-in \
  -H 'Content-Type: application/json' \
  -d '{"email":"taro@example.com","password":"Passw0rd!"}'
```

---

# ディレクトリ構成とその意味

## なぜ構成を規約にするのか

ディレクトリを分けること自体に意味はありません。
**意味があるのは「どの層がどの層に依存してよいか」を決め、それを守らせること**です。

層を分けても依存方向が自由なら、フォルダが増えただけの密結合になります。
だからこのリポジトリでは、依存方向を**ツールの設定ファイルとして持ちます**。

| スタック | 規約 | 検証ツール | 設定ファイル |
|---|---|---|---|
| Django | [`10-django-ddd.md`](.claude/rules/10-django-ddd.md) | import-linter | `services/django-ddd/.importlinter` |
| Laravel | [`20-laravel-clean.md`](.claude/rules/20-laravel-clean.md) | deptrac | `services/laravel-clean/depfile.yaml` |
| React | [`30-frontend.md`](.claude/rules/30-frontend.md) | eslint-plugin-boundaries | `services/frontend-react/eslint.config.js` |
| Go | [`50-go-clean.md`](.claude/rules/50-go-clean.md) | go-arch-lint | `services/go-clean/.go-arch-lint.yml` |
| Hanami | [`60-hanami-clean.md`](.claude/rules/60-hanami-clean.md) | 専用スクリプト | `services/hanami-clean/bin/verify-layers` |

**規約ドキュメントと設定ファイルは同じ内容です。**
片方だけ直すと乖離するため、必ず両方を更新してください。

## Django — DDD 構成

Laravel 側（クリーンアーキテクチャ）と方向性は同じですが、**作法が違います**。
DDD は「層を分けること」より**ドメインモデルの表現力**を重視するため、
以下の戦術的パターンを、そのままディレクトリと型の名前として使います。

| 概念 | 置き場所 | 定義 | 例 |
|---|---|---|---|
| **集約 / 集約ルート** | `domain/aggregates/` | **不変条件を守る単位**、**トランザクションの境界** | `UserAccount` + `Profile` |
| **エンティティ** | `domain/entities/` | 同一性を持つ。値が変わっても ID が同じなら同じもの | `User` |
| **値オブジェクト** | `domain/value_objects/` | 不変。等価性は値。不正な値は生成させない | `Email`, `DisplayName` |
| **ドメインサービス** | `domain/services/` | 単一の集約に属さない業務ルール。状態を持たない | `EmailUniquenessService` |
| **リポジトリ** | `domain/repositories/` | **集約ルート単位で 1 つ** | `UserRepository` |
| **ドメインイベント** | 集約が保持 | 集約が起こした出来事 | `UserRegistered` |

```
services/django-ddd/src/
├── domain/            # 依存ゼロ。Django すら import しない
│   ├── aggregates/        # 集約ルート + 内部エンティティ（UserAccount, Profile）
│   ├── entities/          # 同一性を持つ（User, HealthStatus）
│   ├── value_objects/     # 不変・等価性は値（Email, UserId, DisplayName）
│   ├── repositories/      # 集約ルート単位の「契約」(ABC)。実装は置かない
│   ├── services/          # ドメインサービス（EmailUniquenessService）
│   └── exceptions.py      # ドメイン例外（HTTPステータスを持ち込まない）
│
├── application/       # ユースケース層。Django ORM を知らない
│   ├── usecases/          # 1ファイル1ユースケース。公開は execute() のみ
│   ├── dto/               # 層をまたぐデータ。frozen dataclass
│   └── ports/             # 外部サービスの契約（認証・ストレージ・UoW）
│
├── infrastructure/    # 実装層。ここだけが外部技術を知る
│   ├── django_orm/        # Django Model と Repository 実装
│   ├── cognito/           # Cognito クライアント
│   └── health/            # 各依存の疎通確認
│
├── interfaces/        # 外部接点
│   └── api/               # DRF View・Serializer・URL
│
└── config/            # settings / DI 組立
```

**依存は必ず内側（domain）へ向かいます。**

```
interfaces  →  application  →  domain
                                 ↑
                          infrastructure
```

`infrastructure` の矢印が上向きなのは**依存性逆転**によるものです。
`infrastructure` は `domain` が定義した抽象を実装するので `domain` に依存しますが、
`domain` は `infrastructure` を一切知りません。

### この構成の実利

```bash
$ pytest tests
29 passed in 0.03s
```

**DB もフレームワークも AWS も無しで、29 件のテストが 0.03 秒で終わります。**
ドメインのテストに Django のセットアップが要らないのが、層を分けた見返りです。

### 主要な禁止事項

| ❌ 禁止 | なぜ |
|---|---|
| `domain/` から `django.*` を import | ドメインが Django を知った時点で、それはもうドメインではない |
| `application/` から `infrastructure.*` を import | ユースケースは「何をするか」で、「どう保存するか」ではない |
| Django Model を層をまたいで渡す | ORM の都合が全層へ伝播する。必ず DTO / 集約へ変換する |
| Dto に `fromModel()` を置く | Dto が Model に依存し、依存グラフの末端でなくなる |
| 内部エンティティ（`Profile`）を集約の外から直接 import | ルートが守る不変条件を迂回できてしまう |
| 内部エンティティ専用の Repository を作る | Repository は**集約ルート単位で 1 つ** |
| 集約の不変条件を UseCase の `if` で書く | ドメインが貧血症になる。規則は集約が持つ |

### 集約とドメインサービスの使い分け

| 判定に必要なもの | 置き場所 | 例 |
|---|---|---|
| その集約 1 つだけで判定できる | **集約のメソッド** | 無効なアカウントは変更不可 |
| 他の集約との関係で決まる | **ドメインサービス** | メールアドレスの重複チェック |

重複チェックを無理にエンティティのメソッドにすると、
エンティティが自分以外の集約を知ることになり、集約の境界が壊れます。

## Laravel — クリーンアーキテクチャ構成

```
Request(FormRequest)
  → Controller（UseCase へ渡す・例外catch・出し分けのみ）
      → UseCase（Service のオーケストレーション・トランザクション境界）
          → Service（ビジネスロジック。Service 間の相互呼び出し禁止）
              → RepositoryInterface（Model ではなく Dto を返す契約）
                  → Repository（Model を use する唯一の層）
                      → Model
          → Helper（Model/DB 非依存の純粋関数）
  ← Formatter（配列/文字列を返す。JsonResponse は返さない）
  ← Response（HTTP レスポンス生成）

Dto（Model に一切依存しない末端ノード）
```

要点は 3 つです。

- **Model を直接触れるのは Repository だけ。** 他の層が扱うのは Dto。
- **Dto は Model に依存しない。** 変換は Repository 内で完結させる。
- **Service 間の相互呼び出しを禁止。** 複数 Service の調整は UseCase の仕事。

`Repository` と `RepositoryInterface` は同じディレクトリに同居させるため、
deptrac は**ディレクトリではなくクラス名サフィックス**で層を判別します。

## Go — クリーンアーキテクチャ構成

Go コミュニティでは DDD より**クリーンアーキテクチャ**が主流です
（`go-clean-arch` 10k★・`go-clean-template` 7.6k★）。
`internal/` によるパッケージ境界の強制が言語機能として効くため相性がよく、
参照実装もクリーンアーキ寄りに揃っています。

```
services/go-clean/
├── cmd/app/                 # main。HTTPサーバ / Lambda の切り替えのみ
├── internal/
│   ├── entity/              # 最内層。標準ライブラリのみ
│   ├── usecase/             # ユースケース + 契約(interface)の定義
│   ├── repo/                # MySQL / Cognito / S3 の実装
│   ├── controller/http/     # chi ルータ
│   └── app/                 # DI の結線（具象を知ってよい唯一の場所）
└── pkg/                     # 公開してよい汎用部品のみ
```

### Go 特有の作法

- **インターフェースは「使う側」で定義する。**
  Repository の契約は `repo` ではなく `usecase` に置き、`repo` がそれを満たします。
  これが Go における依存性逆転の書き方です。
- **`repo` から `usecase` を import しない。**
  共有したい型が出てきたら `entity` へ置きます
  （実際にこれを踏み、`AuthTokens` を `entity` へ移しました）。
- `internal/` は Go が import を禁じてくれるため、境界の強制に言語機能を使えます。

## Hanami — クリーンアーキテクチャ構成（Ruby）

**Rails ではなく Hanami を採用しています。**

Rails にクリーンアーキテクチャを丸ごと被せるのは主流ではなく、
アンチパターン扱いされることが多い構成です。Active Record が
「Model が永続化を知っている」前提のため、Repository 層を挟むと
フレームワークと戦い続けることになります。

Hanami は最初からクリーンアーキテクチャ寄りに設計されています。

- **DI コンテナ内蔵**（`Deps[...]`）
- **ROM** — Entity と永続化が分離（Active Record と根本的に違う）
- **Dry::Operation** — ユースケースを Result（Success/Failure）で表現

```
services/hanami-clean/
├── lib/app_core/domain/     # 最内層。素の Ruby のみ（Hanami も ROM も知らない）
│   ├── entities/
│   ├── value_objects/
│   └── errors.rb
└── app/
    ├── actions/             # Failure のタグ → HTTP ステータス変換のみ
    ├── operations/          # Dry::Operation。ユースケース
    ├── repos/               # ROM に触れてよい唯一の層。Struct → Entity 変換
    ├── relations/           # ROM リレーション（スキーマ宣言のみ）
    ├── structs/             # ROM Struct（**エンティティではない**）
    └── gateways/            # Cognito / S3
```

### Struct と Entity を混同しない

Hanami/ROM で最も間違えやすい点です。

| | 意味 | 置き場所 |
|---|---|---|
| **Struct** | DB から読んだ「行」 | `app/structs/` |
| **Entity** | 業務上の「ユーザー」 | `lib/app_core/domain/entities/` |

Active Record と違い、**この2つは別物**です。変換は Repo が行い、
その境界で永続化の都合を断ち切ります。

```bash
$ bundle exec rspec spec/domain
21 examples, 0 failures in 0.018s
```

**Hanami を起動せず、DB も無しで 21 件が 0.018 秒**で終わります。

## React — feature-sliced 構成

```
src/
├── app/        # 組立点。ルーティングと Provider のみ
├── features/   # 機能単位。ここが主戦場
│   └── <feature>/{api,components,hooks}
├── shared/     # 複数 feature で共有するもののみ
└── config/     # 環境変数の読み込み（ここ以外で import.meta.env を読まない）
```

- `shared/` が `features/` を参照したら、それはもう共有物ではない
- `features/A` から `features/B` の内部を参照しない（必要なら `shared/` へ引き上げる）

---

# AI 駆動開発での使い方

## `.claude/rules/` が制御するもの

このリポジトリは **AI にコードを書かせる前提**で設計されています。

AI は与えられたタスクを最短で満たそうとするため、
「UseCase から Model を直接呼べば動く」場面では、実際にそう書きます。
**それを人間のレビューだけで止め続けるのは現実的ではありません。**

そこで二重に縛ります。

### 1. 実装前に読ませる（`.claude/rules/`）

| ファイル | 適用範囲 |
|---|---|
| [`00-core.md`](.claude/rules/00-core.md) | 全体。**他のどの指示より優先される** |
| [`10-django-ddd.md`](.claude/rules/10-django-ddd.md) | Django を触るとき |
| [`20-laravel-clean.md`](.claude/rules/20-laravel-clean.md) | Laravel を触るとき |
| [`30-frontend.md`](.claude/rules/30-frontend.md) | React を触るとき |
| [`40-infra-cd.md`](.claude/rules/40-infra-cd.md) | インフラ / CD を触るとき |

ルールには**層ごとの責務・命名規約・禁止事項**が書かれています。
AI は新しいファイルを作る前に、**そのファイルがどの層に属するかを宣言**します。
層が決まらないファイルは、まだ設計が終わっていないということです。

### 2. 書かせた後に落とす（CI）

ルールを読ませても、AI は時々破ります。**破ったら CI が落ちます。**

```bash
$ lint-imports --config .importlinter
domain は外部技術に依存しない BROKEN

domain is not allowed to import django:
-   domain.entities.user -> django.db (l.12)
```

この検証は実際に機能することを確認済みです。
`domain/entities/user.py` に `from django.db import models` を 1 行足すと、
上記のとおり contract が BROKEN になり、削除すると KEPT に戻ります。

同様に、Laravel 側で `Service` から `Model` を use すると deptrac が落ちます。

```
App\Services\AuthService must not depend on App\Models\User
Violations: 1
```

**「ルールを書いたが誰も守っていない」状態にならないのは、この仕組みがあるからです。**

### 規約を曲げるとき

規約どおりに書けない場面は必ず出ます。このリポジトリの立場は
「例外を作るな」ではなく、**例外を作った理由を追跡可能にしろ**です。

- 既存の違反 → ベースライン（`skip_violations` / `ignore_imports`）に登録してよい
- **新規の違反 → 追加しない。** 該当箇所を正しい層へ移す
- 登録するときは**同じ行に理由をコメントで書く**

こうするとベースラインが「減らしていく対象」になり、逃げ道になりません。

---

# CD

`main` への push で本番デプロイが走ります。

```
verify（層検証）→ backend（ECRへpush）→ infra（SAM deploy）→ frontend（S3同期+invalidation）
```

**層検証が落ちたらデプロイへ進みません**（`needs: verify`）。

- [`.github/workflows/verify.yml`](.github/workflows/verify.yml) — 層検証・静的解析・テスト
- [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) — デプロイ本体
- [`infra/sam/template.yaml`](infra/sam/template.yaml) — Lambda / API Gateway / Cognito / CloudFront

AWS 認証は **OIDC のみ**。アクセスキーを GitHub Secrets に置きません。

**Secrets が未設定の環境では、デプロイ系ジョブは自動的にスキップされます**
（`verify` は常に実行されます）。clone しただけで CI が赤くならないようにするためです。
実際にデプロイするには、以下を GitHub Secrets に設定してください。

| Secret | 用途 |
|---|---|
| `AWS_DEPLOY_ROLE_ARN` | OIDC で引き受けるデプロイ用ロール |
| `DB_HOST` | RDS のエンドポイント |
| `DB_SECRET_ARN` | DB 認証情報の Secrets Manager ARN |
| `VPC_SUBNET_IDS` | Lambda を置くサブネット |
| `LAMBDA_SG_ID` | Lambda のセキュリティグループ |

## Terraform 指定の案件では

本リポジトリの正は SAM ですが、案件によって Terraform 指定は多くあります。
**移植の対応表・併用時の境界の引き方**をまとめてあります。

→ [docs/iac-sam-vs-terraform.md](docs/iac-sam-vs-terraform.md)

要点だけ:

- **同じリソースを 2 つの IaC で管理しない。** Terraform=土台（VPC/RDS/Cognito）、SAM=アプリ層（Lambda/APIGW）
- 受け渡しは **SSM Parameter Store 経由**。ARN を手でコピペしない
- `terraform apply` を CI で自動実行しない。**必ず `plan` を人が見てから**

土台側の Terraform サンプルは [`infra/terraform/`](infra/terraform/) にあります。

## ライセンス

MIT
