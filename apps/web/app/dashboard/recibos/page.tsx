'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import { FilterBar, ListCard, PageHeader } from '../../components/Fiori';
import { listReceipts, type LoginResult, type ReceiptSummary } from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function RecibosPage() {
  const [items, setItems] = useState<ReceiptSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [q, setQ] = useState('');

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const token = (JSON.parse(raw) as LoginResult).accessToken;
    listReceipts(token)
      .then(setItems)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, []);

  const filtered = useMemo(() => {
    const term = q.trim().toLowerCase();
    if (!term) return items;
    return items.filter((r) => r.number.toLowerCase().includes(term) || r.donorName.toLowerCase().includes(term));
  }, [items, q]);

  return (
    <>
      <PageHeader
        title="Recibos"
        subtitle="Comprovantes emitidos automaticamente quando cada pagamento é confirmado."
      />

      <FilterBar>
        <div className="grow" />
        <input
          className="filter-search"
          placeholder="Buscar por número ou doador…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
      </FilterBar>

      <ListCard label="Recibos" count={filtered.length}>
        {error && <p className="error-text" style={{ padding: '1rem' }}>{error}</p>}
        {filtered.length === 0 ? (
          <p className="muted" style={{ padding: '1.25rem' }}>
            {items.length === 0
              ? 'Nenhum recibo ainda — são gerados quando um pagamento é confirmado.'
              : 'Nenhum recibo corresponde à busca.'}
          </p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Número</th>
                <th>Doador</th>
                <th className="num">Valor</th>
                <th className="num">Emitido em</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {filtered.map((r) => (
                <tr key={r.id}>
                  <td className="mono">{r.number}</td>
                  <td>{r.donorName}</td>
                  <td className="num">{brl(r.amount)}</td>
                  <td className="num muted">{new Date(r.issuedAt).toLocaleDateString('pt-BR')}</td>
                  <td className="num"><Link className="btn btn-ghost btn-sm" href={`/recibo/${r.id}`}>Abrir</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </ListCard>
    </>
  );
}
