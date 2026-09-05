'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { login } from '../lib/api';

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const result = await login(email, password);
      sessionStorage.setItem('fidellis.session', JSON.stringify(result));
      router.push('/dashboard');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro inesperado.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="container" style={{ maxWidth: 420 }}>
      <h1>Entrar</h1>
      <p className="muted">Login global por e-mail — resolvemos automaticamente o(s) seu(s) tenant(s).</p>

      <form className="card" onSubmit={onSubmit}>
        <label htmlFor="email">E-mail</label>
        <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />

        <label htmlFor="password">Senha</label>
        <input
          id="password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />

        {error && (
          <p style={{ color: '#ff7a7a', marginTop: '0.75rem' }}>{error}</p>
        )}

        <button className="btn" type="submit" style={{ marginTop: '1rem', width: '100%' }} disabled={loading}>
          {loading ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
    </main>
  );
}
