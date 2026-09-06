'use client';

import { use, useEffect, useState } from 'react';
import { getReceipt, type LoginResult, type ReceiptDetail } from '../../lib/api';

export default function ReceiptPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const [receipt, setReceipt] = useState<ReceiptDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) {
      setError('Sessão não encontrada. Faça login.');
      return;
    }
    const token = (JSON.parse(raw) as LoginResult).accessToken;
    getReceipt(token, id)
      .then(setReceipt)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, [id]);

  if (error) return <main className="container"><p style={{ color: '#ff7a7a' }}>{error}</p></main>;
  if (!receipt) return <main className="container"><p className="muted">Carregando…</p></main>;

  return (
    <main className="container" style={{ maxWidth: 640 }}>
      <div className="receipt-actions" style={{ marginBottom: '1rem' }}>
        <button className="btn" onClick={() => window.print()}>Imprimir / Salvar PDF</button>
      </div>

      <div className="receipt card" style={{ background: '#fff', color: '#111' }}>
        <h2 style={{ marginTop: 0 }}>Recibo de Doação</h2>
        <p style={{ margin: '0 0 1rem' }}>Nº <strong>{receipt.number}</strong></p>

        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <tbody>
            <Row label="Instituição / Unidade" value={receipt.organizationName ?? '—'} />
            <Row label="Doador" value={receipt.donorName} />
            <Row label="Documento" value={receipt.donorDocument ?? '—'} />
            <Row label="Valor" value={`R$ ${receipt.amount.toFixed(2)}`} />
            <Row label="Data" value={new Date(receipt.issuedAt).toLocaleDateString('pt-BR')} />
          </tbody>
        </table>

        <p style={{ marginTop: '1.5rem', fontSize: '0.9rem' }}>
          Recebemos a importância acima a título de doação. Este recibo comprova a contribuição para fins
          de prestação de contas.
        </p>
      </div>

      <style>{`
        @media print {
          .receipt-actions { display: none; }
          body { background: #fff; }
          .receipt { border: none !important; box-shadow: none; }
        }
      `}</style>
    </main>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <tr>
      <td style={{ padding: '6px 0', color: '#555', width: '40%' }}>{label}</td>
      <td style={{ padding: '6px 0', fontWeight: 600 }}>{value}</td>
    </tr>
  );
}
