'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import { OrganizationPicker } from '../../components/OrganizationPicker';
import {
  actOnRecurring,
  createRecurring,
  listRecurring,
  type LoginResult,
  type RecurringDonation,
} from '../../lib/api';

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
    if (!token) {
      setError('Sessão não encontrada. Faça login novamente.');
      return;
    }
    if (!organizationId) {
      setError('Selecione ou crie uma unidade.');
      return;
    }
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
    <main className="container" style={{ maxWidth: 720 }}>
      <p className="muted">
        <Link href="/dashboard">← Painel</Link>
      </p>
      <h1>Dízimo/oferta recorrente</h1>
      <p className="muted">
        A cobrança PIX é gerada a cada ciclo pelo servidor; falhas entram na régua de dunning
        (D+1, D+3, D+5) automaticamente.
      </p>

      <form className="card" onSubmit={onSubmit}>
        <OrganizationPicker token={token} value={organizationId} onChange={setOrganizationId} />

        <div style={{ display: 'flex', gap: '1rem' }}>
          <div style={{ flex: 1 }}>
            <label htmlFor="amount">Valor (R$)</label>
            <input id="amount" type="number" step="0.01" min="0.01" value={amount}
              onChange={(e) => setAmount(e.target.value)} required />
          </div>
          <div style={{ width: 140 }}>
            <label htmlFor="day">Dia do mês</label>
            <input id="day" type="number" min="1" max="31" value={dayOfMonth}
              onChange={(e) => setDayOfMonth(e.target.value)} required />
          </div>
        </div>

        <label htmlFor="dname">Doador — nome</label>
        <input id="dname" value={donorName} onChange={(e) => setDonorName(e.target.value)} required />

        <label htmlFor="ddoc">Doador — CPF/CNPJ</label>
        <input id="ddoc" value={donorDocument} onChange={(e) => setDonorDocument(e.target.value)} required />

        <label htmlFor="demail">Doador — e-mail (opcional)</label>
        <input id="demail" type="email" value={donorEmail} onChange={(e) => setDonorEmail(e.target.value)} />

        {error && <p style={{ color: '#ff7a7a', marginTop: '0.75rem' }}>{error}</p>}

        <button className="btn" type="submit" style={{ marginTop: '1rem' }} disabled={loading}>
          {loading ? 'Criando…' : 'Criar recorrência'}
        </button>
      </form>

      <h3 style={{ marginTop: '2rem' }}>Recorrências</h3>
      {items.length === 0 ? (
        <p className="muted">Nenhuma recorrência ainda.</p>
      ) : (
        <div className="card">
          {items.map((r) => (
            <div key={r.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '0.5rem 0', borderBottom: '1px solid var(--border)' }}>
              <div>
                <strong>R$ {r.amount.toFixed(2)}</strong> <span className="muted">/ dia {r.dayOfMonth}</span>
                <br />
                <span className="muted" style={{ fontSize: '0.85rem' }}>
                  {r.status} · próxima: {new Date(r.nextChargeAt).toLocaleDateString('pt-BR')}
                  {r.attempt > 0 ? ` · tentativa ${r.attempt}` : ''}
                </span>
              </div>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                {r.status === 'active' && <button className="btn" style={ghost} onClick={() => act(r.id, 'pause')}>Pausar</button>}
                {(r.status === 'paused' || r.status === 'past_due') && <button className="btn" style={ghost} onClick={() => act(r.id, 'resume')}>Retomar</button>}
                {r.status !== 'canceled' && <button className="btn" style={ghost} onClick={() => act(r.id, 'cancel')}>Cancelar</button>}
              </div>
            </div>
          ))}
        </div>
      )}
    </main>
  );
}

const ghost: React.CSSProperties = {
  background: 'transparent',
  color: 'var(--accent)',
  border: '1px solid var(--border)',
  padding: '0.35rem 0.7rem',
  fontSize: '0.85rem',
};
