# Django (DDD) — 生成手順

対象: `services/django-ddd/`
ルール本文: `.claude/rules/10-django-ddd.md`（**先に読むこと**）
検証: `make verify-django` / `.importlinter`

このファイルは **ルールに書かれていない「どう作るか」** だけを扱う。
層の定義・禁止事項・命名表はルール本文が正本。

## 雛形にする既存ファイル

新規に書く前に、必ず同種の既存ファイルを読む。

| 作るもの | 雛形 |
|---|---|
| 値オブジェクト | `src/domain/value_objects/email.py` |
| エンティティ | `src/domain/entities/user.py` |
| 集約ルート | `src/domain/aggregates/user_account.py` |
| ドメインサービス | `src/domain/services/email_uniqueness_service.py` |
| リポジトリ契約 | `src/domain/repositories/user_repository.py` |
| ポート | `src/application/ports/authenticator.py` |
| ユースケース | `src/application/usecases/sign_in.py` |
| DTO | `src/application/dto/auth_dto.py` |
| リポジトリ実装 | `src/infrastructure/django_orm/repositories/django_user_repository.py` |
| View | `src/interfaces/api/views.py` |
| DI 結線 | `src/config/container.py` |
| ドメインのテスト | `tests/domain/test_user_account.py` |
| ユースケースのテスト | `tests/application/test_sign_in.py`（Fake は `tests/application/fakes.py`） |

## 生成順序

```
1. domain/value_objects/    不変・frozen=True・__post_init__ で検証
2. domain/entities/         同一性を持つ。@dataclass
3. domain/aggregates/       不変条件はここ。生成は classmethod（ファクトリ）
4. domain/repositories/     ABC のみ。集約ルート単位で1つ。戻り値は集約
5. application/ports/       外部サービスの契約（認証・ストレージ・UnitOfWork）
6. application/dto/         dataclass(frozen=True)
7. application/usecases/    execute() のみ公開。依存はコンストラクタ注入
8. infrastructure/          Model → エンティティ変換はここで完結
9. interfaces/api/          serializers → views → urls
10. config/container.py     具象の結線
11. tests/
```

## import-linter の契約を理解しておく

`.importlinter` には 6 つの契約がある。**どれに引っかかったかで直し方が変わる。**

| 契約名 | 落ちたときの意味 | 直し方 |
|---|---|---|
| `layers` | 層順に逆行する import | 依存の向きを直す。DI で受け取る形にする |
| `domain-purity` | domain が Django/boto3/他層を import した | その処理は domain の仕事ではない。上の層へ出す |
| `application-purity` | application が Django/infrastructure を import した | 契約（Port / Repository）を切って注入する |
| `interfaces-no-infra` | View が具象を直接 import した | `config.container` 経由で受け取る |
| `aggregate-boundary` | 集約の内部エンティティ（`Profile`）を外から触った | 集約ルートのメソッドを追加してそれを呼ぶ |
| `model-containment` | Django Model が infrastructure の外へ漏れた | Repository 内で DTO / エンティティへ変換する |

**新しい集約の内部エンティティを追加したら、`aggregate-boundary` の
`forbidden_modules` にそのモジュールを追加すること。** 追加しないと検証されない
（書いていないルールは、無いのと同じ）。

## 集約を扱うときの注意

DDD の中核。ここを外すと「ディレクトリだけ DDD」になる。

- **新しい Repository を作る前に、それが集約ルートか確認する。**
  内部エンティティ用の Repository（`ProfileRepository`）は作らない。
- **不変条件は集約に置く。** ユースケースに `if account.is_active:` と書き始めたら、
  それは集約のメソッドにすべき判定。
- 判定に**他の集約が必要**なら、集約ではなく `domain/services/` のドメインサービス。
  （例: メールアドレスの重複は他ユーザーを知る必要があるのでドメインサービス）
- 集約が起こした出来事は `events` に積み、`pull_events()` で取り出す（取り出すと消える）。

## 実装上の作法

- `from __future__ import annotations` を先頭に置く（既存ファイルすべてがそう）。
- 値オブジェクトは `@dataclass(frozen=True)`、検証は `__post_init__`。
- **ORM の都合（自動採番 id・created_at）をエンティティに持ち込まない。**
- トランザクションは `django.db.transaction` を直接呼ばず、
  `application/ports/` の `UnitOfWork` 抽象を経由する。
- View がやってよいのは **入力検証 → UseCase 呼び出し → 応答組み立て** の3つだけ。

## テスト

- `tests/domain/` — 依存ゼロ。DB を使わない。
- `tests/application/` — `tests/application/fakes.py` の Fake を使う。
  **Django も DB も使わない。** 使いたくなったら依存が漏れている合図。

## users テーブルは共有物

`users` は **Django が所有**し、他スタック（Laravel / Go / Hanami / .NET）が
**同じ行を共有する**。

- **スキーマを変更するときは、他スタックへの影響を必ず確認・報告する。**
- マイグレーションを持っているのは Django だけ。他スタックは既存テーブルへマップするだけ。

## 検証

```bash
make verify-django
# 内訳: lint-imports（層）→ ruff → mypy → pytest
```

単体で流すとき（コンテナ内）は `PYTHONPATH=src lint-imports --config .importlinter`。
