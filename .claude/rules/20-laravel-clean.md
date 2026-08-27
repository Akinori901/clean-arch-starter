# 20. Laravel — クリーンアーキテクチャ構成ルール

対象: `services/laravel-clean/`
検証: `deptrac`（`depfile.yaml`）+ PHPStan

> この規約は [clean-arch-laravel](https://github.com/Akinori901/clean-arch-laravel) を
> 本リポジトリのサンプル（Cognito 認証・ヘルスチェック）に合わせて具体化したもの。

## 層構成

```
Request(FormRequest)
  → Controller（UseCase へ渡す・例外catch・出し分けのみ）
      → UseCase（Service のオーケストレーション・トランザクション境界）
          → Service（ビジネスロジック。Service 間の相互呼び出し禁止）
              → RepositoryInterface（Model ではなく Dto を返す契約）
                  → Repository（Model を use する唯一の層。返す直前に Dto へ変換）
                      → Model
          → Helper（Model/DB 非依存の純粋関数）
  ← Formatter（配列/文字列を返す。JsonResponse は返さない）
  ← Response（HTTP レスポンス生成）

Dto（Model に一切依存しない末端ノード。全層から参照可能）
```

## ディレクトリと責務

```
services/laravel-clean/app/
├── Http/
│   ├── Controllers/       # UseCase 呼び出しと例外の出し分けのみ
│   ├── Requests/          # バリデーション。依存先なし（末端）
│   ├── Formatters/        # 配列/文字列を返す。JsonResponse を返さない
│   └── Responses/         # HTTP レスポンス生成
├── UseCases/              # Service のオーケストレーション・トランザクション境界
├── Services/              # ビジネスロジック。Service 間の相互呼び出し禁止
├── Repositories/          # Interface と実装を同居させる（命名で層を判別）
├── Models/                # Eloquent。Repository 以外から触らない
├── DataTransferObjects/   # Dto。Model に依存しない末端ノード
├── Helpers/               # Model/DB 非依存の純粋関数
├── Enums/                 # backed enum
└── Exceptions/
```

## 依存ルール（deptrac が強制）

| 層 | 依存してよい層 |
|---|---|
| Controller | UseCase, Request, Formatter, Response, Exception, Enum |
| UseCase | Service, Dto, Exception, Enum |
| Service | RepositoryInterface, Helper, Dto, Exception, Enum |
| Repository | RepositoryInterface, Model, Dto, Enum |
| RepositoryInterface | Dto, Enum |
| Formatter | Model, Enum |
| Helper | Enum |
| Model | Enum |
| Request / Dto / Enum | **なし（末端）** |

### 3つの要点

- **Model を直接触れるのは Repository だけ。** 他の層が扱うのは Dto。
  ORM の都合がビジネスロジックへ漏れるのを、層の定義そのもので止める。
- **Dto は Model に依存しない。** `fromModel()` を Dto に置かない。
  変換は Repository 内で完結させ、Dto を依存グラフの末端に保つ。
- **Service 間の相互呼び出しを禁止。** 複数 Service の調整は UseCase の仕事。

### 禁止事項（違反は CI で落ちる）

- ❌ Controller / UseCase / Service から `App\Models\*` を use する
- ❌ Dto から Model を参照する（`fromModel()` を Dto に置かない）
- ❌ Service が別の Service を呼ぶ
- ❌ Repository が Eloquent Model / Collection をそのまま返す
- ❌ Formatter が `JsonResponse` を返す

## Repository の命名判別

Interface と実装を同じディレクトリに同居させる規約のため、
deptrac は **ディレクトリではなくクラス名サフィックス**で層を判別する。

- 契約: `App\Repositories\UserRepositoryInterface`
- 実装: `App\Repositories\UserRepository`（`Interface` を否定後読みで除外）

## ベースライン運用

既存コードに後から入れる場合、`skip_violations` に既知違反を登録して始めてよい。
重要なのは運用ルールの方。

- 既存の違反 → ベースラインに登録してよい
- **新規の違反 → 追加しない。** 該当箇所を正しい層へ移す

## Lambda（Bref）での注意

- ファイルシステムは `/tmp` 以外書き込み不可。`storage/` は `/tmp` へ逃がす。
- セッション/キャッシュはファイルドライバを使わない（Cognito JWT はステートレス検証）。
