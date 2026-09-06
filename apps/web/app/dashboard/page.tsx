'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import type { LoginResult } from '../lib/api';

export default function DashboardPage() {
  const [session, setSession] = useState<LoginResult | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) setSession(JSON.parse(raw) as LoginResult);
  }, []);

  if (!session) {
    return (
      <main className="container">
        <h1>Painel</h1>
        <p className="muted">
          Sessão não encontrada. <Link href="/login">Faça login</Link> para continuar.
        </p>
      </main>
    );
  }

  return (
    <main className="container">
      <h1>Painel</h1>
      <p className="muted">
        Olá, {session.user.displayName ?? session.user.email}. Tenant ativo:{' '}
        <strong style={{ color: 'var(--fg)' }}>{session.activeTenant ?? '— selecione —'}</strong>
      </p>

      <p style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
        <Link className="btn" href="/dashboard/cobranca">Nova cobrança (PIX)</Link>
        <Link className="btn" href="/dashboard/recorrencia" style={ghostLink}>Recorrência (dízimo)</Link>
        <Link className="btn" href="/dashboard/recibos" style={ghostLink}>Recibos</Link>
        <Link className="btn" href="/dashboard/contabilidade" style={ghostLink}>Balancete</Link>
      </p>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>Suas instituições</h3>
        {session.tenants.length === 0 ? (
          <p className="muted">Nenhum vínculo encontrado.</p>
        ) : (
          <ul>
            {session.tenants.map((t) => (
              <li key={t.tenantId}>
                {t.name} <span className="muted">({t.slug}) — {t.role}</span>
              </li>
            ))}
          </ul>
        )}
      </div>

      <p className="muted" style={{ marginTop: '1.5rem' }}>
        As chamadas de dados (ex.: <code>/api/donations/ping</code>) passam pelo BFF, que anexa o
        tenant ao token e encaminha ao core .NET.
      </p>
    </main>
  );
}

const ghostLink: React.CSSProperties = {
  background: 'transparent',
  color: 'var(--accent)',
  border: '1px solid var(--border)',
};
