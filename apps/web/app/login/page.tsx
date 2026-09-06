'use client';

import Link from 'next/link';
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
    <div className="auth-wrap">
      <div className="auth-card rise">
        <div className="auth-brand" style={{ color: 'var(--text)' }}>
          <span className="mark">F</span> Fidellis
        </div>
        <h1 style={{ fontSize: '1.4rem' }}>Entrar</h1>
        <p className="muted" style={{ marginTop: '-0.25rem' }}>
          Login global por e-mail — resolvemos automaticamente sua instituição.
        </p>

        <form onSubmit={onSubmit} style={{ marginTop: '1.25rem' }}>
          <div className="field">
            <label htmlFor="email">E-mail</label>
            <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
          </div>
          <div className="field">
            <label htmlFor="password">Senha</label>
            <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </div>

          {error && <p className="error-text">{error}</p>}

          <button className="btn btn-primary" type="submit" style={{ width: '100%', marginTop: '0.5rem' }} disabled={loading}>
            {loading ? 'Entrando…' : 'Entrar'}
          </button>
        </form>

        <p className="muted" style={{ marginTop: '1.25rem', textAlign: 'center', fontSize: '0.9rem' }}>
          Não tem conta? <Link href="/signup">Criar instituição</Link>
        </p>
      </div>
    </div>
  );
}
