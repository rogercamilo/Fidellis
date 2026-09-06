'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { FinanceNav } from '../../components/FinanceNav';
import { Panel } from '../../components/Panel';
import {
  closeCashSession, depositCashSession, listCashSessions, listTreasuryAccounts, openCashSession,
  type CashSession, type LoginResult, type TreasuryAccount,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const dt = (s: string | null) => (s ? new Date(s).toLocaleString('pt-BR') : '—');

export default function CaixaPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sessions, setSessions] = useState<CashSession[]>([]);
  const [accounts, setAccounts] = useState<TreasuryAccount[]>([]);

  const [cashAccountId, setCashAccountId] = useState('');
  const [eventLabel, setEventLabel] = useState('');
  const [bankAccountId, setBankAccountId] = useState('');

  const cashAccounts = useMemo(() => accounts.filter((a) => a.kind === 'cash'), [accounts]);
  const bankAccounts = useMemo(() => accounts.filter((a) => a.kind === 'bank'), [accounts]);
  const accountName = useMemo(() => new Map(accounts.map((a) => [a.id, a.name])), [accounts]);

  const refresh = useCallback(async (t: string) => {
    try {
      const [ss, ac] = await Promise.all([listCashSessions(t), listTreasuryAccounts(t)]);
      setSessions(ss); setAccounts(ac);
      if (!cashAccountId && ac.some((a) => a.kind === 'cash')) setCashAccountId(ac.find((a) => a.kind === 'cash')!.id);
      if (!bankAccountId && ac.some((a) => a.kind === 'bank')) setBankAccountId(ac.find((a) => a.kind === 'bank')!.id);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao carregar.'); }
  }, [cashAccountId, bankAccountId]);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) { const t = (JSON.parse(raw) as LoginResult).accessToken; setToken(t); void refresh(t); }
  }, [refresh]);

  async function open(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!cashAccountId) return setError('Selecione um caixa.');
    setError(null);
    try { await openCashSession(token, { accountId: cashAccountId, eventLabel: eventLabel || undefined }); setEventLabel(''); await refresh(token); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  async function close(s: CashSession) {
    if (!token) return;
    const input = window.prompt('Valor conferido no fechamento (dupla conferência — feito por um 2º responsável):', '');
    if (input === null) return;
    const value = Number(input.replace(',', '.'));
    if (!(value >= 0)) return setError('Valor inválido.');
    setError(null);
    try { await closeCashSession(token, s.id, { countedAmount: value }); await refresh(token); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  async function deposit(s: CashSession) {
    if (!token) return;
    if (!bankAccountId) return setError('Cadastre/selecione uma conta bancária para o depósito.');
    setError(null);
    try { await depositCashSession(token, s.id, { bankAccountId }); await refresh(token); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Caixa físico</h1>
          <p className="subtitle">Coleta/oferta em espécie: abrir sessão, fechar com dupla conferência e depositar no banco.</p>
        </div>
      </div>

      <FinanceNav />

      {error && <p className="error-text">{error}</p>}

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        <Panel title="Abrir sessão de caixa">
          {cashAccounts.length === 0 ? (
            <p className="muted">Nenhum caixa (conta tipo &quot;caixa&quot;). Crie um em Tesouraria.</p>
          ) : (
            <form onSubmit={open}>
              <div className="field">
                <label htmlFor="cx-acc">Caixa</label>
                <select id="cx-acc" value={cashAccountId} onChange={(e) => setCashAccountId(e.target.value)}>
                  {cashAccounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                </select>
              </div>
              <div className="field">
                <label htmlFor="cx-ev">Evento (opcional)</label>
                <input id="cx-ev" placeholder="Ex.: Missa dom 10h" value={eventLabel} onChange={(e) => setEventLabel(e.target.value)} />
              </div>
              <button className="btn btn-primary" type="submit">Abrir caixa</button>
            </form>
          )}
        </Panel>

        <Panel title="Depósito">
          <p className="muted" style={{ marginTop: 0 }}>Ao depositar uma sessão fechada, o valor é transferido do caixa para a conta bancária escolhida.</p>
          <div className="field">
            <label htmlFor="cx-bank">Conta bancária de depósito</label>
            <select id="cx-bank" value={bankAccountId} onChange={(e) => setBankAccountId(e.target.value)}>
              <option value="">—</option>
              {bankAccounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
            </select>
          </div>
        </Panel>
      </div>

      <div className="rise rise-3" style={{ marginTop: '1rem' }}>
        <Panel title="Sessões" flush>
          {sessions.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhuma sessão ainda.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Caixa / evento</th><th>Fechada em</th><th>Status</th><th className="num">Conferido</th><th></th></tr></thead>
              <tbody>
                {sessions.map((s) => (
                  <tr key={s.id}>
                    <td>{accountName.get(s.accountId) ?? '—'}{s.eventLabel && <><br /><span className="muted" style={{ fontSize: '0.75rem' }}>{s.eventLabel}</span></>}</td>
                    <td className="muted">{s.status === 'open' ? 'em aberto' : dt(s.closedAt)}</td>
                    <td><span className={`badge ${s.status === 'open' ? 'warn' : s.depositedMovementId ? 'ok' : 'muted'}`}>{s.status === 'open' ? 'aberta' : s.depositedMovementId ? 'depositada' : 'fechada'}</span></td>
                    <td className="num">{s.countedAmount != null ? brl(s.countedAmount) : '—'}</td>
                    <td className="num" style={{ whiteSpace: 'nowrap' }}>
                      {s.status === 'open' && <button className="btn btn-ghost btn-sm" onClick={() => close(s)}>Fechar</button>}
                      {s.status === 'closed' && !s.depositedMovementId && <button className="btn btn-primary btn-sm" onClick={() => deposit(s)}>Depositar</button>}
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
