import { SignInPanel } from '@/features/auth/components/SignInPanel';
import { HealthPanel } from '@/features/health/components/HealthPanel';

/**
 * app は組立点。feature を並べるだけで、ロジックを持たない。
 */
export function App() {
  return (
    <main>
      <h1>clean-arch-starter</h1>
      <SignInPanel />
      <HealthPanel />
    </main>
  );
}
