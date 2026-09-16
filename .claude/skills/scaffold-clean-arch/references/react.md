# React フロントエンド — 生成手順

対象: `services/frontend-react/`
ルール本文: `.claude/rules/30-frontend.md`（**先に読むこと**）
検証: `make verify-front` / `eslint.config.js`（eslint-plugin-boundaries）

## 雛形にする既存ファイル

| 作るもの | 雛形 |
|---|---|
| feature の API 呼び出し | `src/features/auth/api/authApi.ts` |
| feature の UI | `src/features/auth/components/SignInPanel.tsx` |
| feature の hook | `src/features/auth/hooks/useAuth.ts` |
| 共有 HTTP クライアント | `src/shared/api/httpClient.ts` |
| 共有ストア | `src/shared/api/authStore.ts` |
| 環境変数 | `src/config/env.ts` |
| 組立点 | `src/app/App.tsx` |

## 新しい feature を足す手順

```
src/features/<feature>/
├── api/         このfeature専用のAPI呼び出し
├── components/  このfeature専用のUI
├── hooks/
└── types.ts
```

1. `src/features/<feature>/` を作る
2. `api/` … `shared/api/httpClient` を使う。**個別に `Authorization` を組み立てない**
3. `components/` `hooks/`
4. `src/app/App.tsx` から繋ぐ（**組立点は app のみ**）

## 境界ルール（eslint-plugin-boundaries が強制）

| from | 参照してよい |
|---|---|
| `app` | feature, shared, config |
| `feature` | **自分と同じ feature**, shared, config |
| `shared` | shared, config |
| `config` | **なし**（末端） |

`default: 'disallow'` なので、**表に無い参照はすべて落ちる。**

### 落ちたときの直し方

| 違反 | 直し方 |
|---|---|
| feature A → feature B | 共有したいものを `shared/` へ引き上げる |
| shared → feature | それはもう共有物ではない。feature 側へ戻す |
| config → 何か | config は末端。依存を持たせない |
| `../../` の相対 import | `@/` エイリアスに直す |
| `import.meta.env` を config 外で読んだ | `src/config/env.ts` 経由にする |

**「とりあえず shared に置く」をしない。** shared が肥大すると境界の意味が消える。
2 つ目の feature が実際に必要としてから引き上げる。

## 前提（変えてはならない）

- **SSR を使わない。** S3 + CloudFront の静的ホスティングが前提で、
  ビルド成果物は純粋な静的ファイル（`dist/`）である必要がある。
  Next.js の SSR/ISR を採用すると配置先が変わり、前提が崩れる。
- ルーティングは `react-router` のクライアントサイド。
  CloudFront で 403/404 → `/index.html` へフォールバック（`infra/sam` に設定済み）。

## 認証（Cognito）

- **AWS Amplify を使わない。** 依存が重く、Cognito 以外へ移りにくくなる。
  `oidc-client-ts` による標準 OIDC フロー（Authorization Code + PKCE）。
- トークンは `shared/api/` の HTTP クライアントが自動付与する。
- **アクセストークンを `localStorage` に置かない。** メモリ保持 + リフレッシュで再取得。

## バックエンドとの契約

このリポジトリのバックエンドは **5 スタックすべてが同じ JSON を返す**。
フロントは**どのスタックに繋いでも動く**（`VITE_API_BASE_URL` の切り替えのみ）。

レスポンスの形を変えるときは、**バックエンド全スタックを同時に変える**必要がある。
片方だけ変えると、他スタックに繋いだときに壊れる。

## 検証

```bash
make verify-front
# 内訳: eslint（境界）→ tsc --noEmit（型）
```
