'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Panel } from '../../components/Panel';
import {
  budgetActual, createBudget, listCostCenters, listFunds, reviseBudget,
  type BudgetActual, type CostCenter, type Fund, type LoginResult,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function OrcamentoPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [year, setYear] = useState(new Date().getFullYear());
  const [rows, setRows] = useState<BudgetActual[]>([]);
  const [costCenters, setCostCenters] = useState<CostCenter[]>([]);
  const [funds, setFunds] = useState<Fund[]>([]);

  const [kind, setKind] = useState('expense');
  const [amount, setAmount] = useState('');
  const [costCenterId, setCostCenterId] = useState('');
  const [fundId, setFundId] = useState('');

  const ccName = useMemo(() => new Map(costCenters.map((c) => [c.id, c.name])), [costCenters]);
  const fundName = useMemo(() => new Map(funds.map((f) => [f.id, f.name])), [funds]);

  const refresh = useCallback(async (t: string, y: number) => {
    try {
      const [actual, cc, fn] = await Promise.all([budgetActual(t, y), listCostCenters(t), listFunds(t)]);
      setRows(actual); setCostCenters(cc); setFunds(fn);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao carregar.'); }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) { const t = (JSON.parse(raw) as LoginResult).accessToken; setToken(t); void refresh(t, year); }
  }, [refresh, year]);

  async function add(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!amount) return setError('Informe o valor.');
    setError(null);
    try {
      await createBudget(token, {
        year, kind, amount: Number(amount),
        costCenterId: costCenterId || undefined, fundId: fundId || undefined,
      });
      setAmount('');
      await refresh(token, year);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  async function revise(r: BudgetActual) {
    if (!token) return;
    const input = window.prompt(`Novo valor previsto (atual ${brl(r.budgeted)}):`, r.budgeted.toFixed(2));
    if (input === null) return;
    const value = Number(input.replace(',', '.'));
    if (!(value > 0)) return setError('Valor inválido.');
    setError(null);
    try { await reviseBudget(token, r.budgetId, { amount: value }); await refresh(token, year); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro ao revisar.'); }
  }

  const dims = (r: BudgetActual) =>
    [r.costCenterId ? ccName.get(r.costCenterId) : null, r.fundId ? fundName.get(r.fundId) : null]
      .filter(Boolean).join(' · ') || 'Geral';

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Orçamento</h1>
          <p className="subtitle">Previsto × realizado por dimensão (competência). Revisões preservam o histórico.</p>
        </div>
      </div>

      {error && <p className="error-text">{error}</p>}

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        <Panel title="Novo orçamento">
          <form onSubmit={add}>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
              <div className="field" style={{ width: 120 }}>
                <label htmlFor="o-year">Ano</label>
                <input id="o-year" type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} />
              </div>
              <div className="field" style={{ width: 140 }}>
                <label htmlFor="o-kind">Tipo</label>
                <select id="o-kind" value={kind} onChange={(e) => setKind(e.target.value)}>
                  <option value="expense">Despesa</option>
                  <option value="revenue">Receita</option>
                </select>
              </div>
              <div className="field" style={{ flex: 1 }}>
                <label htmlFor="o-amt">Valor previsto (R$)</label>
                <input id="o-amt" type="number" step="0.01" min="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} required />
              </div>
            </div>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
              <div className="field" style={{ flex: 1 }}>
                <label htmlFor="o-cc">Centro de custo (opcional)</label>
                <select id="o-cc" value={costCenterId} onChange={(e) => setCostCenterId(e.target.value)}>
                  <option value="">Todos</option>
                  {costCenters.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <div className="field" style={{ flex: 1 }}>
                <label htmlFor="o-fund">Fundo (opcional)</label>
                <select id="o-fund" value={fundId} onChange={(e) => setFundId(e.target.value)}>
                  <option value="">Todos</option>
                  {funds.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
                </select>
              </div>
            </div>
            <button className="btn btn-primary" type="submit">Adicionar</button>
          </form>
        </Panel>

        <Panel title={`Previsto × realizado — ${year}`} flush>
          {rows.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum orçamento para o ano.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Dimensão</th><th>Tipo</th><th className="num">Previsto</th><th className="num">Realizado</th><th></th></tr></thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.budgetId} style={r.overBudget ? { background: '#fdf3f2' } : undefined}>
                    <td>{dims(r)}</td>
                    <td><span className={`badge ${r.kind === 'revenue' ? 'ok' : 'warn'}`}>{r.kind === 'revenue' ? 'receita' : 'despesa'}</span></td>
                    <td className="num">{brl(r.budgeted)}</td>
                    <td className="num">
                      <strong>{brl(r.realized)}</strong>
                      {r.overBudget && <span className="badge err" style={{ marginLeft: 6 }}>estouro</span>}
                    </td>
                    <td className="num"><button className="btn btn-ghost btn-sm" onClick={() => revise(r)}>Revisar</button></td>
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
