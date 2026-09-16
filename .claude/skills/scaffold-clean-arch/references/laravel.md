# Laravel (クリーンアーキテクチャ) — 生成手順

対象: `services/laravel-clean/`
ルール本文: `.claude/rules/20-laravel-clean.md`（**先に読むこと**）
検証: `make verify-laravel` / `depfile.yaml`

## 雛形にする既存ファイル

| 作るもの | 雛形 |
|---|---|
| Controller | `app/Http/Controllers/AuthController.php` |
| FormRequest | `app/Http/Requests/SignInRequest.php` |
| Formatter | `app/Http/Formatters/UserFormatter.php` |
| Response | `app/Http/Responses/JsonApiResponse.php` |
| UseCase | `app/UseCases/SignInUseCase.php` |
| Service | `app/Services/AuthService.php` |
| Repository 契約 | `app/Repositories/UserRepositoryInterface.php` |
| Repository 実装 | `app/Repositories/UserRepository.php` |
| Dto | `app/DataTransferObjects/UserDto.php` |
| Helper | `app/Helpers/DisplayNameHelper.php` |
| Enum | `app/Enums/ComponentState.php` |
| DI 結線 | `app/Providers/DomainServiceProvider.php` |
| テスト | `tests/Unit/SignInUseCaseTest.php` |

## 生成順序

```
1. app/Enums/                  backed enum（末端）
2. app/DataTransferObjects/    Dto（末端。Model に依存しない）
3. app/Exceptions/
4. app/Repositories/XxxRepositoryInterface.php   契約。戻り値は Dto
5. app/Repositories/XxxRepository.php            実装。Model を扱う唯一の層
6. app/Services/                                 ビジネスロジック
7. app/UseCases/                                 Service の調整・トランザクション境界
8. app/Http/Requests/                            バリデーション
9. app/Http/Formatters/                          配列/文字列を返す
10. app/Http/Controllers/                        UseCase 呼び出しと例外の出し分け
11. routes/api.php
12. app/Providers/DomainServiceProvider.php      契約 → 実装の結線
13. tests/Unit/
```

## 命名が検証そのもの（最重要）

deptrac は Repository を **ディレクトリではなくクラス名サフィックス**で判別している。

```yaml
RepositoryInterface: '^App\Repositories\.*RepositoryInterface$'
Repository:          '^App\Repositories\.*(?<!Interface)Repository$'
```

- 契約は必ず `...RepositoryInterface` で終える
- 実装は必ず `...Repository` で終える（`Interface` は否定後読みで除外される）
- **`UserRepositoryImpl` のような別の語尾を使うと、どの層にも属さず検証をすり抜ける。**
  すり抜けたコードは「検証されていない」という点で、違反より危険。

## 3 つの要点（これを外すと構成の意味が消える）

1. **Model を触れるのは Repository だけ。** 他の層が扱うのは Dto。
2. **Dto は Model に依存しない。** `fromModel()` を Dto に置かない。
   変換は Repository 内で完結させ、Dto を依存グラフの末端に保つ。
3. **Service 間の相互呼び出し禁止。** 複数 Service の調整は UseCase の仕事。

## Provider は結線だけ

`DomainServiceProvider` は契約と実装の両方を知ってよい唯一の層（deptrac でそう定義してある）。
**ここを緩めているぶん、結線以外を書かない。** ロジックが混ざると層の穴になる。

## 新しい層を足したくなったら

`depfile.yaml` の `layers` と `ruleset` の両方に追加が必要。
**ただし、追加する前に既存の層で表現できないか考える。**
層が増えるほど「どこに置くか」の判断が難しくなり、規約としての力が落ちる。

追加する場合は `.claude/rules/20-laravel-clean.md` の依存表も同時に更新する
（表と設定が食い違うと、どちらが正本か分からなくなる）。

## ベースライン

`skip_violations` は現在**空**。これは「完全準拠している」という状態を表す。

- 既存コードを移行するとき以外、**登録しない**
- 新規コードの違反を登録して通すのは禁止。層を直す

## Lambda（Bref）での注意

- `/tmp` 以外書き込み不可。`storage/` は `/tmp` へ逃がす。
- セッション/キャッシュにファイルドライバを使わない（Cognito JWT はステートレス検証）。

## users テーブル

Django が所有するテーブルを**共有している**。マイグレーションを作らない。
Eloquent は既存テーブルへマップするだけ。

## 検証

```bash
make verify-laravel
# 内訳: deptrac（層）→ PHPStan → PHPUnit（Unit）
```
