/**
 * アクセストークンの保持。
 *
 * **localStorage に置かない。** XSS を踏んだ瞬間に持ち出される。
 * メモリ保持にして、リロード時は refresh token（httpOnly cookie もしくは
 * oidc-client-ts の silent renew）で取り直す。
 */
let accessToken: string | null = null;

export const authStore = {
  get: (): string | null => accessToken,
  set: (token: string | null): void => {
    accessToken = token;
  },
};
