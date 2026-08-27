/**
 * 環境変数の読み取りはここに集約する。
 *
 * 各 feature が import.meta.env を直接読むと、
 * 「何を設定すれば動くのか」がコード全体に散らばって追えなくなる。
 */

function required(key: string, value: string | undefined): string {
  if (!value) {
    // ビルド時ではなく起動時に落とす。設定漏れは早く・大きく失敗させる。
    throw new Error(`環境変数 ${key} が設定されていません`);
  }
  return value;
}

export const env = {
  apiBaseUrl: required('VITE_API_BASE_URL', import.meta.env.VITE_API_BASE_URL),
  cognito: {
    authority: required('VITE_COGNITO_AUTHORITY', import.meta.env.VITE_COGNITO_AUTHORITY),
    clientId: required('VITE_COGNITO_CLIENT_ID', import.meta.env.VITE_COGNITO_CLIENT_ID),
    redirectUri: required('VITE_COGNITO_REDIRECT_URI', import.meta.env.VITE_COGNITO_REDIRECT_URI),
  },
} as const;
