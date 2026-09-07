'use client';

import { useEffect, useState } from 'react';
import { AnchorBar, Fact, ObjectHeader, ObjectSection, StatusBadge } from '../../../components/Fiori';
import {
  anonymizeDonor, exportDonor, getDonor, optOutDonor,
  type DonorDetail, type LoginResult,
} from '../../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

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
    getDonor(t, id).then(setData).catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, [id]);

  async function reload() { if (token) setData(await getDonor(token, id)); }

  async function onExport() {
    if (!token) return;
    const json = await exportDonor(token, id);
    const blob = new Blob([JSON.stringify(json, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = `doador-${id}.json`; a.click();
    URL.revokeObjectURL(url);
    setNotice('Dados exportados.');
  }
  async function onAnonymize() {
    if (!token || !confirm('Anonimizar os dados pessoais deste doador? Esta ação é irreversível.')) return;
    await anonymizeDonor(token, id); setNotice('Doador anonimizado.'); await reload();
  }
  async function onOptOut() {
    if (!token) return;
    await optOutDonor(token, id); setNotice('Doador marcado como opt-out.'); await reload();
  }

  if (error) return <div className="obj-header"><p className="error-text">{error}</p></div>;
  if (!data) return <div className="obj-header"><p className="muted">Carregando…</p></div>;

  const paid = data.donations.filter((d) => d.status === 'paid');
  const total = paid.reduce((s, d) => s + d.amount, 0);
  const activeRecurring = data.recurring.filter((r) => r.status === 'active').length;

  return (
    <>
      <ObjectHeader
        backHref="/dashboard/doadores"
        backLabel="Doadores"
        title={data.donor.name}
        subtitle={`${data.donor.email ?? 'sem e-mail'} · ${data.donor.document ?? 'sem documento'}`}
        actions={
          <>
            <button className="btn btn-ghost btn-sm" onClick={onExport}>Exportar dados</button>
            <button className="btn btn-ghost btn-sm" onClick={onOptOut}>Opt-out de contato</button>
            <button className="btn btn-ghost btn-sm" onClick={onAnonymize}>Anonimizar</button>
          </>
        }
        facts={
          <>
            <Fact label="Total doado">{brl(total)}</Fact>
            <Fact label="Doações pagas">{paid.length}</Fact>
            <Fact label="Recorrências ativas">{activeRecurring}</Fact>
            <Fact label="Telefone">{data.donor.phone ?? '—'}</Fact>
          </>
        }
      />

      {notice && <p className="badge ok" style={{ marginBottom: '1rem' }}>{notice}</p>}

      <AnchorBar
        items={[
          { id: 'perfil', label: 'Perfil' },
          { id: 'recorrencias', label: 'Recorrências' },
          { id: 'historico', label: 'Histórico de doações' },
          { id: 'mensagens', label: 'Relacionamento' },
        ]}
      />

      <ObjectSection id="perfil" title="Perfil" sub="Dados cadastrais do doador." delay={3}>
        <div className="panel">
          <div className="panel-body">
            <dl className="kv">
              <dt>Nome</dt><dd>{data.donor.name}</dd>
              <dt>E-mail</dt><dd className="mono">{data.donor.email ?? '—'}</dd>
              <dt>Documento</dt><dd className="mono">{data.donor.document ?? '—'}</dd>
              <dt>Telefone</dt><dd className="mono">{data.donor.phone ?? '—'}</dd>
            </dl>
          </div>
        </div>
      </ObjectSection>

      <ObjectSection id="recorrencias" title="Recorrências" sub="Dízimos e apoios mensais deste doador." delay={4}>
        <div className="panel">
          {data.recurring.length === 0 ? (
            <p className="muted" style={{ padding: '1.25rem' }}>Nenhuma recorrência ativa.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Valor / dia</th><th>Status</th><th className="num">Próxima cobrança</th></tr></thead>
              <tbody>
                {data.recurring.map((r) => (
                  <tr key={r.id}>
                    <td>{brl(r.amount)} <span className="muted">/ dia {r.dayOfMonth}</span></td>
                    <td><StatusBadge status={r.status} /></td>
                    <td className="num muted">{new Date(r.nextChargeAt).toLocaleDateString('pt-BR')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </ObjectSection>

      <ObjectSection id="historico" title="Histórico de doações" sub="Todas as doações registradas para este doador." delay={4}>
        <div className="panel">
          {data.donations.length === 0 ? (
            <p className="muted" style={{ padding: '1.25rem' }}>Sem doações registradas.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Data</th><th>Método</th><th>Status</th><th className="num">Valor</th></tr></thead>
              <tbody>
                {data.donations.map((d) => (
                  <tr key={d.id}>
                    <td className="muted">{new Date(d.createdAt).toLocaleDateString('pt-BR')}</td>
                    <td>{d.method}</td>
                    <td><StatusBadge status={d.status} /></td>
                    <td className="num">{brl(d.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </ObjectSection>

      <ObjectSection id="mensagens" title="Relacionamento" sub="Mensagens da régua (agradecimento, cobrança, reativação)." delay={4}>
        <div className="panel">
          {data.messages.length === 0 ? (
            <p className="muted" style={{ padding: '1.25rem' }}>Nenhuma mensagem enviada ainda.</p>
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
                    <td><StatusBadge status={m.status} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </ObjectSection>
    </>
  );
}
