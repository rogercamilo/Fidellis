'use client';

import { use, useEffect, useRef, useState } from 'react';
import {
  publicCreateDonation,
  publicGetDonation,
  publicOrganizations,
  type DonationCheckout,
  type PublicOrg,
} from '../../lib/api';

export default function DoarPage({ params }: { params: Promise<{ tenant: string }> }) {
  const { tenant } = use(params);
  const [orgs, setOrgs] = useState<PublicOrg[]>([]);
  const [organizationId, setOrganizationId] = useState('');
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
    publicOrganizations(tenant)
      .then((list) => {
        setOrgs(list);
        if (list[0]) setOrganizationId(list[0].id);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, [tenant]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!organizationId) return setError('Selecione uma unidade.');
    setLoading(true);
    try {
      const result = await publicCreateDonation(tenant, {
        organizationId,
        amount: Number(amount),
        donor: { name, email: email || undefined, document },
      });
      setCheckout(result);
      setStatus(result.status);
      pollRef.current = setInterval(async () => {
        try {
          const d = await publicGetDonation(tenant, result.donationId);
          setStatus(d.status);
          if (d.status === 'paid' && pollRef.current) clearInterval(pollRef.current);
        } catch {
          /* ignore */
        }
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
          {!checkout ? (
            <>
              <h1 style={{ fontSize: '1.4rem' }}>Fazer uma doação</h1>
              <p className="muted" style={{ marginTop: '-0.25rem' }}>Sua contribuição via PIX — 100% vai para a unidade.</p>
              <form onSubmit={onSubmit} style={{ marginTop: '1rem' }}>
                <div className="field">
                  <label htmlFor="org">Unidade</label>
                  <select id="org" value={organizationId} onChange={(e) => setOrganizationId(e.target.value)} required>
                    {orgs.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                  </select>
                </div>
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
          ) : (
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
          )}
        </div>

        <p className="muted" style={{ textAlign: 'center', marginTop: '1rem', fontSize: '0.85rem', color: '#aebccb' }}>
          <a href={`/portal/${tenant}`} style={{ color: 'var(--gold-bright)' }}>Já doou? Acesse seus recibos</a>
        </p>
      </div>
    </div>
  );
}
