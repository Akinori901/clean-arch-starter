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

## 依存ルール（eslint-plugin-boundaries が強制）

### 設定上の注意（実際に踏んだ）

**`import/resolver` の設定が無いと、境界検証は素通りする。**

boundaries は import 先のパスを**解決してから**要素（feature / shared 等）を
判定する。`@/features/...` のエイリアスを解決できないと「どの要素か不明」となり、
違反を検知できない。

この状態では、相対パス（`../../auth/...`）だけが `no-restricted-imports` で
拾われ、**規約が推奨している `@/` 記法のほうが検証されない**という
ねじれが起きる。実際に feature 間の相互参照を注入しても落ちなかった。

```js
'import/resolver': {
  typescript: { project: './tsconfig.json' },
  node: true,   // これも要る（相対パス・node_modules の解決）
},
```

`eslint-import-resolver-typescript` と `eslint-plugin-import` が
devDependencies に必要。**設定を変えたら、違反を注入して実際に落ちることを
必ず確認すること。**


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
