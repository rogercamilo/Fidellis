'use client';

import { useCallback, useEffect, useState } from 'react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { FinanceNav } from '../../components/FinanceNav';
import { OrganizationPicker } from '../../components/OrganizationPicker';
import { Panel } from '../../components/Panel';
import {
  createTreasuryAccount, listTreasuryAccounts, treasuryBalance, treasuryCashflow, treasuryTransfer,
  type CashFlowProjection, type LoginResult, type TreasuryAccount,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const brlShort = (n: number) => n.toLocaleString('pt-BR', { notation: 'compact', maximumFractionDigits: 1 });

export default function TesourariaPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [accounts, setAccounts] = useState<TreasuryAccount[]>([]);
  const [consolidated, setConsolidated] = useState(0);
  const [cashflow, setCashflow] = useState<CashFlowProjection[]>([]);

  const [organizationId, setOrganizationId] = useState('');
  const [name, setName] = useState('');
  const [kind, setKind] = useState('bank');
  const [opening, setOpening] = useState('');

  const [fromId, setFromId] = useState('');
  const [toId, setToId] = useState('');
  const [transferAmount, setTransferAmount] = useState('');

  const refresh = useCallback(async (t: string) => {
    try {
      const [accs, bal, cf] = await Promise.all([listTreasuryAccounts(t), treasuryBalance(t), treasuryCashflow(t)]);
      setAccounts(accs);
      setConsolidated(bal.balance);
      setCashflow(cf);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar.');
    }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) {
      const t = (JSON.parse(raw) as LoginResult).accessToken;
      setToken(t);
      void refresh(t);
    }
  }, [refresh]);

  async function addAccount(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return setError('Sessão não encontrada.');
    if (!organizationId || !name) return setError('Unidade e nome são obrigatórios.');
    setError(null);
    try {
      await createTreasuryAccount(token, { organizationId, name, kind, openingBalance: Number(opening || '0') });
      setName(''); setOpening('');
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  async function doTransfer(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!fromId || !toId || !transferAmount) return setError('Preencha origem, destino e valor.');
    setError(null);
    try {
      await treasuryTransfer(token, { fromAccountId: fromId, toAccountId: toId, amount: Number(transferAmount) });
      setTransferAmount('');
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  const cfData = cashflow.map((c) => ({ label: `D+${c.horizonDays}`, ...c }));

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Tesouraria</h1>
          <p className="subtitle">Contas e caixas, saldo consolidado da rede e fluxo de caixa projetado.</p>
        </div>
      </div>

      <FinanceNav />

      {error && <p className="error-text">{error}</p>}

      <div className="kpi-grid rise rise-2">
        <div className="kpi">
          <div className="kpi-label">Saldo consolidado</div>
          <div className="kpi-value"><span className="cur">R$</span>{consolidated.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Contas</div>
          <div className="kpi-value">{accounts.length}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Projetado D+90</div>
          <div className="kpi-value"><span className="cur">R$</span>{(cashflow.find((c) => c.horizonDays === 90)?.projected ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
        </div>
      </div>

      <div className="rise rise-3">
        <Panel title="Fluxo de caixa projetado">
          {cfData.length === 0 ? (
            <p className="muted">Sem projeção disponível.</p>
          ) : (
            <>
              <div style={{ width: '100%', height: 260 }}>
                <ResponsiveContainer>
                  <BarChart data={cfData} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e6e9ed" vertical={false} />
                    <XAxis dataKey="label" tick={{ fontSize: 12, fill: '#64717e' }} axisLine={{ stroke: '#dde2e8' }} tickLine={false} />
                    <YAxis tickFormatter={(v) => brlShort(Number(v))} tick={{ fontSize: 12, fill: '#64717e' }} axisLine={false} tickLine={false} width={54} />
                    <Tooltip formatter={(v: number | string) => brl(Number(v))} contentStyle={{ borderRadius: 8, border: '1px solid #dde2e8' }} />
                    <Bar dataKey="projected" name="Projetado" fill="#2f6fb0" radius={[4, 4, 0, 0]} maxBarSize={64} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
              <table className="table" style={{ marginTop: '0.5rem' }}>
                <thead><tr><th>Horizonte</th><th className="num">Saldo</th><th className="num">Entradas</th><th className="num">Saídas</th><th className="num">Projetado</th></tr></thead>
                <tbody>
                  {cfData.map((c) => (
                    <tr key={c.horizonDays}>
                      <td>{c.label}</td>
                      <td className="num">{brl(c.opening)}</td>
                      <td className="num" style={{ color: '#2f9e6b' }}>+{brl(c.expectedInflows)}</td>
                      <td className="num" style={{ color: '#d0483c' }}>−{brl(c.expectedOutflows)}</td>
                      <td className="num"><strong>{brl(c.projected)}</strong></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </Panel>
      </div>

      <div className="grid cols-2 rise rise-4" style={{ marginTop: '1rem', alignItems: 'start' }}>
        <Panel title="Contas e caixas" flush>
          {accounts.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhuma conta ainda.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Conta</th><th>Tipo</th><th className="num">Saldo</th></tr></thead>
              <tbody>
                {accounts.map((a) => (
                  <tr key={a.id}>
                    <td>{a.name}</td>
                    <td><span className={`badge ${a.kind === 'cash' ? 'warn' : 'muted'}`}>{a.kind === 'cash' ? 'caixa' : 'banco'}</span></td>
                    <td className="num"><strong>{brl(a.balance)}</strong></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <Panel title="Nova conta">
            <form onSubmit={addAccount}>
              <div className="field"><OrganizationPicker token={token} value={organizationId} onChange={setOrganizationId} /></div>
              <div style={{ display: 'flex', gap: '0.75rem' }}>
                <div className="field" style={{ flex: 1 }}>
                  <label htmlFor="acc-name">Nome</label>
                  <input id="acc-name" value={name} onChange={(e) => setName(e.target.value)} required />
                </div>
                <div className="field" style={{ width: 120 }}>
                  <label htmlFor="acc-kind">Tipo</label>
                  <select id="acc-kind" value={kind} onChange={(e) => setKind(e.target.value)}>
                    <option value="bank">Banco</option>
                    <option value="cash">Caixa</option>
                  </select>
                </div>
                <div className="field" style={{ width: 140 }}>
                  <label htmlFor="acc-open">Saldo inicial</label>
                  <input id="acc-open" type="number" step="0.01" value={opening} onChange={(e) => setOpening(e.target.value)} />
                </div>
              </div>
              <button className="btn btn-primary" type="submit">Criar conta</button>
            </form>
          </Panel>

          <Panel title="Transferência interna">
            <form onSubmit={doTransfer}>
              <div style={{ display: 'flex', gap: '0.75rem' }}>
                <div className="field" style={{ flex: 1 }}>
                  <label htmlFor="tr-from">De</label>
                  <select id="tr-from" value={fromId} onChange={(e) => setFromId(e.target.value)}>
                    <option value="">—</option>
                    {accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                  </select>
                </div>
                <div className="field" style={{ flex: 1 }}>
                  <label htmlFor="tr-to">Para</label>
                  <select id="tr-to" value={toId} onChange={(e) => setToId(e.target.value)}>
                    <option value="">—</option>
                    {accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                  </select>
                </div>
                <div className="field" style={{ width: 140 }}>
                  <label htmlFor="tr-amt">Valor</label>
                  <input id="tr-amt" type="number" step="0.01" min="0.01" value={transferAmount} onChange={(e) => setTransferAmount(e.target.value)} />
                </div>
              </div>
              <button className="btn btn-ghost" type="submit">Transferir</button>
            </form>
          </Panel>
        </div>
      </div>
    </>
  );
}
