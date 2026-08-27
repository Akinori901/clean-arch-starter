import { useAuth } from '@/features/auth/hooks/useAuth';

export function SignInPanel() {
  const { status, email, signIn, signOut } = useAuth();

  if (status === 'loading') return <p>認証状態を確認中…</p>;

  if (status === 'anonymous') {
    return (
      <section>
        <h2>未サインイン</h2>
        <button onClick={() => void signIn()}>Cognito でサインイン</button>
      </section>
    );
  }

  return (
    <section>
      <h2>サインイン済み</h2>
      <p>{email}</p>
      <button onClick={() => void signOut()}>サインアウト</button>
    </section>
  );
}
