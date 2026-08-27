import { request } from '@/shared/api/httpClient';

export type Component = {
  name: string;
  state: 'up' | 'down';
  detail: string;
};

export type Health = {
  healthy: boolean;
  components: Component[];
};

/**
 * ヘルスチェック。
 *
 * 依存が落ちていると 503 が返り、httpClient が ApiError を投げる。
 * 「落ちている」ことも情報なので、呼び出し側で catch して表示する。
 */
export function fetchHealth(): Promise<Health> {
  return request<Health>('/api/health');
}
