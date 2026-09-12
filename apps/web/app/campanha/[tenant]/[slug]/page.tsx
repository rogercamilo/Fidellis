'use client';

import { useEffect, useRef, useState } from 'react';
import {
  publicCampaign, publicCreateDonation, publicGetDonation,
  type CampaignProgress, type DonationCheckout,
} from '../../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function CampanhaPage({ params }: { params: { tenant: string; slug: string } }) {
  const { tenant, slug } = params;
  const [campaign, setCampaign] = useState<CampaignProgress | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [amount, setAmount] = useState('');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [document, setDocument] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [checkout, setCheckout] = useState<DonationCheckout | null>(null);
  const [status, setStatus] = useState('pending');
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    publicCampaign(tenant, slug)
      .then(setCampaign)
      .catch((e) => setLoadError(e instanceof Error ? e.message : 'Campanha não encontrada.'));
    return () => { if (pollRef.current) clearInterval(pollRef.current); };
  }, [tenant, slug]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!campaign) return;
    setError(null);
    setLoading(true);
    try {
      const result = await publicCreateDonation(tenant, {
        organizationId: campaign.organizationId,
        amount: Number(amount),
        donor: { name, email: email || undefined, document },
        campaignId: campaign.id,
      });
      setCheckout(result);
      setStatus(result.status);
      pollRef.current = setInterval(async () => {
        try {
          const d = await publicGetDonation(tenant, result.donationId);
          setStatus(d.status);
          if (d.status === 'paid' && pollRef.current) clearInterval(pollRef.current);
        } catch { /* ignore */ }
      }, 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro inesperado.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth-wrap" style={{ alignItems: 'start', paddingTop: '3rem' }}>
      <div style={{ width: '100%', maxWidth: 460 }}>
        <div className="auth-brand" style={{ justifyContent: 'center' }}>
          <span className="mark">F</span> Fidellis
        </div>

        <div className="auth-card rise">
          {loadError ? (
            <>
              <h1 style={{ fontSize: '1.3rem' }}>Campanha indisponível</h1>
              <p className="muted">{loadError}</p>
            </>
          ) : !campaign ? (
            <p className="muted">Carregando…</p>
          ) : checkout ? (
            <div style={{ display: 'grid', placeItems: 'center', gap: '0.75rem' }}>
              <h1 style={{ fontSize: '1.3rem' }}>{status === 'paid' ? 'Obrigado! 🙏' : 'Escaneie para pagar'}</h1>
              {status !== 'paid' && checkout.qrCodeUrl && (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={checkout.qrCodeUrl} alt="QR PIX" style={{ width: 220, height: 220, background: '#fff', borderRadius: 10, border: '1px solid var(--border)', padding: 8 }} />
              )}
              {status !== 'paid' && (
                <div className="field" style={{ width: '100%' }}>
                  <label>PIX copia-e-cola</label>
                  <textarea readOnly value={checkout.qrCode} rows={4} className="mono" style={{ fontSize: '0.78rem' }} />
                </div>
              )}
              <p className="hint" style={{ textAlign: 'center' }}>
                {status === 'paid' ? 'Pagamento confirmado — o recibo foi enviado por e-mail.' : 'Aguardando confirmação do pagamento…'}
              </p>
            </div>
          ) : (
            <>
              <h1 style={{ fontSize: '1.4rem' }}>{campaign.title}</h1>
              {campaign.description && <p className="muted" style={{ marginTop: '-0.25rem' }}>{campaign.description}</p>}

              {campaign.goalAmount != null && (
                <div style={{ margin: '1rem 0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem' }}>
                    <strong>{brl(campaign.raised)}</strong>
                    <span className="muted">meta {brl(campaign.goalAmount)} · {campaign.percent}%</span>
                  </div>
                  <div style={{ height: 10, background: 'var(--border)', borderRadius: 5, overflow: 'hidden', marginTop: 4 }}>
                    <div style={{ width: `${Math.min(100, campaign.percent)}%`, height: '100%', background: 'var(--gold-bright, #c8a24a)' }} />
                  </div>
                </div>
              )}

              <form onSubmit={onSubmit} style={{ marginTop: '1rem' }}>
                <div className="field">
                  <label htmlFor="amount">Valor (R$)</label>
                  <input id="amount" type="number" step="0.01" min="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} required />
                </div>
                <div className="field">
                  <label htmlFor="name">Seu nome</label>
                  <input id="name" value={name} onChange={(e) => setName(e.target.value)} required />
                </div>
                <div className="field">
                  <label htmlFor="doc">CPF/CNPJ</label>
                  <input id="doc" value={document} onChange={(e) => setDocument(e.target.value)} required />
                </div>
                <div className="field">
                  <label htmlFor="email">E-mail (para o recibo)</label>
                  <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
                </div>
                {error && <p className="error-text">{error}</p>}
                <button className="btn btn-primary" type="submit" style={{ width: '100%', marginTop: '0.5rem' }} disabled={loading}>
                  {loading ? 'Gerando…' : 'Doar com PIX'}
                </button>
              </form>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
