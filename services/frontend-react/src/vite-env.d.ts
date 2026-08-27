/// <reference types="vite/client" />

/**
 * 環境変数の型定義。
 *
 * ここに宣言しておくと、`config/env.ts` で参照するときに型が効き、
 * 未定義の変数名を書いた時点で tsc が落ちる。
 *
 * **読み取ってよいのは `src/config/` だけ**（eslint が他の層を弾く）。
 */
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
  readonly VITE_COGNITO_AUTHORITY: string;
  readonly VITE_COGNITO_CLIENT_ID: string;
  readonly VITE_COGNITO_REDIRECT_URI: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
