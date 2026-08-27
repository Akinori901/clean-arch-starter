# 30. React フロントエンド ルール

対象: `services/frontend-react/`
配置前提: **S3 + CloudFront**（SPA・静的ホスティング）

## 前提

- **サーバサイドレンダリングを使わない。** S3 静的ホスティングのため、
  ビルド成果物は純粋な静的ファイル（`dist/`）である必要がある。
  Next.js の SSR/ISR は採用しない（採用するなら配置先が Lambda@Edge 等になり前提が変わる）。
- ルーティングは `react-router` のクライアントサイド。
  CloudFront で 403/404 → `/index.html` へフォールバックさせる（`infra/sam` に設定済み）。

## ディレクトリ構造

```
services/frontend-react/src/
├── app/            # エントリ・ルーティング・Provider 組立
├── features/       # 機能単位。ここが主戦場
│   └── <feature>/
│       ├── api/        # このfeature専用のAPI呼び出し
│       ├── components/ # このfeature専用のUI
│       ├── hooks/
│       └── types.ts
├── shared/         # 複数featureで共有するもののみ
│   ├── api/            # HTTPクライアント・認証トークン付与
│   ├── components/     # 汎用UI（Button, Spinner 等）
│   ├── hooks/
│   └── lib/
└── config/         # 環境変数の読み込み・型付け
```

## 依存ルール（構造チェッカが強制）

- ❌ `shared/` から `features/` を import する（共有物が機能に依存してはならない）
- ❌ `features/A/` から `features/B/` の内部を import する
  （feature 間の共有が必要になったら `shared/` へ引き上げる）
- ❌ `import` にディレクトリを跨ぐ相対パス（`../../../`）を使う
  → エイリアス（`@/features/...`）を使う
- ❌ `process.env` / `import.meta.env` を `config/` 以外で直接読む

## 認証（Cognito）

- **AWS Amplify を使わない。** 依存が重く、Cognito 以外へ移りにくくなる。
  `oidc-client-ts` による標準 OIDC フロー（Authorization Code + PKCE）を使う。
- トークンは `shared/api/` の HTTP クライアントが自動付与する。
  各 feature が個別に `Authorization` ヘッダを組み立てない。
- **アクセストークンを `localStorage` に置かない。** メモリ保持 + リフレッシュで再取得。
