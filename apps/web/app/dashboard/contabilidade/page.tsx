'use client';

import { useEffect, useState } from 'react';
import { Panel } from '../../components/Panel';
import { trialBalance, type LoginResult, type TrialBalance } from '../../lib/api';

const fmt = (n: number) => n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

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

  const balanced = tb ? Math.abs(tb.totalDebit - tb.totalCredit) < 0.005 : true;

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Balancete</h1>
          <p className="subtitle">Consolidado das suas unidades (unidade + filiais).</p>
        </div>
        {tb && tb.accounts.length > 0 && (
          <span className={`badge ${balanced ? 'ok' : 'err'}`}>
            {balanced ? 'Débitos = Créditos' : 'Desbalanceado'}
          </span>
        )}
      </div>

      <div className="rise rise-2">
        <Panel title="Balancete de verificação" flush>
          {error && <p className="error-text" style={{ padding: '1rem' }}>{error}</p>}
          {!tb ? (
            <p className="muted" style={{ padding: '1rem' }}>Carregando…</p>
          ) : tb.accounts.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>
              Sem lançamentos ainda. Confirme um pagamento para ver a movimentação.
            </p>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Conta</th>
                  <th className="num">Débito</th>
                  <th className="num">Crédito</th>
                  <th className="num">Saldo</th>
                </tr>
              </thead>
              <tbody>
                {tb.accounts.map((a) => (
                  <tr key={a.ledgerAccountId ?? a.name}>
                    <td>{a.code && <span className="mono muted">{a.code}</span>} {a.name}</td>
                    <td className="num">{fmt(a.debit)}</td>
                    <td className="num">{fmt(a.credit)}</td>
                    <td className="num">{fmt(a.balance)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td>Totais</td>
                  <td className="num">{fmt(tb.totalDebit)}</td>
                  <td className="num">{fmt(tb.totalCredit)}</td>
                  <td className="num">{fmt(tb.totalDebit - tb.totalCredit)}</td>
                </tr>
              </tfoot>
            </table>
          )}
        </Panel>
      </div>
    </>
  );
}
