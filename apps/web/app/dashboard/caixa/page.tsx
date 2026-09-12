'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ListCard, PageHeader } from '../../components/Fiori';
import { Panel } from '../../components/Panel';
import {
  closeCashSession, createManualEntry, depositCashSession, listCashSessions, listTreasuryAccounts, openCashSession,
  type CashSession, type LoginResult, type TreasuryAccount,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const dt = (s: string | null) => (s ? new Date(s).toLocaleString('pt-BR') : '—');
const LAUNCHER_ROLES = ['admin', 'coordinator'];

export default function CaixaPage() {
  const [token, setToken] = useState<string | null>(null);
  const [role, setRole] = useState<string | undefined>();
  const [error, setError] = useState<string | null>(null);
  const [sessions, setSessions] = useState<CashSession[]>([]);
  const [accounts, setAccounts] = useState<TreasuryAccount[]>([]);

  const [cashAccountId, setCashAccountId] = useState('');
  const [eventLabel, setEventLabel] = useState('');
  const [bankAccountId, setBankAccountId] = useState('');

  // Lançamento manual de entrada (D-05).
  const [meAccountId, setMeAccountId] = useState('');
  const [meAmount, setMeAmount] = useState('');
  const [meDonor, setMeDonor] = useState('');
  const [meMsg, setMeMsg] = useState<string | null>(null);

  const canLaunch = !role || LAUNCHER_ROLES.includes(role);

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
    if (raw) {
      const s = JSON.parse(raw) as LoginResult;
      setToken(s.accessToken);
      setRole(s.tenants.find((x) => x.slug === s.activeTenant)?.role);
      void refresh(s.accessToken);
    }
  }, [refresh]);

  async function addManualEntry(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    const value = Number(meAmount.replace(',', '.'));
    if (!meAccountId) return setError('Selecione a conta de destino.');
    if (!(value > 0)) return setError('Informe um valor positivo.');
    setError(null); setMeMsg(null);
    try {
      await createManualEntry(token, { treasuryAccountId: meAccountId, amount: value, donorName: meDonor || undefined });
      setMeAmount(''); setMeDonor('');
      setMeMsg('Entrada registrada.');
      setTimeout(() => setMeMsg(null), 2500);
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao lançar entrada.'); }
  }

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
      <PageHeader
        title="Caixa físico"
        subtitle="Coletas em espécie: abra a sessão, feche com dupla conferência e deposite o valor no banco."
      />

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

        {canLaunch && (
          <Panel title="Lançamento manual de entrada" actions={meMsg && <span className="badge ok">{meMsg}</span>}>
            <p className="muted" style={{ marginTop: 0 }}>Recebimento fora do PSP (transferência, depósito avulso). Vira receita de 1ª classe na conta escolhida; recibo só quando há doador informado.</p>
            <form onSubmit={addManualEntry}>
              <div className="field">
                <label htmlFor="me-acc">Conta de destino</label>
                <select id="me-acc" value={meAccountId} onChange={(e) => setMeAccountId(e.target.value)} required>
                  <option value="">—</option>
                  {accounts.map((a) => <option key={a.id} value={a.id}>{a.name} ({a.kind === 'cash' ? 'caixa' : 'banco'})</option>)}
                </select>
              </div>
              <div style={{ display: 'flex', gap: '0.75rem' }}>
                <div className="field" style={{ flex: 1 }}>
                  <label htmlFor="me-amt">Valor (R$)</label>
                  <input id="me-amt" type="number" step="0.01" min="0.01" value={meAmount} onChange={(e) => setMeAmount(e.target.value)} required />
                </div>
                <div className="field" style={{ flex: 1 }}>
                  <label htmlFor="me-donor">Doador (opcional)</label>
                  <input id="me-donor" placeholder="Nome — deixa em branco p/ anônimo" value={meDonor} onChange={(e) => setMeDonor(e.target.value)} />
                </div>
              </div>
              <button className="btn btn-primary" type="submit">Lançar entrada</button>
            </form>
          </Panel>
        )}
      </div>

      <div className="rise rise-3" style={{ marginTop: '1rem' }}>
        <ListCard label="Sessões" count={sessions.length}>
          {sessions.length === 0 ? (
            <p className="muted" style={{ padding: '1.25rem' }}>Nenhuma sessão ainda. Abra a primeira acima.</p>
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
        </ListCard>
      </div>
    </>
  );
}
