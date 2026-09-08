'use client';

import { useEffect, useState } from 'react';
import { PageHeader } from '../../components/Fiori';
import { Panel } from '../../components/Panel';
import {
  accountLedger, listLedgerAccounts, trialBalance,
  type LedgerAccount, type LedgerView, type LoginResult, type TrialBalance,
} from '../../lib/api';

const fmt = (n: number) => n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function ContabilidadePage() {
  const [token, setToken] = useState<string | null>(null);
  const [tb, setTb] = useState<TrialBalance | null>(null);
  const [accounts, setAccounts] = useState<LedgerAccount[]>([]);
  const [accountId, setAccountId] = useState('');
  const [ledger, setLedger] = useState<LedgerView | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const t = (JSON.parse(raw) as LoginResult).accessToken;
    setToken(t);
    trialBalance(t).then(setTb).catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
    listLedgerAccounts(t).then(setAccounts).catch(() => setAccounts([]));
  }, []);

  useEffect(() => {
    if (!token || !accountId) { setLedger(null); return; }
    accountLedger(token, accountId)
      .then(setLedger)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro ao carregar o razão.'));
  }, [token, accountId]);

  const balanced = tb ? Math.abs(tb.totalDebit - tb.totalCredit) < 0.005 : true;
  const selected = accounts.find((a) => a.id === accountId);

  return (
    <>
      <PageHeader
        title="Contabilidade"
        subtitle="Balancete de verificação e razão (extrato de cada conta) consolidados das suas unidades."
        actions={tb && tb.accounts.length > 0 ? (
          <span className={`badge ${balanced ? 'ok' : 'err'}`}>
            {balanced ? 'Débitos = Créditos' : 'Desbalanceado'}
          </span>
        ) : undefined}
      />

      {error && <p className="error-text">{error}</p>}

      <div className="rise rise-2">
        <Panel title="Balancete de verificação" flush>
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

      <div className="rise rise-3" style={{ marginTop: '1rem' }}>
        <Panel
          title="Razão por conta"
          actions={selected && <span className="badge muted">Saldo: {fmt(ledger?.balance ?? 0)}</span>}
          flush
        >
          <div className="field" style={{ maxWidth: 420, padding: '1rem 1rem 0' }}>
            <label htmlFor="ledger-acc">Conta do plano de contas</label>
            <select id="ledger-acc" value={accountId} onChange={(e) => setAccountId(e.target.value)}>
              <option value="">Selecione uma conta…</option>
              {accounts.map((a) => (
                <option key={a.id} value={a.id}>{a.code} — {a.name}</option>
              ))}
            </select>
          </div>

          {accounts.length === 0 && (
            <p className="muted" style={{ padding: '0 1rem 1rem' }}>Plano de contas ainda não semeado.</p>
          )}

          {accountId && !ledger && <p className="muted" style={{ padding: '0 1rem 1rem' }}>Carregando…</p>}

          {ledger && (
            ledger.lines.length === 0 ? (
              <p className="muted" style={{ padding: '0 1rem 1rem' }}>Sem lançamentos nesta conta.</p>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Data</th>
                    <th>Histórico</th>
                    <th className="num">Débito</th>
                    <th className="num">Crédito</th>
                    <th className="num">Saldo</th>
                  </tr>
                </thead>
                <tbody>
                  {ledger.lines.map((l, i) => (
                    <tr key={i}>
                      <td>{new Date(l.date).toLocaleDateString('pt-BR')}</td>
                      <td>{l.description ?? '—'}</td>
                      <td className="num">{l.debit ? fmt(l.debit) : '—'}</td>
                      <td className="num">{l.credit ? fmt(l.credit) : '—'}</td>
                      <td className="num"><strong>{fmt(l.balance)}</strong></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )
          )}
        </Panel>
      </div>
    </>
  );
}
