'use client';

import { useCallback, useEffect, useState } from 'react';
import { FinanceNav } from '../../components/FinanceNav';
import { OrganizationPicker } from '../../components/OrganizationPicker';
import { Panel } from '../../components/Panel';
import {
  createReceivable, listReceivables, receivablesAging, settleReceivable,
  type AgingReport, type LoginResult, type Receivable,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const date = (s: string) => new Date(s + 'T00:00:00').toLocaleDateString('pt-BR');

function statusBadge(status: string): string {
  if (status === 'received') return 'ok';
  if (status === 'partial') return 'warn';
  if (status === 'canceled') return 'muted';
  return 'warn';
}
const sourceLabel = (s: string) => (s === 'grant' ? 'Convênio' : s === 'agreement' ? 'Acordo' : 'Promessa');

export default function ReceberPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [items, setItems] = useState<Receivable[]>([]);
  const [aging, setAging] = useState<AgingReport | null>(null);

  const [organizationId, setOrganizationId] = useState('');
  const [amount, setAmount] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [source, setSource] = useState('pledge');
  const [description, setDescription] = useState('');

  const refresh = useCallback(async (t: string) => {
    try {
      const [list, ag] = await Promise.all([listReceivables(t), receivablesAging(t)]);
      setItems(list);
      setAging(ag);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao carregar.'); }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) {
      const t = (JSON.parse(raw) as LoginResult).accessToken;
      setToken(t);
      void refresh(t);
    }
  }, [refresh]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return setError('Sessão não encontrada.');
    if (!organizationId || !amount || !dueDate) return setError('Unidade, valor e vencimento são obrigatórios.');
    setError(null);
    try {
      await createReceivable(token, { organizationId, amount: Number(amount), dueDate, source, description: description || undefined });
      setAmount(''); setDescription('');
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  async function settle(r: Receivable) {
    if (!token) return;
    const outstanding = r.amount - r.receivedAmount;
    const input = window.prompt(`Valor a baixar (restante ${brl(outstanding)}):`, outstanding.toFixed(2));
    if (input === null) return;
    const value = Number(input.replace(',', '.'));
    if (!(value > 0)) return setError('Valor inválido.');
    setError(null);
    try {
      await settleReceivable(token, r.id, { amount: value });
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Contas a Receber</h1>
          <p className="subtitle">Promessas de doação e recebíveis (convênios/editais), com aging e baixa.</p>
        </div>
      </div>

      <FinanceNav />

      {error && <p className="error-text">{error}</p>}

      <div className="kpi-grid rise rise-2">
        <div className="kpi"><div className="kpi-label">A vencer</div><div className="kpi-value"><span className="cur">R$</span>{(aging?.notDue ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div></div>
        <div className="kpi"><div className="kpi-label">Vencido 1–30</div><div className="kpi-value"><span className="cur">R$</span>{(aging?.overdue1To30 ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div></div>
        <div className="kpi"><div className="kpi-label">Vencido 31–60</div><div className="kpi-value"><span className="cur">R$</span>{(aging?.overdue31To60 ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div></div>
        <div className="kpi"><div className="kpi-label">Vencido 60+</div><div className="kpi-value"><span className="cur">R$</span>{(aging?.overdue60Plus ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div></div>
        <div className="kpi"><div className="kpi-label">Total em aberto</div><div className="kpi-value"><span className="cur">R$</span>{(aging?.totalOutstanding ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div></div>
      </div>

      <div className="grid cols-2 rise rise-3" style={{ marginTop: '1rem', alignItems: 'start' }}>
        <Panel title="Nova promessa / recebível">
          <form onSubmit={onSubmit}>
            <div className="field"><OrganizationPicker token={token} value={organizationId} onChange={setOrganizationId} /></div>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
              <div className="field" style={{ flex: 1 }}>
                <label htmlFor="r-amt">Valor (R$)</label>
                <input id="r-amt" type="number" step="0.01" min="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} required />
              </div>
              <div className="field" style={{ width: 160 }}>
                <label htmlFor="r-due">Vencimento</label>
                <input id="r-due" type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} required />
              </div>
              <div className="field" style={{ width: 140 }}>
                <label htmlFor="r-src">Tipo</label>
                <select id="r-src" value={source} onChange={(e) => setSource(e.target.value)}>
                  <option value="pledge">Promessa</option>
                  <option value="grant">Convênio</option>
                  <option value="agreement">Acordo</option>
                </select>
              </div>
            </div>
            <div className="field">
              <label htmlFor="r-desc">Descrição (opcional)</label>
              <input id="r-desc" value={description} onChange={(e) => setDescription(e.target.value)} />
            </div>
            <button className="btn btn-primary" type="submit">Registrar</button>
          </form>
        </Panel>

        <Panel title="Recebíveis" flush>
          {items.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum recebível ainda.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Descrição</th><th>Vencimento</th><th className="num">Valor</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {items.map((r) => (
                  <tr key={r.id}>
                    <td>{r.description || sourceLabel(r.source)}<br /><span className="muted" style={{ fontSize: '0.75rem' }}>{sourceLabel(r.source)}</span></td>
                    <td className="muted">{date(r.dueDate)}</td>
                    <td className="num"><strong>{brl(r.amount)}</strong>{r.receivedAmount > 0 && <><br /><span className="muted" style={{ fontSize: '0.75rem' }}>recebido {brl(r.receivedAmount)}</span></>}</td>
                    <td><span className={`badge ${statusBadge(r.status)}`}>{r.status}</span></td>
                    <td className="num">{(r.status === 'open' || r.status === 'partial') && <button className="btn btn-ghost btn-sm" onClick={() => settle(r)}>Baixar</button>}</td>
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
