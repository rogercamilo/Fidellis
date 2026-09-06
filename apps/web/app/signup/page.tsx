'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { onboarding } from '../lib/api';

function slugify(s: string): string {
  return s
    .normalize('NFD')
    .replace(new RegExp('[\\u0300-\\u036f]', 'g'), '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 63);
}

export default function SignupPage() {
  const router = useRouter();
  const [tenantName, setTenantName] = useState('');
  const [slug, setSlug] = useState('');
  const [slugTouched, setSlugTouched] = useState(false);
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const result = await onboarding({
        slug: slug || slugify(tenantName),
        tenantName,
        email,
        password,
        displayName: displayName || undefined,
      });
      const session = {
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        user: result.user,
        tenants: [{ tenantId: result.tenant.id, slug: result.tenant.slug, name: result.tenant.name, role: 'admin' }],
        activeTenant: result.activeTenant,
      };
      sessionStorage.setItem('fidellis.session', JSON.stringify(session));
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
        <h1 style={{ fontSize: '1.4rem' }}>Criar instituição</h1>
        <p className="muted" style={{ marginTop: '-0.25rem' }}>
          Cria a instituição, a unidade-raiz e o primeiro administrador (já vinculado).
        </p>

        <form onSubmit={onSubmit} style={{ marginTop: '1.25rem' }}>
          <div className="field">
            <label htmlFor="tname">Nome da instituição</label>
            <input
              id="tname"
              value={tenantName}
              onChange={(e) => {
                setTenantName(e.target.value);
                if (!slugTouched) setSlug(slugify(e.target.value));
              }}
              required
              autoFocus
            />
          </div>
          <div className="field">
            <label htmlFor="slug">Identificador (slug)</label>
            <input id="slug" className="mono" value={slug} onChange={(e) => { setSlug(slugify(e.target.value)); setSlugTouched(true); }} required />
          </div>
          <div className="field">
            <label htmlFor="dname">Seu nome</label>
            <input id="dname" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="email">E-mail</label>
            <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </div>
          <div className="field">
            <label htmlFor="password">Senha (mín. 6)</label>
            <input id="password" type="password" minLength={6} value={password} onChange={(e) => setPassword(e.target.value)} required />
          </div>

          {error && <p className="error-text">{error}</p>}

          <button className="btn btn-primary" type="submit" style={{ width: '100%', marginTop: '0.5rem' }} disabled={loading}>
            {loading ? 'Criando…' : 'Criar e entrar'}
          </button>
        </form>

        <p className="muted" style={{ marginTop: '1.25rem', textAlign: 'center', fontSize: '0.9rem' }}>
          Já tem conta? <Link href="/login">Entrar</Link>
        </p>
      </div>
    </div>
  );
}
