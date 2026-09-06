'use client';

import { useEffect, useRef, useState } from 'react';
import { OrganizationPicker } from '../../components/OrganizationPicker';
import { Panel } from '../../components/Panel';
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
    if (!token) return setError('Sessão não encontrada. Faça login novamente.');
    if (!organizationId) return setError('Selecione ou crie uma unidade.');
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

  const statusBadge = status === 'paid' ? 'ok' : status === 'failed' ? 'err' : 'warn';

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Nova cobrança</h1>
          <p className="subtitle">Gere um PIX avulso — a confirmação e o recibo são automáticos.</p>
        </div>
      </div>

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        {!checkout ? (
          <Panel title="Dados da cobrança">
            <form onSubmit={onSubmit}>
              <div className="field">
                <OrganizationPicker token={token} value={organizationId} onChange={setOrganizationId} />
              </div>
              <div className="field">
                <label htmlFor="amount">Valor (R$)</label>
                <input id="amount" type="number" step="0.01" min="0.01" value={amount}
                  onChange={(e) => setAmount(e.target.value)} required />
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

              <button className="btn btn-primary" type="submit" style={{ width: '100%', marginTop: '0.5rem' }} disabled={loading}>
                {loading ? 'Gerando…' : 'Gerar cobrança PIX'}
              </button>
            </form>
          </Panel>
        ) : (
          <Panel title="Cobrança PIX" actions={<span className={`badge ${statusBadge}`}>{status}</span>}>
            <div style={{ display: 'grid', placeItems: 'center', gap: '0.75rem' }}>
              {checkout.qrCodeUrl && (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={checkout.qrCodeUrl} alt="QR Code PIX"
                  style={{ width: 220, height: 220, background: '#fff', borderRadius: 10, border: '1px solid var(--border)', padding: 8 }} />
              )}
              <div className="field" style={{ width: '100%' }}>
                <label>PIX copia-e-cola</label>
                <textarea readOnly value={checkout.qrCode} rows={4} className="mono" style={{ fontSize: '0.8rem' }} />
              </div>
              <p className="hint" style={{ textAlign: 'center' }}>
                {status === 'paid'
                  ? 'Pagamento confirmado — doação conciliada e recibo emitido.'
                  : 'Aguardando pagamento… a página atualiza sozinha quando o PIX for confirmado.'}
              </p>
              <button className="btn btn-ghost" onClick={() => { setCheckout(null); setStatus('pending'); }}>
                Nova cobrança
              </button>
            </div>
          </Panel>
        )}

        <Panel title="Como funciona">
          <ol className="muted" style={{ margin: 0, paddingLeft: '1.1rem', lineHeight: 1.9 }}>
            <li>Selecione a unidade (paróquia/comunidade) que recebe.</li>
            <li>Informe o valor e os dados do doador.</li>
            <li>Exiba o QR / copia-e-cola ao doador.</li>
            <li>Na confirmação do PIX, geramos o <strong>recibo</strong> e os lançamentos contábeis.</li>
          </ol>
          <p className="hint" style={{ marginTop: '1rem' }}>
            A plataforma não retém taxa sobre a doação — a unidade fica com 100%.
          </p>
        </Panel>
      </div>
    </>
  );
}
