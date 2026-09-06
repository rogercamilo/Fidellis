'use client';

import { use, useEffect, useState } from 'react';
import { getReceipt, type LoginResult, type ReceiptDetail } from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

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

  if (error) return <main className="container"><p className="error-text">{error}</p></main>;
  if (!receipt) return <main className="container"><p className="muted">Carregando…</p></main>;

  return (
    <main style={{ maxWidth: 680, margin: '0 auto', padding: '2rem 1.25rem' }}>
      <div className="no-print" style={{ display: 'flex', gap: '0.6rem', marginBottom: '1.25rem' }}>
        <button className="btn btn-primary" onClick={() => window.print()}>Imprimir / Salvar PDF</button>
        <a className="btn btn-ghost" href="/dashboard/recibos">Voltar</a>
      </div>

      <article
        style={{
          background: '#fff',
          border: '1px solid var(--border)',
          borderRadius: 'var(--r-lg)',
          boxShadow: 'var(--shadow-md)',
          padding: '2.25rem',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', borderBottom: '2px solid var(--gold)', paddingBottom: '1rem', marginBottom: '1.5rem' }}>
          <div>
            <div style={{ fontFamily: 'var(--font-display)', fontSize: '1.5rem', fontWeight: 700 }}>Fidellis</div>
            <div className="muted" style={{ fontSize: '0.85rem' }}>Recibo de Doação</div>
          </div>
          <div style={{ textAlign: 'right' }}>
            <div className="muted" style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Nº do recibo</div>
            <div className="mono" style={{ fontSize: '1.15rem', fontWeight: 600 }}>{receipt.number}</div>
          </div>
        </div>

        <dl className="kv" style={{ gridTemplateColumns: 'minmax(140px, 35%) 1fr' }}>
          <dt>Instituição / Unidade</dt>
          <dd>{receipt.organizationName ?? '—'}</dd>
          <dt>Doador</dt>
          <dd>{receipt.donorName}</dd>
          <dt>Documento</dt>
          <dd className="mono">{receipt.donorDocument ?? '—'}</dd>
          <dt>Data</dt>
          <dd>{new Date(receipt.issuedAt).toLocaleDateString('pt-BR')}</dd>
        </dl>

        <div style={{ marginTop: '1.5rem', padding: '1rem 1.25rem', background: 'var(--surface-2)', border: '1px solid var(--border)', borderRadius: 'var(--r)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span className="muted" style={{ textTransform: 'uppercase', letterSpacing: '0.06em', fontSize: '0.8rem' }}>Valor recebido</span>
          <span style={{ fontFamily: 'var(--font-display)', fontSize: '1.6rem', fontWeight: 700 }}>{brl(receipt.amount)}</span>
        </div>

        <p className="muted" style={{ marginTop: '1.5rem', fontSize: '0.88rem', lineHeight: 1.7 }}>
          Recebemos a importância acima a título de <strong>doação</strong>. Este recibo comprova a
          contribuição para fins de prestação de contas.
        </p>

        <div style={{ marginTop: '3rem', textAlign: 'center' }}>
          <div style={{ borderTop: '1px solid var(--border-strong)', width: 260, margin: '0 auto', paddingTop: '0.4rem' }} className="muted">
            {receipt.organizationName ?? 'Instituição'}
          </div>
        </div>
      </article>
    </main>
  );
}
