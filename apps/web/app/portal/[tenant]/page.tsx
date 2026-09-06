'use client';

import { useEffect, useState } from 'react';
import { portalMe, requestMagicLink, type PortalData } from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function PortalPage({ params }: { params: { tenant: string } }) {
  const { tenant } = params;
  const [token, setToken] = useState<string | null>(null);
  const [data, setData] = useState<PortalData | null>(null);
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const t = new URLSearchParams(window.location.search).get('token');
    setToken(t);
    if (t) {
      portalMe(tenant, t)
        .then(setData)
        .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
    }
  }, [tenant]);

  async function onRequest(e: React.FormEvent) {
    e.preventDefault();
    await requestMagicLink(tenant, email);
    setSent(true);
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
              <div className="panel-body"><p className="muted" style={{ margin: 0 }}>{data.donor.email}</p></div>
            </div>

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
