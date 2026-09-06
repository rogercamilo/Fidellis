'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { FinanceNav } from '../../components/FinanceNav';
import { Panel } from '../../components/Panel';
import {
  approvePayable, createPayable, createPayee, listApprovalTiers, listPayables, listPayees,
  listTreasuryAccounts, payPayable, rejectPayable,
  type ApprovalTier, type LoginResult, type Payable, type Payee, type TreasuryAccount,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const date = (s: string) => new Date(s + 'T00:00:00').toLocaleDateString('pt-BR');
const READ_ONLY_ROLES = ['fiscal_council', 'accountant'];

function statusBadge(status: string): string {
  if (status === 'paid') return 'ok';
  if (status === 'approved') return 'ok';
  if (status === 'rejected' || status === 'canceled') return 'muted';
  return 'warn';
}

export default function PagarPage() {
  const [token, setToken] = useState<string | null>(null);
  const [role, setRole] = useState<string | undefined>();
  const [error, setError] = useState<string | null>(null);

  const [payees, setPayees] = useState<Payee[]>([]);
  const [payables, setPayables] = useState<Payable[]>([]);
  const [tiers, setTiers] = useState<ApprovalTier[]>([]);
  const [accounts, setAccounts] = useState<TreasuryAccount[]>([]);
  const [payFrom, setPayFrom] = useState('');

  const [pName, setPName] = useState('');
  const [pDoc, setPDoc] = useState('');
  const [pPix, setPPix] = useState('');

  const [payeeId, setPayeeId] = useState('');
  const [amount, setAmount] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [description, setDescription] = useState('');

  const canWrite = !role || !READ_ONLY_ROLES.includes(role);
  const payeeName = useMemo(() => new Map(payees.map((p) => [p.id, p.name])), [payees]);

  const refresh = useCallback(async (t: string) => {
    try {
      const [pe, pa, ti, ac] = await Promise.all([listPayees(t), listPayables(t), listApprovalTiers(t), listTreasuryAccounts(t)]);
      setPayees(pe); setPayables(pa); setTiers(ti); setAccounts(ac);
      if (!payFrom && ac.length) setPayFrom(ac.find((a) => a.kind === 'bank')?.id ?? ac[0].id);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao carregar.'); }
  }, [payFrom]);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) {
      const s = JSON.parse(raw) as LoginResult;
      setToken(s.accessToken);
      setRole(s.tenants.find((x) => x.slug === s.activeTenant)?.role);
      void refresh(s.accessToken);
    }
  }, [refresh]);

  function guard(fn: () => Promise<unknown>) {
    return async () => {
      if (!token) return setError('Sessão não encontrada.');
      setError(null);
      try { await fn(); await refresh(token); }
      catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
    };
  }

  const addPayee = guard(async () => { await createPayee(token!, { name: pName, document: pDoc || undefined, pixKey: pPix || undefined }); setPName(''); setPDoc(''); setPPix(''); });

  async function addPayable(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!payeeId || !amount || !dueDate || !description) return setError('Credor, valor, vencimento e descrição são obrigatórios.');
    setError(null);
    try {
      await createPayable(token, { payeeId, amount: Number(amount), dueDate, description });
      setAmount(''); setDescription('');
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  const approve = (id: string) => guard(() => approvePayable(token!, id))();
  const reject = (id: string) => guard(() => rejectPayable(token!, id))();
  const pay = (id: string) => {
    if (!payFrom) { setError('Selecione a conta de pagamento.'); return; }
    return guard(() => payPayable(token!, id, { treasuryAccountId: payFrom }))();
  };

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Contas a Pagar</h1>
          <p className="subtitle">Credores, títulos e alçadas de aprovação (segregação de funções + dupla assinatura).</p>
        </div>
      </div>

      <FinanceNav />

      {error && <p className="error-text">{error}</p>}
      {!canWrite && <p className="muted" style={{ marginBottom: '0.5rem' }}>Perfil somente-leitura: ações de escrita ocultadas.</p>}

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        {canWrite && (
          <Panel title="Novo título">
            <form onSubmit={addPayable}>
              <div className="field">
                <label htmlFor="pa-payee">Credor</label>
                <select id="pa-payee" value={payeeId} onChange={(e) => setPayeeId(e.target.value)} required>
                  <option value="">—</option>
                  {payees.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </div>
              <div style={{ display: 'flex', gap: '0.75rem' }}>
                <div className="field" style={{ flex: 1 }}>
                  <label htmlFor="pa-amt">Valor (R$)</label>
                  <input id="pa-amt" type="number" step="0.01" min="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} required />
                </div>
                <div className="field" style={{ width: 160 }}>
                  <label htmlFor="pa-due">Vencimento</label>
                  <input id="pa-due" type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} required />
                </div>
              </div>
              <div className="field">
                <label htmlFor="pa-desc">Descrição</label>
                <input id="pa-desc" value={description} onChange={(e) => setDescription(e.target.value)} required />
              </div>
              <button className="btn btn-primary" type="submit">Lançar (aguarda aprovação)</button>
            </form>
          </Panel>
        )}

        <Panel title="Faixas de alçada" flush>
          <table className="table">
            <thead><tr><th>Faixa</th><th className="num">Assin.</th><th>Aprovadores</th></tr></thead>
            <tbody>
              {tiers.map((t) => (
                <tr key={t.id}>
                  <td>{brl(t.minAmount)} {t.maxAmount ? `– ${brl(t.maxAmount)}` : 'ou mais'}</td>
                  <td className="num">{t.signatures}</td>
                  <td className="muted">{t.rolesCsv}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Panel>

        {canWrite && (
          <Panel title="Novo credor">
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
              <input placeholder="Nome" style={{ flex: 1, minWidth: 160 }} value={pName} onChange={(e) => setPName(e.target.value)} />
              <input placeholder="CPF/CNPJ" style={{ width: 150 }} value={pDoc} onChange={(e) => setPDoc(e.target.value)} />
              <input placeholder="Chave PIX" style={{ width: 160 }} value={pPix} onChange={(e) => setPPix(e.target.value)} />
              <button className="btn btn-ghost btn-sm" onClick={addPayee} disabled={!pName}>Adicionar</button>
            </div>
            <ul className="list" style={{ marginTop: '0.75rem' }}>
              {payees.map((p) => <li key={p.id}>{p.name}{p.document && <span className="muted" style={{ marginLeft: 6, fontSize: '0.8rem' }}>· {p.document}</span>}</li>)}
            </ul>
          </Panel>
        )}
      </div>

      <div className="rise rise-3" style={{ marginTop: '1rem' }}>
        <Panel
          title="Títulos"
          flush
          actions={canWrite && (
            <span style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.8rem' }}>
              <span className="muted">Pagar de:</span>
              <select value={payFrom} onChange={(e) => setPayFrom(e.target.value)}>
                {accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
              </select>
            </span>
          )}
        >
          {payables.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum título ainda.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Descrição</th><th>Credor</th><th>Vencimento</th><th className="num">Valor</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {payables.map((p) => (
                  <tr key={p.id}>
                    <td>{p.description}</td>
                    <td className="muted">{payeeName.get(p.payeeId) ?? '—'}</td>
                    <td className="muted">{date(p.dueDate)}</td>
                    <td className="num"><strong>{brl(p.amount)}</strong></td>
                    <td><span className={`badge ${statusBadge(p.status)}`}>{p.status}</span></td>
                    <td className="num" style={{ whiteSpace: 'nowrap' }}>
                      {canWrite && p.status === 'awaiting_approval' && (
                        <>
                          <button className="btn btn-ghost btn-sm" onClick={() => approve(p.id)}>Aprovar</button>
                          <button className="btn btn-ghost btn-sm" style={{ marginLeft: 6 }} onClick={() => reject(p.id)}>Rejeitar</button>
                        </>
                      )}
                      {canWrite && p.status === 'approved' && <button className="btn btn-primary btn-sm" onClick={() => pay(p.id)}>Pagar</button>}
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
