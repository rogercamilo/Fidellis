'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { trialBalance, type LoginResult, type TrialBalance } from '../../lib/api';

export default function ContabilidadePage() {
  const [tb, setTb] = useState<TrialBalance | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const token = (JSON.parse(raw) as LoginResult).accessToken;
    trialBalance(token)
      .then(setTb)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, []);

  return (
    <main className="container" style={{ maxWidth: 720 }}>
      <p className="muted"><Link href="/dashboard">← Painel</Link></p>
      <h1>Balancete</h1>
      <p className="muted">Consolidado das suas unidades (unidade + filiais).</p>
      {error && <p style={{ color: '#ff7a7a' }}>{error}</p>}

      {!tb ? (
        <p className="muted">Carregando…</p>
      ) : tb.accounts.length === 0 ? (
        <p className="muted">Sem lançamentos ainda. Confirme um pagamento para ver os lançamentos.</p>
      ) : (
        <div className="card">
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ textAlign: 'left', color: 'var(--muted)' }}>
                <th style={{ padding: '0.35rem 0' }}>Conta</th>
                <th style={{ textAlign: 'right' }}>Débito</th>
                <th style={{ textAlign: 'right' }}>Crédito</th>
                <th style={{ textAlign: 'right' }}>Saldo</th>
              </tr>
            </thead>
            <tbody>
              {tb.accounts.map((a) => (
                <tr key={a.ledgerAccountId ?? a.name} style={{ borderTop: '1px solid var(--border)' }}>
                  <td style={{ padding: '0.35rem 0' }}>{a.code ? `${a.code} · ` : ''}{a.name}</td>
                  <td style={{ textAlign: 'right' }}>{a.debit.toFixed(2)}</td>
                  <td style={{ textAlign: 'right' }}>{a.credit.toFixed(2)}</td>
                  <td style={{ textAlign: 'right' }}>{a.balance.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr style={{ borderTop: '2px solid var(--border)', fontWeight: 700 }}>
                <td style={{ padding: '0.5rem 0' }}>Totais</td>
                <td style={{ textAlign: 'right' }}>{tb.totalDebit.toFixed(2)}</td>
                <td style={{ textAlign: 'right' }}>{tb.totalCredit.toFixed(2)}</td>
                <td style={{ textAlign: 'right' }}>{(tb.totalDebit - tb.totalCredit).toFixed(2)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </main>
  );
}
