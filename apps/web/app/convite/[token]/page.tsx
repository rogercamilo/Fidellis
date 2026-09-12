'use client';

import { useParams, useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { acceptInvitation, getInvitation, roleLabel, type InvitationInfo } from '../../lib/api';

export default function AceitarConvitePage() {
  const router = useRouter();
  const params = useParams<{ token: string }>();
  const token = params.token;

  const [info, setInfo] = useState<InvitationInfo | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    getInvitation(token)
      .then(setInfo)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Convite inválido.'));
  }, [token]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const result = await acceptInvitation(token, password, displayName || undefined);
      const session = {
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        user: result.user,
        tenants: [{ tenantId: '', slug: result.tenant.slug, name: result.tenant.name, role: result.tenant.role }],
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

        {loadError ? (
          <>
            <h1 style={{ fontSize: '1.4rem' }}>Convite indisponível</h1>
            <p className="muted">{loadError}</p>
            <p className="muted" style={{ marginTop: '1rem' }}>
              Peça um novo convite ao coordenador da sua instituição, ou <a href="/login">entre</a> se já tem conta.
            </p>
          </>
        ) : !info ? (
          <p className="muted">Carregando convite…</p>
        ) : (
          <>
            <h1 style={{ fontSize: '1.4rem' }}>Aceitar convite</h1>
            <p className="muted" style={{ marginTop: '-0.25rem' }}>
              Você foi convidado(a) para a equipe de <strong>{info.tenantName}</strong> como{' '}
              <strong>{roleLabel(info.role)}</strong>. Defina sua senha para entrar.
            </p>

            <form onSubmit={onSubmit} style={{ marginTop: '1.25rem' }}>
              <div className="field">
                <label htmlFor="email">E-mail</label>
                <input id="email" value={info.email} readOnly disabled />
              </div>
              <div className="field">
                <label htmlFor="dname">Seu nome</label>
                <input id="dname" value={displayName} onChange={(e) => setDisplayName(e.target.value)} autoFocus />
              </div>
              <div className="field">
                <label htmlFor="password">Senha (mín. 6)</label>
                <input id="password" type="password" minLength={6} value={password} onChange={(e) => setPassword(e.target.value)} required />
              </div>

              {error && <p className="error-text">{error}</p>}

              <button className="btn btn-primary" type="submit" style={{ width: '100%', marginTop: '0.5rem' }} disabled={loading}>
                {loading ? 'Entrando…' : 'Aceitar e entrar'}
              </button>
            </form>
          </>
        )}
      </div>
    </div>
  );
}
