'use client';

import { useEffect, useState } from 'react';
import {
  memberGive, memberPledge, portalMe, publicOrganizations, requestMagicLink,
  type DonationCheckout, type EntryType, type PortalData, type PublicOrg,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function PortalPage({ params }: { params: { tenant: string } }) {
  const { tenant } = params;
  const [token, setToken] = useState<string | null>(null);
  const [data, setData] = useState<PortalData | null>(null);
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Contribuição do membro (#75)
  const [orgs, setOrgs] = useState<PublicOrg[]>([]);
  const [orgId, setOrgId] = useState('');
  const [gType, setGType] = useState<EntryType>('tithe');
  const [gAmount, setGAmount] = useState('');
  const [pAmount, setPAmount] = useState('');
  const [pDay, setPDay] = useState('5');
  const [checkout, setCheckout] = useState<DonationCheckout | null>(null);
  const [msg, setMsg] = useState<string | null>(null);

  useEffect(() => {
    const t = new URLSearchParams(window.location.search).get('token');
    setToken(t);
    if (t) {
      portalMe(tenant, t)
        .then((d) => {
          setData(d);
          if (d.donor.isMember) publicOrganizations(tenant).then((o) => { setOrgs(o); if (o[0]) setOrgId(o[0].id); }).catch(() => {});
        })
        .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
    }
  }, [tenant]);

  async function onRequest(e: React.FormEvent) {
    e.preventDefault();
    await requestMagicLink(tenant, email);
    setSent(true);
  }

  async function give(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!orgId) return setError('Selecione a unidade.');
    setError(null); setMsg(null);
    try {
      const r = await memberGive(tenant, token, { organizationId: orgId, amount: Number(gAmount), entryType: gType });
      setCheckout(r); setGAmount('');
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro.'); }
  }

  async function pledge(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!orgId) return setError('Selecione a unidade.');
    setError(null); setMsg(null);
    try {
      await memberPledge(tenant, token, { organizationId: orgId, amount: Number(pAmount), dayOfMonth: Number(pDay) });
      setPAmount('');
      setMsg('Dízimo recorrente assinado — a cobrança do 1º ciclo foi gerada.');
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro.'); }
  }

  return (
    <div className="auth-wrap" style={{ alignItems: 'start', paddingTop: '3rem' }}>
      <div style={{ width: '100%', maxWidth: token ? 640 : 420 }}>
        <div className="auth-brand" style={{ justifyContent: 'center' }}>
          <span className="mark">F</span> Fidellis
        </div>

        {!token ? (
          <div className="auth-card rise">
            <h1 style={{ fontSize: '1.4rem' }}>Seus recibos</h1>
            <p className="muted" style={{ marginTop: '-0.25rem' }}>Enviaremos um link de acesso para o seu e-mail.</p>
            {sent ? (
              <p className="hint" style={{ marginTop: '1rem' }}>
                Se houver doações associadas a esse e-mail, você receberá um link em instantes.
              </p>
            ) : (
              <form onSubmit={onRequest} style={{ marginTop: '1rem' }}>
                <div className="field">
                  <label htmlFor="email">E-mail</label>
                  <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
                </div>
                <button className="btn btn-primary" type="submit" style={{ width: '100%' }}>Enviar link</button>
              </form>
            )}
          </div>
        ) : error ? (
          <div className="auth-card rise"><p className="error-text">{error}</p></div>
        ) : !data ? (
          <div className="auth-card rise"><p className="muted">Carregando…</p></div>
        ) : (
          <div className="rise" style={{ display: 'grid', gap: '1rem' }}>
            <div className="panel">
              <div className="panel-header"><h2 className="panel-title">Olá, {data.donor.name}</h2></div>
              <div className="panel-body"><p className="muted" style={{ margin: 0 }}>{data.donor.email}{data.donor.isMember && <span className="badge ok" style={{ marginLeft: 8 }}>membro</span>}</p></div>
            </div>

            {data.donor.isMember && (
              <div className="panel">
                <div className="panel-header"><h2 className="panel-title">Contribuir</h2></div>
                <div className="panel-body">
                  {error && <p className="error-text">{error}</p>}
                  {msg && <p className="badge ok" style={{ display: 'inline-block', marginBottom: '0.75rem' }}>{msg}</p>}
                  {checkout ? (
                    <div style={{ display: 'grid', placeItems: 'center', gap: '0.6rem' }}>
                      {checkout.qrCodeUrl && (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img src={checkout.qrCodeUrl} alt="QR PIX" style={{ width: 200, height: 200, background: '#fff', borderRadius: 10, border: '1px solid var(--border)', padding: 8 }} />
                      )}
                      <textarea readOnly value={checkout.qrCode} rows={3} className="mono" style={{ width: '100%', fontSize: '0.75rem' }} />
                      <button className="btn btn-ghost btn-sm" onClick={() => setCheckout(null)}>Nova contribuição</button>
                    </div>
                  ) : (
                    <>
                      {orgs.length > 1 && (
                        <div className="field">
                          <label htmlFor="p-org">Unidade</label>
                          <select id="p-org" value={orgId} onChange={(e) => setOrgId(e.target.value)}>
                            {orgs.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                          </select>
                        </div>
                      )}
                      <form onSubmit={give} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
                        <div className="field" style={{ width: 130, marginBottom: 0 }}>
                          <label htmlFor="g-type">Tipo</label>
                          <select id="g-type" value={gType} onChange={(e) => setGType(e.target.value as EntryType)}>
                            <option value="tithe">Dízimo</option>
                            <option value="offering">Oferta</option>
                          </select>
                        </div>
                        <div className="field" style={{ flex: 1, minWidth: 120, marginBottom: 0 }}>
                          <label htmlFor="g-amt">Valor (R$)</label>
                          <input id="g-amt" type="number" step="0.01" min="0.01" value={gAmount} onChange={(e) => setGAmount(e.target.value)} required />
                        </div>
                        <button className="btn btn-primary" type="submit">Contribuir com PIX</button>
                      </form>
                      <form onSubmit={pledge} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem', borderTop: '1px solid var(--border)', paddingTop: '0.75rem' }}>
                        <div className="field" style={{ flex: 1, minWidth: 120, marginBottom: 0 }}>
                          <label htmlFor="p-amt">Dízimo mensal (R$)</label>
                          <input id="p-amt" type="number" step="0.01" min="0.01" value={pAmount} onChange={(e) => setPAmount(e.target.value)} required />
                        </div>
                        <div className="field" style={{ width: 110, marginBottom: 0 }}>
                          <label htmlFor="p-day">Dia</label>
                          <input id="p-day" type="number" min="1" max="31" value={pDay} onChange={(e) => setPDay(e.target.value)} required />
                        </div>
                        <button className="btn btn-ghost" type="submit">Assinar recorrente</button>
                      </form>
                    </>
                  )}
                </div>
              </div>
            )}

            <div className="panel">
              <div className="panel-header"><h2 className="panel-title">Recibos</h2></div>
              <div className="panel-body flush">
                {data.receipts.length === 0 ? (
                  <p className="muted" style={{ padding: '1rem' }}>Nenhum recibo ainda.</p>
                ) : (
                  <table className="table">
                    <thead><tr><th>Número</th><th className="num">Valor</th><th className="num">Data</th></tr></thead>
                    <tbody>
                      {data.receipts.map((r) => (
                        <tr key={r.id}>
                          <td className="mono">{r.number}</td>
                          <td className="num">{brl(r.amount)}</td>
                          <td className="num muted">{new Date(r.issuedAt).toLocaleDateString('pt-BR')}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>

            <div className="panel">
              <div className="panel-header"><h2 className="panel-title">Histórico de doações</h2></div>
              <div className="panel-body flush">
                {data.donations.length === 0 ? (
                  <p className="muted" style={{ padding: '1rem' }}>Sem doações.</p>
                ) : (
                  <table className="table">
                    <thead><tr><th>Data</th><th>Status</th><th className="num">Valor</th></tr></thead>
                    <tbody>
                      {data.donations.map((d) => (
                        <tr key={d.id}>
                          <td className="muted">{new Date(d.createdAt).toLocaleDateString('pt-BR')}</td>
                          <td><span className={`badge ${d.status === 'paid' ? 'ok' : 'warn'}`}>{d.status}</span></td>
                          <td className="num">{brl(d.amount)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
