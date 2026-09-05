'use client';

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
      // Sessão no mesmo formato do login (para o dashboard).
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
    <main className="container" style={{ maxWidth: 460 }}>
      <h1>Criar instituição</h1>
      <p className="muted">Cria a instituição, a unidade-raiz e o primeiro administrador (já vinculado).</p>

      <form className="card" onSubmit={onSubmit}>
        <label htmlFor="tname">Nome da instituição</label>
        <input
          id="tname"
          value={tenantName}
          onChange={(e) => {
            setTenantName(e.target.value);
            if (!slugTouched) setSlug(slugify(e.target.value));
          }}
          required
        />

        <label htmlFor="slug">Identificador (slug)</label>
        <input id="slug" value={slug} onChange={(e) => { setSlug(slugify(e.target.value)); setSlugTouched(true); }} required />

        <label htmlFor="dname">Seu nome</label>
        <input id="dname" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />

        <label htmlFor="email">E-mail</label>
        <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />

        <label htmlFor="password">Senha (mín. 6)</label>
        <input id="password" type="password" minLength={6} value={password} onChange={(e) => setPassword(e.target.value)} required />

        {error && <p style={{ color: '#ff7a7a', marginTop: '0.75rem' }}>{error}</p>}

        <button className="btn" type="submit" style={{ marginTop: '1rem', width: '100%' }} disabled={loading}>
          {loading ? 'Criando…' : 'Criar e entrar'}
        </button>
      </form>
    </main>
  );
}
