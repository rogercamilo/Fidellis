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

  // Contribuição do membro (#75): o membro escolhe tipo, valor, recorrência e forma de pagamento.
  const [orgs, setOrgs] = useState<PublicOrg[]>([]);
  const [orgId, setOrgId] = useState('');
  const [gType, setGType] = useState<EntryType>('tithe');
  const [gAmount, setGAmount] = useState('');
  const [recurrence, setRecurrence] = useState<'once' | 'monthly'>('monthly');
  const [pDay, setPDay] = useState('5');
  const [method, setMethod] = useState('pix');
  const [checkout, setCheckout] = useState<DonationCheckout | null>(null);
  const [msg, setMsg] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

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

  // Oferta é sempre pontual (taxonomia); dízimo pode ser recorrente (esperado) ou pontual — o membro decide.
  function changeType(t: EntryType) {
    setGType(t);
    setRecurrence(t === 'tithe' ? 'monthly' : 'once');
  }

  const isRecurring = gType === 'tithe' && recurrence === 'monthly';

  async function contribute(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!orgId) return setError('Selecione a unidade.');
    const amount = Number(gAmount);
    if (!(amount > 0)) return setError('Informe um valor válido.');
    setError(null); setMsg(null); setSubmitting(true);
    try {
      if (isRecurring) {
        const r = await memberPledge(tenant, token, { organizationId: orgId, amount, dayOfMonth: Number(pDay), method });
        setGAmount('');
        setMsg(`Dízimo mensal por ${r.method === 'boleto' ? 'boleto' : 'PIX'} assinado (dia ${r.dayOfMonth}). A cobrança de cada ciclo é gerada automaticamente.`);
      } else {
        const r = await memberGive(tenant, token, { organizationId: orgId, amount, entryType: gType, method });
        setCheckout(r); setGAmount('');
      }
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro.'); }
    finally { setSubmitting(false); }
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
                      {checkout.method === 'boleto' ? (
                        <>
                          <div className="field" style={{ width: '100%' }}>
                            <label>Linha digitável do boleto</label>
                            <textarea readOnly value={checkout.boletoLine ?? ''} rows={2} className="mono" style={{ width: '100%', fontSize: '0.8rem' }} />
                          </div>
                          {checkout.boletoUrl && (
                            <a className="btn btn-ghost btn-sm" href={checkout.boletoUrl} target="_blank" rel="noreferrer">Abrir PDF do boleto</a>
                          )}
                        </>
                      ) : (
                        <>
                          {checkout.qrCodeUrl && (
                            // eslint-disable-next-line @next/next/no-img-element
                            <img src={checkout.qrCodeUrl} alt="QR PIX" style={{ width: 200, height: 200, background: '#fff', borderRadius: 10, border: '1px solid var(--border)', padding: 8 }} />
                          )}
                          <textarea readOnly value={checkout.qrCode} rows={3} className="mono" style={{ width: '100%', fontSize: '0.75rem' }} />
                        </>
                      )}
                      <button className="btn btn-ghost btn-sm" onClick={() => setCheckout(null)}>Nova contribuição</button>
                    </div>
                  ) : (
                    <form onSubmit={contribute}>
                      {orgs.length > 1 && (
                        <div className="field">
                          <label htmlFor="p-org">Unidade</label>
                          <select id="p-org" value={orgId} onChange={(e) => setOrgId(e.target.value)}>
                            {orgs.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                          </select>
                        </div>
                      )}
                      <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                        <div className="field" style={{ width: 150 }}>
                          <label htmlFor="g-type">Tipo</label>
                          <select id="g-type" value={gType} onChange={(e) => changeType(e.target.value as EntryType)}>
                            <option value="tithe">Dízimo</option>
                            <option value="offering">Oferta</option>
                          </select>
                        </div>
                        <div className="field" style={{ flex: 1, minWidth: 120 }}>
                          <label htmlFor="g-amt">Valor (R$)</label>
                          <input id="g-amt" type="number" step="0.01" min="0.01" value={gAmount} onChange={(e) => setGAmount(e.target.value)} required />
                        </div>
                      </div>

                      {/* Recorrência: opcional e indicada pelo membro — só para dízimo (oferta é sempre pontual). */}
                      {gType === 'tithe' && (
                        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                          <div className="field" style={{ width: 200 }}>
                            <label htmlFor="g-rec">Frequência</label>
                            <select id="g-rec" value={recurrence} onChange={(e) => setRecurrence(e.target.value as 'once' | 'monthly')}>
                              <option value="monthly">Mensal (recorrente)</option>
                              <option value="once">Pontual (uma vez)</option>
                            </select>
                          </div>
                          {isRecurring && (
                            <div className="field" style={{ width: 110 }}>
                              <label htmlFor="g-day">Dia</label>
                              <input id="g-day" type="number" min="1" max="31" value={pDay} onChange={(e) => setPDay(e.target.value)} required />
                            </div>
                          )}
                        </div>
                      )}

                      <div className="field" style={{ maxWidth: 200 }}>
                        <label htmlFor="g-method">Forma de pagamento</label>
                        <select id="g-method" value={method} onChange={(e) => setMethod(e.target.value)}>
                          <option value="pix">PIX</option>
                          <option value="boleto">Boleto</option>
                        </select>
                      </div>

                      <button className="btn btn-primary" type="submit" disabled={submitting}>
                        {submitting ? 'Enviando…' : isRecurring ? 'Assinar dízimo mensal' : 'Contribuir'}
                      </button>
                    </form>
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
