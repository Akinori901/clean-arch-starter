# 10. Django — DDD 構成ルール

対象: `services/django-ddd/`
検証: `import-linter`（`.importlinter`）+ ruff + mypy

## 層構成

DDD の戦術的パターンに沿って 4 層に分ける。
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
│   ├── entities/                 # エンティティ（同一性を持つ）
│   ├── value_objects/            # 値オブジェクト（不変・等価性は値）
│   ├── repositories/             # リポジトリ「契約」(ABC)。実装は置かない
│   ├── services/                 # ドメインサービス（単一エンティティに属さない規則）
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
- ❌ Django Model をそのまま層をまたいで渡す（**必ず DTO / エンティティに変換**）

## 実装規約

### エンティティ・値オブジェクト
- `@dataclass` を使う。値オブジェクトは `frozen=True`。
- ビジネスルールの検証はコンストラクタ（`__post_init__`）に置く。
- **ORM の都合（`id` の自動採番、`created_at` 等）をエンティティに持ち込まない。**

### リポジトリ
- `domain/repositories/` には **ABC のみ**。実装を書かない。
- 契約のシグネチャは **エンティティを返す**。Django Model や QuerySet を返さない。
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
| エンティティ | 名詞・単数 | `User`, `HealthStatus` |
| 値オブジェクト | 名詞 | `Email`, `UserId` |
| リポジトリ契約 | `<Entity>Repository` | `UserRepository` |
| リポジトリ実装 | `Django<Entity>Repository` | `DjangoUserRepository` |
| ユースケース | `<動詞><名詞>UseCase` | `SignInUseCase` |
| DTO | `<名詞>Dto` / `<名詞>Input` / `<名詞>Output` | `SignInInput` |
| ポート | `<名詞>Port` | `AuthenticatorPort` |
