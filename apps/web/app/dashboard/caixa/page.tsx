'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ListCard, PageHeader } from '../../components/Fiori';
import { Panel } from '../../components/Panel';
import {
  closeCashSession, createManualEntry, depositCashSession, listCashSessions, listTreasuryAccounts, openCashSession,
  ENTRY_TYPES, type CashLine, type CashSession, type EntryType, type LoginResult, type TreasuryAccount,
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

  // Lançamento manual de entrada (D-05/D-06).
  const [meAccountId, setMeAccountId] = useState('');
  const [meAmount, setMeAmount] = useState('');
  const [meType, setMeType] = useState<EntryType>('donation');
  const [meDonor, setMeDonor] = useState('');
  const [meMsg, setMeMsg] = useState<string | null>(null);

  // Fechamento de caixa com discriminação por tipo (D-06).
  const [closing, setClosing] = useState<{ id: string; counted: string; tithe: string; offering: string; donation: string } | null>(null);

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
      await createManualEntry(token, { treasuryAccountId: meAccountId, amount: value, entryType: meType, donorName: meDonor || undefined });
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

  async function submitClose(e: React.FormEvent) {
    e.preventDefault();
    if (!token || !closing) return;
    const counted = Number(closing.counted.replace(',', '.'));
    if (!(counted >= 0)) return setError('Valor conferido inválido.');
    const num = (v: string) => Number((v || '').replace(',', '.')) || 0;
    const raw: { entryType: EntryType; amount: number }[] = [
      { entryType: 'tithe', amount: num(closing.tithe) },
      { entryType: 'offering', amount: num(closing.offering) },
      { entryType: 'donation', amount: num(closing.donation) },
    ];
    const lines: CashLine[] = raw.filter((l) => l.amount > 0);
    if (lines.length > 0) {
      const sum = lines.reduce((acc, l) => acc + l.amount, 0);
      if (Math.abs(sum - counted) > 0.005) return setError('A soma da discriminação deve ser igual ao valor conferido.');
    }
    setError(null);
    try {
      await closeCashSession(token, closing.id, { countedAmount: counted, lines: lines.length ? lines : undefined });
      setClosing(null);
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
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

      {closing && (
        <div className="rise" style={{ marginBottom: '1rem' }}>
          <Panel title="Fechar caixa (dupla conferência)" actions={<button className="btn btn-ghost btn-sm" onClick={() => setClosing(null)}>Cancelar</button>}>
            <p className="muted" style={{ marginTop: 0 }}>
              O fechamento é conferido por um 2º responsável. Informe o total conferido; opcionalmente
              discrimine por tipo (a soma deve bater com o total). Em branco = uma oferta agregada.
            </p>
            <form onSubmit={submitClose}>
              <div className="field" style={{ maxWidth: 220 }}>
                <label htmlFor="cl-counted">Total conferido (R$)</label>
                <input id="cl-counted" type="number" step="0.01" min="0" value={closing.counted} onChange={(e) => setClosing({ ...closing, counted: e.target.value })} required autoFocus />
              </div>
              <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                <div className="field" style={{ width: 160 }}>
                  <label htmlFor="cl-tithe">Dízimo (opcional)</label>
                  <input id="cl-tithe" type="number" step="0.01" min="0" value={closing.tithe} onChange={(e) => setClosing({ ...closing, tithe: e.target.value })} />
                </div>
                <div className="field" style={{ width: 160 }}>
                  <label htmlFor="cl-off">Oferta (opcional)</label>
                  <input id="cl-off" type="number" step="0.01" min="0" value={closing.offering} onChange={(e) => setClosing({ ...closing, offering: e.target.value })} />
                </div>
                <div className="field" style={{ width: 160 }}>
                  <label htmlFor="cl-don">Doação (opcional)</label>
                  <input id="cl-don" type="number" step="0.01" min="0" value={closing.donation} onChange={(e) => setClosing({ ...closing, donation: e.target.value })} />
                </div>
              </div>
              <button className="btn btn-primary" type="submit">Confirmar fechamento</button>
            </form>
          </Panel>
        </div>
      )}

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
                <div className="field" style={{ width: 150 }}>
                  <label htmlFor="me-type">Tipo</label>
                  <select id="me-type" value={meType} onChange={(e) => setMeType(e.target.value as EntryType)}>
                    {ENTRY_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                  </select>
                </div>
              </div>
              <div className="field">
                <label htmlFor="me-donor">Doador (opcional — em branco = anônimo, sem recibo)</label>
                <input id="me-donor" value={meDonor} onChange={(e) => setMeDonor(e.target.value)} />
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
                      {s.status === 'open' && <button className="btn btn-ghost btn-sm" onClick={() => setClosing({ id: s.id, counted: '', tithe: '', offering: '', donation: '' })}>Fechar</button>}
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
