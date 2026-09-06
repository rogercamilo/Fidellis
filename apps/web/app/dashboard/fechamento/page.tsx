'use client';

import { useCallback, useEffect, useState } from 'react';
import { FinanceNav } from '../../components/FinanceNav';
import { Panel } from '../../components/Panel';
import {
  closePeriod, listPeriods, reopenPeriod,
  type AccountingPeriod, type LoginResult,
} from '../../lib/api';

const MONTHS = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
const monthLabel = (m: number) => MONTHS[m - 1] ?? String(m);

export default function FechamentoPage() {
  const [token, setToken] = useState<string | null>(null);
  const [role, setRole] = useState<string | undefined>();
  const [error, setError] = useState<string | null>(null);
  const [periods, setPeriods] = useState<AccountingPeriod[]>([]);

  const now = new Date();
  const [year, setYear] = useState(String(now.getFullYear()));
  const [month, setMonth] = useState(String(now.getMonth() + 1));

  const isAdmin = role === 'admin';

  const refresh = useCallback(async (t: string) => {
    try { setPeriods(await listPeriods(t)); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro ao carregar.'); }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) {
      const s = JSON.parse(raw) as LoginResult;
      setToken(s.accessToken);
      setRole(s.tenants.find((x) => x.slug === s.activeTenant)?.role);
      void refresh(s.accessToken);
    }
  }, [refresh]);

  async function close() {
    if (!token) return;
    setError(null);
    try { await closePeriod(token, Number(year), Number(month)); await refresh(token); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  async function reopen(p: AccountingPeriod) {
    if (!token) return;
    setError(null);
    try { await reopenPeriod(token, p.year, p.month); await refresh(token); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Fechamento de período</h1>
          <p className="subtitle">Fecha o mês bloqueando lançamentos retroativos. Reabertura somente por admin (com auditoria).</p>
        </div>
      </div>

      <FinanceNav />

      {error && <p className="error-text">{error}</p>}

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        <Panel title="Fechar um mês">
          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end' }}>
            <div className="field" style={{ width: 120 }}>
              <label htmlFor="fc-year">Ano</label>
              <input id="fc-year" type="number" value={year} onChange={(e) => setYear(e.target.value)} />
            </div>
            <div className="field" style={{ width: 140 }}>
              <label htmlFor="fc-month">Mês</label>
              <select id="fc-month" value={month} onChange={(e) => setMonth(e.target.value)}>
                {MONTHS.map((m, i) => <option key={m} value={i + 1}>{i + 1} — {m}</option>)}
              </select>
            </div>
            <button className="btn btn-primary" onClick={close} style={{ marginBottom: 2 }}>Fechar período</button>
          </div>
        </Panel>

        <Panel title="Períodos" flush>
          {periods.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum período registrado.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Período</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {periods.map((p) => (
                  <tr key={`${p.year}-${p.month}`}>
                    <td>{monthLabel(p.month)}/{p.year}</td>
                    <td><span className={`badge ${p.status === 'closed' ? 'muted' : 'ok'}`}>{p.status === 'closed' ? 'fechado' : 'aberto'}</span></td>
                    <td className="num">
                      {p.status === 'closed' && isAdmin && <button className="btn btn-ghost btn-sm" onClick={() => reopen(p)}>Reabrir</button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>
      </div>
    </>
  );
}
