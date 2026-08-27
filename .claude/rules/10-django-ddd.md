# 10. Django — DDD 構成ルール

対象: `services/django-ddd/`
検証: `import-linter`（`.importlinter`）+ ruff + mypy

## DDD の戦術的パターン（この構成の語彙）

Laravel 側（クリーンアーキテクチャ）と方向性は同じだが、**作法が違う**。
DDD は「層を分けること」より **ドメインモデルの表現力**を重視する。
以下の用語を、そのままディレクトリと型の名前として使うこと。

| 概念 | 置き場所 | 定義 | 例 |
|---|---|---|---|
| **エンティティ** | `domain/entities/` | 同一性を持つ。値が変わっても ID が同じなら同じもの | `User` |
| **値オブジェクト** | `domain/value_objects/` | 不変。等価性は「値」で決まる。不正な値は生成させない | `Email`, `UserId`, `DisplayName` |
| **集約 / 集約ルート** | `domain/aggregates/` | **不変条件を守る単位**であり、**トランザクションの境界** | `UserAccount`（ルート）+ `Profile`（内部） |
| **ドメインサービス** | `domain/services/` | 単一の集約に属さない業務ルール。**状態を持たない** | `EmailUniquenessService` |
| **リポジトリ** | `domain/repositories/` | **集約ルート単位で 1 つ。** 集約を出し入れする契約 | `UserRepository` |
| **ドメインイベント** | 集約が保持 | 集約が起こした出来事。`pull_events()` で取り出す | `UserRegistered` |
| **ファクトリ** | 集約ルートの classmethod | 集約の生成もルートの責務 | `UserAccount.register()` |

### 集約の扱い（DDD の中核。ここを外すと DDD ではなくなる）

- **集約の外からは、集約ルート経由でしか触らない。**
  `Profile` を直接 import して書き換えないこと。変更は `UserAccount.rename()` 等を通す。
- **Repository は集約ルート単位で 1 つ。** `ProfileRepository` を作らない。
  内部エンティティは常にルートごと出し入れする。
- **1 トランザクションに複数の集約を入れない。**
  集約をまたぐ整合性は結果整合で扱う。
- **不変条件は集約が守る。** 「無効なアカウントは変更できない」のような規則を
  UseCase 側の `if` で書かない。書いた時点でドメインが貧血症になる。

### 集約とドメインサービスの使い分け

| 判定に必要なもの | 置き場所 |
|---|---|
| その集約 1 つだけで判定できる | **集約のメソッド**（例: 無効アカウントは変更不可） |
| 他の集約との関係で決まる | **ドメインサービス**（例: メールアドレスの重複） |

重複チェックを無理にエンティティのメソッドにすると、
エンティティが自分以外の集約を知ることになり、集約の境界が壊れる。

## 層構成

上記の戦術的パターンを、4 層に配置する。
**依存は必ず内側（domain）へ向かう。** 外向きの依存は存在してはならない。

```
interfaces/     ← 外部との接点（HTTP・CLI）。Django に依存してよい唯一の入口
    ↓
application/    ← ユースケース。トランザクション境界。Django ORM を知らない
    ↓
domain/         ← エンティティ・値オブジェクト・リポジトリ契約。何にも依存しない
    ↑
infrastructure/ ← domain の契約を実装する。Django ORM / Cognito / S3 はここだけ
```

`infrastructure` の矢印が上向きなのは **依存性逆転** による。
`infrastructure` は `domain` が定義した抽象を実装するので `domain` に依存するが、
`domain` は `infrastructure` を一切知らない。

## ディレクトリと責務

