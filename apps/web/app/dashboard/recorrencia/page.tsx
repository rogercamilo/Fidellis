'use client';

import { useCallback, useEffect, useState } from 'react';
import { OrganizationPicker } from '../../components/OrganizationPicker';
import { Panel } from '../../components/Panel';
import {
  actOnRecurring,
  createRecurring,
  listRecurring,
  type LoginResult,
  type RecurringDonation,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

function statusBadge(status: string): string {
  if (status === 'active') return 'ok';
  if (status === 'past_due') return 'err';
  if (status === 'canceled') return 'muted';
  return 'warn';
}

export default function RecorrenciaPage() {
  const [token, setToken] = useState<string | null>(null);
  const [organizationId, setOrganizationId] = useState('');
  const [amount, setAmount] = useState('');
  const [dayOfMonth, setDayOfMonth] = useState('5');
  const [donorName, setDonorName] = useState('');
  const [donorEmail, setDonorEmail] = useState('');
  const [donorDocument, setDonorDocument] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<RecurringDonation[]>([]);

  const refresh = useCallback(async (t: string) => {
    try {
      setItems(await listRecurring(t));
    } catch {
      /* ignore */
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

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!token) return setError('Sessão não encontrada. Faça login novamente.');
    if (!organizationId) return setError('Selecione ou crie uma unidade.');
    setLoading(true);
    try {
      await createRecurring(token, {
        organizationId,
        amount: Number(amount),
        dayOfMonth: Number(dayOfMonth),
        donor: { name: donorName, email: donorEmail || undefined, document: donorDocument },
      });
      setAmount('');
      setDonorName('');
      setDonorEmail('');
      setDonorDocument('');
      await refresh(token);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro inesperado.');
    } finally {
      setLoading(false);
    }
  }

  async function act(id: string, action: 'pause' | 'resume' | 'cancel') {
    if (!token) return;
    try {
      await actOnRecurring(token, id, action);
      await refresh(token);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro inesperado.');
    }
  }

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Dízimo recorrente</h1>
          <p className="subtitle">
            Cobrança PIX gerada a cada ciclo; falhas entram na régua de dunning (D+1, D+3, D+5).
          </p>
        </div>
      </div>

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        <Panel title="Nova recorrência">
          <form onSubmit={onSubmit}>
            <div className="field">
              <OrganizationPicker token={token} value={organizationId} onChange={setOrganizationId} />
            </div>
            <div style={{ display: 'flex', gap: '1rem' }}>
              <div className="field" style={{ flex: 1 }}>
                <label htmlFor="amount">Valor (R$)</label>
                <input id="amount" type="number" step="0.01" min="0.01" value={amount}
                  onChange={(e) => setAmount(e.target.value)} required />
              </div>
              <div className="field" style={{ width: 130 }}>
                <label htmlFor="day">Dia do mês</label>
                <input id="day" type="number" min="1" max="31" value={dayOfMonth}
                  onChange={(e) => setDayOfMonth(e.target.value)} required />
              </div>
            </div>
            <div className="field">
              <label htmlFor="dname">Doador — nome</label>
              <input id="dname" value={donorName} onChange={(e) => setDonorName(e.target.value)} required />
            </div>
            <div className="field">
              <label htmlFor="ddoc">Doador — CPF/CNPJ</label>
              <input id="ddoc" value={donorDocument} onChange={(e) => setDonorDocument(e.target.value)} required />
            </div>
            <div className="field">
              <label htmlFor="demail">Doador — e-mail (opcional)</label>
              <input id="demail" type="email" value={donorEmail} onChange={(e) => setDonorEmail(e.target.value)} />
            </div>

            {error && <p className="error-text">{error}</p>}

            <button className="btn btn-primary" type="submit" style={{ marginTop: '0.5rem' }} disabled={loading}>
              {loading ? 'Criando…' : 'Criar recorrência'}
            </button>
          </form>
        </Panel>

        <Panel title="Recorrências" flush>
          {items.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhuma recorrência ainda.</p>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Valor / dia</th>
                  <th>Status</th>
                  <th>Próxima</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((r) => (
                  <tr key={r.id}>
                    <td>
                      <strong>{brl(r.amount)}</strong> <span className="muted">/ dia {r.dayOfMonth}</span>
                    </td>
                    <td>
                      <span className={`badge ${statusBadge(r.status)}`}>{r.status}</span>
                      {r.attempt > 0 && <span className="muted" style={{ fontSize: '0.75rem' }}> · tent. {r.attempt}</span>}
                    </td>
                    <td className="muted">{new Date(r.nextChargeAt).toLocaleDateString('pt-BR')}</td>
                    <td className="num" style={{ whiteSpace: 'nowrap' }}>
                      {r.status === 'active' && <button className="btn btn-ghost btn-sm" onClick={() => act(r.id, 'pause')}>Pausar</button>}
                      {(r.status === 'paused' || r.status === 'past_due') && <button className="btn btn-ghost btn-sm" onClick={() => act(r.id, 'resume')}>Retomar</button>}
                      {r.status !== 'canceled' && <button className="btn btn-ghost btn-sm" style={{ marginLeft: 6 }} onClick={() => act(r.id, 'cancel')}>Cancelar</button>}
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
