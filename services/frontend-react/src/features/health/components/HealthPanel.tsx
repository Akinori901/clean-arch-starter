import { useEffect, useState } from 'react';

import { fetchHealth, type Health } from '@/features/health/api/healthApi';
import { ApiError } from '@/shared/api/httpClient';

export function HealthPanel() {
  const [health, setHealth] = useState<Health | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchHealth()
      .then(setHealth)
      .catch((e: unknown) => {
        // 503 は「サーバが落ちている」ではなく「依存が落ちている」。
        // 区別して見せないと切り分けの役に立たない。
        setError(e instanceof ApiError ? `${e.status}: ${e.message}` : '取得に失敗しました');
      });
  }, []);

  if (error) return <p role="alert">ヘルスチェック取得エラー — {error}</p>;
  if (!health) return <p>確認中…</p>;

  return (
    <section>
      <h2>ヘルスチェック: {health.healthy ? '正常' : '異常'}</h2>
      <ul>
        {health.components.map((c) => (
          <li key={c.name}>
            {c.name}: <strong>{c.state}</strong>
            {c.detail && ` — ${c.detail}`}
          </li>
        ))}
      </ul>
    </section>
  );
}