```
services/django-ddd/src/
├── domain/                       # 依存ゼロ。Django import 禁止
│   ├── aggregates/               # 集約ルート + 内部エンティティ（不変条件を守る単位）
│   ├── entities/                 # エンティティ（同一性を持つ）
│   ├── value_objects/            # 値オブジェクト（不変・等価性は値）
│   ├── repositories/             # リポジトリ「契約」(ABC)。**集約ルート単位で1つ**
│   ├── services/                 # ドメインサービス（単一の集約に属さない規則）
│   └── exceptions.py             # ドメイン例外
│
├── application/                  # ユースケース層
│   ├── usecases/                 # 1ユースケース1クラス。execute() のみ公開
│   ├── dto/                      # 層をまたぐデータ。dataclass(frozen=True)
│   └── ports/                    # 外部サービスの契約（認証・ストレージ等）
│
├── infrastructure/               # 実装層。ここだけが外部技術を知る
│   ├── django_orm/
│   │   ├── models.py             # Django Model。ここ以外で import 禁止
│   │   └── repositories/         # domain.repositories の実装
│   ├── cognito/                  # Cognito クライアント（application.ports の実装）
│   └── storage/                  # S3 / SeaweedFS クライアント
│
├── interfaces/                   # 外部接点
│   └── api/
│       ├── views.py              # DRF View。UseCase を呼ぶだけ
│       ├── serializers.py        # 入出力の形式変換のみ
│       └── urls.py
│
└── config/                       # Django settings / wsgi / asgi / DI 組立
```

## 依存ルール（import-linter が強制）

| 層 | import してよい |
|---|---|
| `domain` | **なし**（標準ライブラリのみ） |
| `application` | `domain` |
| `infrastructure` | `domain`, `application`, Django, boto3 |
| `interfaces` | `application`, `domain`(型のみ), Django, DRF |
| `config` | すべて（DI 組立のため） |

### 禁止事項（違反は CI で落ちる）

- ❌ `domain/` から `django.*` を import する
- ❌ `application/` から `django.*` / `infrastructure.*` を import する
- ❌ `interfaces/` から `infrastructure.*` を直接 import する（DI 経由で受け取る）
- ❌ `infrastructure/django_orm/models.py` を上記以外の層で import する
- ❌ Django Model をそのまま層をまたいで渡す（**必ず DTO / 集約に変換**）
- ❌ 集約の内部エンティティ（`Profile`）を集約の外から直接書き換える
- ❌ 内部エンティティ専用の Repository を作る（Repository は集約ルート単位）
- ❌ 集約の不変条件を UseCase 側の `if` で書く（ドメインが貧血症になる）

## 実装規約

### エンティティ・値オブジェクト
- `@dataclass` を使う。値オブジェクトは `frozen=True`。
- ビジネスルールの検証はコンストラクタ（`__post_init__`）に置く。
- **ORM の都合（`id` の自動採番、`created_at` 等）をエンティティに持ち込まない。**

### 集約
- 不変条件は `__post_init__` かルートのメソッドに置く。UseCase 側に漏らさない。
- 生成はルートの classmethod（ファクトリ）に集約する。バラバラに `new` させない。
- 出来事は `events` に積み、`pull_events()` で取り出す（取り出したら消える）。

### リポジトリ
- `domain/repositories/` には **ABC のみ**。実装を書かない。
- **集約ルート単位で 1 つ**作る。内部エンティティ用の Repository を作らない。
- 契約のシグネチャは **集約を返す**。Django Model や QuerySet を返さない。
- 実装は `infrastructure/django_orm/repositories/` に置き、
  戻り値を返す直前に Model → エンティティへ変換する。

### ユースケース
- 1 ファイル 1 ユースケース。公開メソッドは `execute()` のみ。
- 依存はすべて **コンストラクタ注入**。ユースケース内で具象を `import` しない。
- トランザクション境界はここ。ただし `django.db.transaction` を直接呼ばず、
  `application/ports/` の `UnitOfWork` 抽象を経由する。

### View
- View がやってよいのは 3 つだけ: **入力の検証 → UseCase 呼び出し → 応答の組み立て**。
- ビジネスロジックを書かない。`if` によるドメイン判定が出てきたら層を間違えている。

## 命名規約

| 対象 | 規約 | 例 |
|---|---|---|
| 集約ルート | 名詞・単数 | `UserAccount` |
| エンティティ | 名詞・単数 | `User`, `Profile`, `HealthStatus` |
| 値オブジェクト | 名詞 | `Email`, `UserId` |
| リポジトリ契約 | `<集約ルート>Repository` | `UserRepository` |
| リポジトリ実装 | `Django<Entity>Repository` | `DjangoUserRepository` |
| ユースケース | `<動詞><名詞>UseCase` | `SignInUseCase` |
| DTO | `<名詞>Dto` / `<名詞>Input` / `<名詞>Output` | `SignInInput` |
| ドメインサービス | `<名詞>Service` | `EmailUniquenessService` |
| ポート | `<名詞>Port` | `AuthenticatorPort` |
