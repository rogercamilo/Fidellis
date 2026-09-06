'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Panel } from '../../../components/Panel';
import {
  anonymizeDonor,
  exportDonor,
  getDonor,
  optOutDonor,
  type DonorDetail,
  type LoginResult,
} from '../../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

function badgeFor(status: string): string {
  if (status === 'paid' || status === 'active' || status === 'sent') return 'ok';
  if (status === 'failed' || status === 'past_due') return 'err';
  if (status === 'canceled' || status === 'skipped' || status === 'expired') return 'muted';
  return 'warn';
}

export default function DonorDetailPage({ params }: { params: { id: string } }) {
  const { id } = params;
  const [data, setData] = useState<DonorDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const t = (JSON.parse(raw) as LoginResult).accessToken;
    setToken(t);
    getDonor(t, id)
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, [id]);

  async function reload() {
    if (token) setData(await getDonor(token, id));
  }

  async function onExport() {
    if (!token) return;
    const json = await exportDonor(token, id);
    const blob = new Blob([JSON.stringify(json, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `doador-${id}.json`;
    a.click();
    URL.revokeObjectURL(url);
    setNotice('Dados exportados.');
  }

  async function onAnonymize() {
    if (!token || !confirm('Anonimizar os dados pessoais deste doador? Esta ação é irreversível.')) return;
    await anonymizeDonor(token, id);
    setNotice('Doador anonimizado.');
    await reload();
  }

  async function onOptOut() {
    if (!token) return;
    await optOutDonor(token, id);
    setNotice('Doador marcado como opt-out.');
    await reload();
  }

  if (error) return <Panel title="Doador"><p className="error-text">{error}</p></Panel>;
  if (!data) return <Panel title="Doador"><p className="muted">Carregando…</p></Panel>;

  const total = data.donations.filter((d) => d.status === 'paid').reduce((s, d) => s + d.amount, 0);

  return (
    <>
      <div className="page-head rise">
        <div>
          <p className="muted" style={{ margin: 0 }}><Link href="/dashboard/doadores">← Doadores</Link></p>
          <h1>{data.donor.name}</h1>
          <p className="subtitle">{data.donor.email ?? 'sem e-mail'} · {data.donor.document ?? 'sem documento'}</p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <button className="btn btn-ghost btn-sm" onClick={onExport}>Exportar dados</button>
          <button className="btn btn-ghost btn-sm" onClick={onOptOut}>Opt-out</button>
          <button className="btn btn-ghost btn-sm" onClick={onAnonymize}>Anonimizar</button>
        </div>
      </div>
      {notice && <p className="badge ok" style={{ marginBottom: '1rem' }}>{notice}</p>}

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        <Panel title="Perfil">
          <dl className="kv">
            <dt>Nome</dt><dd>{data.donor.name}</dd>
            <dt>E-mail</dt><dd className="mono">{data.donor.email ?? '—'}</dd>
            <dt>Documento</dt><dd className="mono">{data.donor.document ?? '—'}</dd>
            <dt>Telefone</dt><dd className="mono">{data.donor.phone ?? '—'}</dd>
            <dt>Total doado</dt><dd>{brl(total)}</dd>
            <dt>Doações</dt><dd>{data.donations.filter((d) => d.status === 'paid').length}</dd>
          </dl>
        </Panel>

        <Panel title="Recorrências" flush>
          {data.recurring.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhuma recorrência.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Valor / dia</th><th>Status</th><th className="num">Próxima</th></tr></thead>
              <tbody>
                {data.recurring.map((r) => (
                  <tr key={r.id}>
                    <td>{brl(r.amount)} <span className="muted">/ dia {r.dayOfMonth}</span></td>
                    <td><span className={`badge ${badgeFor(r.status)}`}>{r.status}</span></td>
                    <td className="num muted">{new Date(r.nextChargeAt).toLocaleDateString('pt-BR')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>
      </div>

      <div style={{ marginTop: '1rem' }} className="rise rise-3">
        <Panel title="Histórico de doações" flush>
          {data.donations.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Sem doações.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Data</th><th>Método</th><th>Status</th><th className="num">Valor</th></tr></thead>
              <tbody>
                {data.donations.map((d) => (
                  <tr key={d.id}>
                    <td className="muted">{new Date(d.createdAt).toLocaleDateString('pt-BR')}</td>
                    <td>{d.method}</td>
                    <td><span className={`badge ${badgeFor(d.status)}`}>{d.status}</span></td>
                    <td className="num">{brl(d.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>
      </div>

      <div style={{ marginTop: '1rem' }} className="rise rise-4">
        <Panel title="Mensagens (régua)" flush>
          {data.messages.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhuma mensagem enviada ainda.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Data</th><th>Canal</th><th>Evento</th><th>Assunto</th><th>Status</th></tr></thead>
              <tbody>
                {data.messages.map((m) => (
                  <tr key={m.id}>
                    <td className="muted">{new Date(m.createdAt).toLocaleDateString('pt-BR')}</td>
                    <td>{m.channel}</td>
                    <td>{m.eventType}</td>
                    <td className="muted">{m.subject ?? '—'}</td>
                    <td><span className={`badge ${badgeFor(m.status)}`}>{m.status}</span></td>
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
