import { env } from '@/config/env';
import { authStore } from '@/shared/api/authStore';

/**
 * HTTP クライアント。
 *
 * **Authorization ヘッダの付与はここだけで行う。**
 * 各 feature が個別に組み立てると、付け忘れや形式ゆれが必ず起きる。
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Content-Type', 'application/json');

  const token = authStore.get();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`${env.apiBaseUrl}${path}`, { ...init, headers });

  if (!response.ok) {
    // 本文が JSON とは限らない（502 等はプロキシが HTML を返す）
    const detail = await response
      .json()
      .then((body: { detail?: string }) => body.detail)
      .catch(() => response.statusText);
    throw new ApiError(response.status, detail ?? response.statusText);
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}
