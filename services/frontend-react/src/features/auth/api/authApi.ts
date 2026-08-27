import { request } from '@/shared/api/httpClient';

export type CurrentUser = {
  id: string;
  email: string;
  display_name: string;
  is_active: boolean;
};

/**
 * バックエンドの /auth/me を叩く。
 *
 * Cognito のトークン取得自体は oidc-client-ts が担い、
 * ここは「自サービス側のユーザー情報」を取りに行く役割。
 */
export function fetchCurrentUser(): Promise<CurrentUser> {
  return request<CurrentUser>('/api/auth/me');
}
