'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Panel } from '../../components/Panel';
import { listReceipts, type LoginResult, type ReceiptSummary } from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

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
    <>
      <div className="page-head rise">
        <div>
          <h1>Recibos</h1>
          <p className="subtitle">Emitidos automaticamente na confirmação de cada pagamento.</p>
        </div>
      </div>

      <div className="rise rise-2">
        <Panel title="Recibos emitidos" actions={<span className="muted">{items.length} total</span>} flush>
          {error && <p className="error-text" style={{ padding: '1rem' }}>{error}</p>}
          {items.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>
              Nenhum recibo ainda — são gerados quando um pagamento é confirmado.
            </p>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Doador</th>
                  <th className="num">Valor</th>
                  <th className="num">Emitido</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((r) => (
                  <tr key={r.id}>
                    <td className="mono">{r.number}</td>
                    <td>{r.donorName}</td>
                    <td className="num">{brl(r.amount)}</td>
                    <td className="num muted">{new Date(r.issuedAt).toLocaleDateString('pt-BR')}</td>
                    <td className="num"><Link className="btn btn-ghost btn-sm" href={`/recibo/${r.id}`}>abrir</Link></td>
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
