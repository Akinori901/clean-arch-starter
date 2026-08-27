import { UserManager, WebStorageStateStore } from 'oidc-client-ts';
import { useCallback, useEffect, useState } from 'react';

import { env } from '@/config/env';
import { authStore } from '@/shared/api/authStore';

/**
 * Cognito 認証（Authorization Code + PKCE）。
 *
 * AWS Amplify は使わない。依存が重く、Cognito 以外へ移りにくくなる。
 * oidc-client-ts なら標準 OIDC なので、認証基盤の差し替えに耐える。
 */
const userManager = new UserManager({
  authority: env.cognito.authority,
  client_id: env.cognito.clientId,
  redirect_uri: env.cognito.redirectUri,
  response_type: 'code',
  scope: 'openid email profile',
  // 認可コードのやり取りに使う一時状態のみ sessionStorage へ置く。
  // アクセストークン本体はメモリ（authStore）で保持する。
  stateStore: new WebStorageStateStore({ store: window.sessionStorage }),
  automaticSilentRenew: true,
});

export type AuthState = {
  status: 'loading' | 'authenticated' | 'anonymous';
  email: string | null;
};

export function useAuth() {
  const [state, setState] = useState<AuthState>({ status: 'loading', email: null });

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      // リダイレクト直後なら、URL の認可コードをトークンへ交換する
      const isCallback = new URLSearchParams(window.location.search).has('code');
      const user = isCallback
        ? await userManager.signinRedirectCallback()
        : await userManager.getUser();

      if (isCallback) {
        // 認可コードを URL に残さない（履歴・Referer 経由の漏洩を避ける）
        window.history.replaceState({}, '', window.location.pathname);
      }
      if (cancelled) return;

      authStore.set(user?.access_token ?? null);
      setState(
        user && !user.expired
          ? { status: 'authenticated', email: user.profile.email ?? null }
          : { status: 'anonymous', email: null },
      );
    })().catch(() => {
      if (!cancelled) setState({ status: 'anonymous', email: null });
    });

    // トークンが自動更新されたら、保持している値も差し替える
    const onLoaded = (u: { access_token: string }) => authStore.set(u.access_token);
    userManager.events.addUserLoaded(onLoaded);

    return () => {
      cancelled = true;
      userManager.events.removeUserLoaded(onLoaded);
    };
  }, []);

  const signIn = useCallback(() => userManager.signinRedirect(), []);
  const signOut = useCallback(async () => {
    authStore.set(null);
    await userManager.signoutRedirect();
  }, []);

  return { ...state, signIn, signOut };
}
