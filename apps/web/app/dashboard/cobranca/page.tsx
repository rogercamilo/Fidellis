'use client';

import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { createDonation, getDonation, type DonationCheckout, type LoginResult } from '../../lib/api';

export default function CobrancaPage() {
  const [token, setToken] = useState<string | null>(null);
  const [organizationId, setOrganizationId] = useState('');
  const [amount, setAmount] = useState('');
  const [donorName, setDonorName] = useState('');
  const [donorEmail, setDonorEmail] = useState('');
  const [donorDocument, setDonorDocument] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [checkout, setCheckout] = useState<DonationCheckout | null>(null);
  const [status, setStatus] = useState<string>('pending');
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) setToken((JSON.parse(raw) as LoginResult).accessToken);
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, []);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!token) {
      setError('Sessão não encontrada. Faça login novamente.');
      return;
    }
    setLoading(true);
    try {
      const result = await createDonation(token, {
        organizationId,
        amount: Number(amount),
        donor: { name: donorName, email: donorEmail || undefined, document: donorDocument },
      });
      setCheckout(result);
      setStatus(result.status);
      startPolling(result.donationId);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro inesperado.');
    } finally {
      setLoading(false);
    }
  }

  function startPolling(donationId: string) {
    if (pollRef.current) clearInterval(pollRef.current);
    pollRef.current = setInterval(async () => {
      if (!token) return;
      try {
        const d = await getDonation(token, donationId);
        setStatus(d.status);
        if (d.status === 'paid' || d.status === 'failed') {
          if (pollRef.current) clearInterval(pollRef.current);
        }
      } catch {
        /* mantém o polling */
      }
    }, 3000);
  }

  return (
    <main className="container" style={{ maxWidth: 560 }}>
      <p className="muted">
        <Link href="/dashboard">← Painel</Link>
      </p>
      <h1>Nova cobrança (PIX)</h1>

      {!checkout ? (
        <form className="card" onSubmit={onSubmit}>
          <label htmlFor="org">Organization ID (unidade)</label>
          <input id="org" value={organizationId} onChange={(e) => setOrganizationId(e.target.value)} required
            placeholder="uuid da paróquia/unidade" />

          <label htmlFor="amount">Valor (R$)</label>
          <input id="amount" type="number" step="0.01" min="0.01" value={amount}
            onChange={(e) => setAmount(e.target.value)} required />

          <label htmlFor="dname">Doador — nome</label>
          <input id="dname" value={donorName} onChange={(e) => setDonorName(e.target.value)} required />

          <label htmlFor="ddoc">Doador — CPF/CNPJ</label>
          <input id="ddoc" value={donorDocument} onChange={(e) => setDonorDocument(e.target.value)} required />

          <label htmlFor="demail">Doador — e-mail (opcional)</label>
          <input id="demail" type="email" value={donorEmail} onChange={(e) => setDonorEmail(e.target.value)} />

          {error && <p style={{ color: '#ff7a7a', marginTop: '0.75rem' }}>{error}</p>}

          <button className="btn" type="submit" style={{ marginTop: '1rem', width: '100%' }} disabled={loading}>
            {loading ? 'Gerando…' : 'Gerar cobrança PIX'}
          </button>
        </form>
      ) : (
        <div className="card">
          <p className="muted">Status: <strong style={{ color: status === 'paid' ? 'var(--accent)' : 'var(--fg)' }}>{status}</strong></p>
          {checkout.qrCodeUrl && (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={checkout.qrCodeUrl} alt="QR Code PIX" style={{ width: 240, height: 240, background: '#fff', borderRadius: 8 }} />
          )}
          <label>PIX copia-e-cola</label>
          <textarea readOnly value={checkout.qrCode} rows={4} style={{ width: '100%' }} />
          <p className="muted" style={{ fontSize: '0.85rem' }}>
            {status === 'paid'
              ? 'Pagamento confirmado — doação conciliada.'
              : 'Aguardando pagamento… a página atualiza sozinha quando o PIX for confirmado (webhook).'}
          </p>
          <button className="btn" onClick={() => { setCheckout(null); setStatus('pending'); }}
            style={{ background: 'transparent', color: 'var(--accent)', border: '1px solid var(--border)' }}>
            Nova cobrança
          </button>
        </div>
      )}
    </main>
  );
}
