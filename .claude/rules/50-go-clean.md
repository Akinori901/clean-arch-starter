# 50. Go — クリーンアーキテクチャ構成ルール

対象: `services/go-clean/`
検証: `go-arch-lint`（`.go-arch-lint.yml`）+ `golangci-lint` + `go vet`

## なぜクリーンアーキテクチャか

Go コミュニティでは DDD より**クリーンアーキテクチャ（Hexagonal / Ports & Adapters）**が主流。
参照実装も `go-clean-arch`（10k★）・`go-clean-template`（7.6k★）とクリーンアーキ寄りで、
`internal/` によるパッケージ境界の強制が言語機能として効くため相性がよい。

## 層構成

```
cmd/            ← エントリポイント。app を呼ぶだけ
    ↓
internal/app/   ← 組立点(composition root)。具象を結線してよい唯一の場所
    ↓
internal/controller/  ← 外部接点(HTTP)。HTTP の語彙を扱ってよい唯一の層
    ↓
internal/usecase/     ← アプリケーション固有のルール。契約(interface)を定義
    ↓
internal/entity/      ← 最内層。標準ライブラリのみ
    ↑
internal/repo/        ← usecase の契約を実装（依存性逆転）
```

## ディレクトリと責務

```
services/go-clean/
├── cmd/app/            # main。HTTP サーバ / Lambda の切り替えのみ
├── internal/
│   ├── entity/         # 値オブジェクト・エンティティ・ドメインエラー
│   ├── usecase/        # ユースケース + 契約(interface)の定義
│   ├── repo/           # MySQL / Cognito / S3 の実装
│   ├── controller/http # chi ルータ・ハンドラ
│   └── app/            # DI の結線・設定読み込み
└── pkg/                # 他プロジェクトへ公開してよい汎用部品のみ
```

## 依存ルール（go-arch-lint が強制）

| 層 | 依存してよい | 外部ライブラリ |
|---|---|---|
| `entity` | **なし** | **なし**（標準ライブラリのみ） |
| `usecase` | `entity` | **なし** |
| `repo` | `entity` | AWS SDK / MySQL / jwx |
| `controller` | `entity`, `usecase` | chi |
| `app` | すべて | AWS SDK / MySQL / chi |
| `cmd` | `app` | lambda アダプタ |

### Go 特有の作法

- **インターフェースは「使う側」で定義する。**
  Repository の契約は `repo` ではなく `usecase` に置き、`repo` がそれを満たす。
  これが Go における依存性逆転の書き方。
- **`repo` から `usecase` を import しないこと。**
  共有したい型（`AuthTokens` 等）が出てきたら、それは `entity` へ置く。
  （実際にこれを踏んだ。`usecase` に置くと依存が外から内へ逆流する）
- **`internal/` は Go が import を禁じてくれる。** 外部モジュールから触られたくない
  ものはすべてここに置く。`pkg/` は「公開してよいもの」だけ。

### 禁止事項（違反は CI で落ちる）

- ❌ `entity` から外部ライブラリを import する（DB ドライバ・AWS SDK・HTTP）
- ❌ `usecase` から外部ライブラリを import する
- ❌ `repo` から `usecase` を import する（契約は満たすだけ）
- ❌ `controller` から `repo` を直接触る（DI 経由で受け取る）
- ❌ `sql.ErrNoRows` をそのまま上へ返す（`entity` のエラーへ変換する）

## エラーの扱い

- ドメインのエラーは `entity` に `var Err... = errors.New(...)` で定義する。
- 判定は `errors.Is` / `errors.As`。**ただし AWS SDK のエラーは型で見ないこと。**
  エミュレータや一部経路では型付き例外にならず、`errors.As` が取りこぼす。
  `smithy.APIError` の**エラーコード文字列**で判定する（実際に踏んだ）。
- HTTP ステータスへの変換は `controller` だけが行う。`entity`/`usecase` は 401 を知らない。
- **500 を返すときも、ログには必ず原因を残す。** 両方伏せると本番で追えない。

## Lambda での注意

- `provided.al2023` + 静的バイナリ（`CGO_ENABLED=0`）。
- `AWS_LAMBDA_FUNCTION_NAME` の有無で HTTP / Lambda を切り替え、**同じ Handler を共有する**。
  実行環境ごとにルーティングを書き分けないこと。
- ビルドは `--platform linux/arm64`（Graviton）。揃えないと `exec format error` で起動しない。

## go-arch-lint の設定上の注意

- **`deepScan` は off にしてある。** 有効にすると、組立点(`app`)での依存性注入そのものが
  「`repo` -> `usecase`」の違反として報告される。組立点で具象を注入するのは
  クリーンアーキテクチャの前提なので、import ベースの判定に絞っている。
- 「依存先ゼロ」の層は `deps` に**書かない**（空の `mayDependOn` は設定エラーになる）。
