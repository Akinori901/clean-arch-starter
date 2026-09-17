# Go (クリーンアーキテクチャ) — 生成手順

対象: `services/go-clean/`
ルール本文: `.claude/rules/50-go-clean.md`（**先に読むこと**）
検証: `make verify-go` / `.go-arch-lint.yml`

## 雛形にする既存ファイル

| 作るもの | 雛形 |
|---|---|
| エンティティ・値オブジェクト | `internal/entity/user.go` |
| ドメインエラー | `internal/entity/auth.go`, `internal/entity/health.go` |
| 契約(interface) | `internal/usecase/interfaces.go` |
| ユースケース | `internal/usecase/auth.go` |
| Repository 実装 | `internal/repo/user_mysql.go` |
| 外部サービス実装 | `internal/repo/cognito_auth.go` |
| HTTP ハンドラ | `internal/controller/http/router.go` |
| DI 結線 | `internal/app/app.go` |
| エントリポイント | `cmd/app/main.go` |
| テスト | `internal/usecase/auth_test.go` |

## 生成順序

```
1. internal/entity/             標準ライブラリのみ。ドメインエラーもここ
2. internal/usecase/interfaces.go   契約(interface)を **使う側** に定義
3. internal/usecase/            ユースケース本体
4. internal/repo/               契約を「満たす」実装。AWS SDK / MySQL はここだけ
5. internal/controller/http/    chi ハンドラ。HTTP 語彙を扱ってよい唯一の層
6. internal/app/app.go          結線
7. cmd/app/main.go              HTTP / Lambda の切り替えのみ
8. *_test.go
```

## Go 特有の作法（他言語からの移植で必ず間違える箇所）

### 1. interface は「使う側」で定義する

Repository の契約は `repo` ではなく **`usecase` に置く**。`repo` はそれを満たすだけ。
これが Go における依存性逆転の書き方。

```go
// internal/usecase/interfaces.go  ← 契約はここ
type UserRepo interface { FindByID(ctx context.Context, id entity.UserID) (entity.User, error) }
```

### 2. `repo` から `usecase` を import しない

**実際に踏んだ問題。** 共有したい型（`AuthTokens` 等）が出てきたら、
それは `usecase` ではなく **`entity` へ置く**。
`usecase` に置くと依存が外から内へ逆流し、go-arch-lint が落ちる。

### 3. `internal/` と `pkg/` の使い分け

`internal/` は Go が言語機能として import を禁じてくれる。
外から触られたくないものはすべてここ。**`pkg/` は「他プロジェクトへ公開してよいもの」だけ。**

## go-arch-lint の設定を理解しておく

| 設定 | 意味 | 影響 |
|---|---|---|
| `depOnAnyVendor: false` | 外部ライブラリも層ごとに制限 | entity に DB ドライバを入れると落ちる |
| `deepScan: false` | import ベースでのみ判定 | 組立点(`app`)での DI を違反と誤検知しないため |
| `commonComponents: [entity]` | entity はどの層からも import 可 | 各層の `mayDependOn` に entity を書かなくてよい |

**新しい外部ライブラリを使うときは `vendors:` に登録し、
使う層の `canUse:` に足す。** 登録しないと `depOnAnyVendor: false` により落ちる。
落ちたときに「設定を緩める」のではなく、**その層がそのライブラリを使ってよいか**を先に考える。

**「依存先ゼロ」の層は `deps:` に書かない。**
空の `mayDependOn` は設定エラーになる（`entity` が `deps` に無いのはこのため）。

## エラーの扱い

- ドメインのエラーは `entity` に `var Err... = errors.New(...)` で定義。
- 判定は `errors.Is` / `errors.As`。
- **ただし AWS SDK のエラーは型で見ない。**
  エミュレータでは型付き例外にならず `errors.As` が取りこぼす（実際に踏んだ）。
  `smithy.APIError` の **エラーコード文字列**で判定する。
- `sql.ErrNoRows` をそのまま上へ返さない。`entity` のエラーへ変換する。
- HTTP ステータスへの変換は `controller` だけ。`entity` / `usecase` は 401 を知らない。
- **500 を返すときもログには原因を残す。**

## Lambda での注意

- `provided.al2023` + 静的バイナリ（`CGO_ENABLED=0`）。
- `AWS_LAMBDA_FUNCTION_NAME` の有無で HTTP / Lambda を切り替え、**同じ Handler を共有する**。
  実行環境ごとにルーティングを書き分けない。
- ビルドは `--platform linux/arm64`（Graviton）。揃えないと `exec format error`。

## テスト

外部テストパッケージ（`package foo_test`）は go-arch-lint の対象外（`excludeFiles`）。
テスト対象の自パッケージを import するのは Go の標準的な作法なので層違反ではない。

## 検証

```bash
make verify-go
# 内訳: go-arch-lint（層）→ go vet → go test
```
