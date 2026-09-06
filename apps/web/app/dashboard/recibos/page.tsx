'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { listReceipts, type LoginResult, type ReceiptSummary } from '../../lib/api';

export default function RecibosPage() {
  const [items, setItems] = useState<ReceiptSummary[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const token = (JSON.parse(raw) as LoginResult).accessToken;
    listReceipts(token)
      .then(setItems)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, []);

  return (
    <main className="container" style={{ maxWidth: 720 }}>
      <p className="muted"><Link href="/dashboard">← Painel</Link></p>
      <h1>Recibos</h1>
      {error && <p style={{ color: '#ff7a7a' }}>{error}</p>}
      {items.length === 0 ? (
        <p className="muted">Nenhum recibo emitido ainda (são gerados na confirmação do pagamento).</p>
      ) : (
        <div className="card">
          {items.map((r) => (
            <div key={r.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '0.5rem 0', borderBottom: '1px solid var(--border)' }}>
              <span>
                <strong>{r.number}</strong> <span className="muted">· {r.donorName}</span>
              </span>
              <span style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
                <span>R$ {r.amount.toFixed(2)}</span>
                <Link href={`/recibo/${r.id}`}>abrir</Link>
              </span>
            </div>
          ))}
        </div>
      )}
    </main>
  );
}
