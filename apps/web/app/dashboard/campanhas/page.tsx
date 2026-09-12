'use client';

import { useCallback, useEffect, useState } from 'react';
import { ListCard, PageHeader, StatusBadge } from '../../components/Fiori';
import { OrganizationPicker } from '../../components/OrganizationPicker';
import { Panel } from '../../components/Panel';
import {
  campaignReport, createCampaign, listCampaigns, listFunds, setCampaignStatus,
  type CampaignProgress, type CampaignReport, type Fund, type LoginResult,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function CampanhasPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [items, setItems] = useState<CampaignProgress[]>([]);
  const [funds, setFunds] = useState<Fund[]>([]);
  const [report, setReport] = useState<CampaignReport | null>(null);

  const [organizationId, setOrganizationId] = useState('');
  const [title, setTitle] = useState('');
  const [goal, setGoal] = useState('');
  const [description, setDescription] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [fundId, setFundId] = useState('');

  const refresh = useCallback(async (t: string) => {
    try {
      const [cs, fs] = await Promise.all([listCampaigns(t), listFunds(t)]);
      setItems(cs);
      setFunds(fs);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao carregar.'); }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) { const t = (JSON.parse(raw) as LoginResult).accessToken; setToken(t); void refresh(t); }
  }, [refresh]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!organizationId) return setError('Selecione a unidade.');
    if (!title.trim()) return setError('Informe o título.');
    setError(null);
    try {
      await createCampaign(token, {
        organizationId,
        title: title.trim(),
        goalAmount: goal ? Number(goal) : undefined,
        description: description || undefined,
        endsAt: endsAt ? new Date(endsAt + 'T23:59:59').toISOString() : undefined,
        fundId: fundId || undefined,
      });
      setTitle(''); setGoal(''); setDescription(''); setEndsAt(''); setFundId('');
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao criar campanha.'); }
  }

  async function toggle(c: CampaignProgress) {
    if (!token) return;
    setError(null);
    try { await setCampaignStatus(token, c.id, c.status === 'active' ? 'closed' : 'active'); await refresh(token); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro.'); }
  }

  async function showReport(id: string) {
    if (!token) return;
    try { setReport(await campaignReport(token, id)); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro.'); }
  }

  const restrictedFunds = funds.filter((f) => f.restriction === 'restricted');

  return (
    <>
      <PageHeader
        title="Campanhas"
        subtitle="Arrecadação por finalidade (earmark): meta × arrecadado, janela e vínculo a um fundo restrito para a prestação de contas."
      />

      {error && <p className="error-text">{error}</p>}

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        <Panel title="Nova campanha">
          <form onSubmit={onSubmit}>
            <div className="field">
              <OrganizationPicker token={token} value={organizationId} onChange={setOrganizationId} />
            </div>
            <div className="field">
              <label htmlFor="c-title">Título</label>
              <input id="c-title" value={title} onChange={(e) => setTitle(e.target.value)} required placeholder="Ex.: Reforma do telhado" />
            </div>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
              <div className="field" style={{ flex: 1 }}>
                <label htmlFor="c-goal">Meta (R$, opcional)</label>
                <input id="c-goal" type="number" step="0.01" min="0" value={goal} onChange={(e) => setGoal(e.target.value)} />
              </div>
              <div className="field" style={{ width: 170 }}>
                <label htmlFor="c-ends">Encerra em (opcional)</label>
                <input id="c-ends" type="date" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
              </div>
            </div>
            <div className="field">
              <label htmlFor="c-fund">Fundo restrito (earmark, opcional)</label>
              <select id="c-fund" value={fundId} onChange={(e) => setFundId(e.target.value)}>
                <option value="">— sem vínculo</option>
                {restrictedFunds.map((f) => <option key={f.id} value={f.id}>{f.name}{f.purpose ? ` · ${f.purpose}` : ''}</option>)}
              </select>
            </div>
            <div className="field">
              <label htmlFor="c-desc">Descrição pública (opcional)</label>
              <textarea id="c-desc" rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
            </div>
            <button className="btn btn-primary" type="submit">Criar campanha</button>
          </form>
        </Panel>

        <Panel title={report ? `Prestação de contas — ${report.title}` : 'Prestação de contas'}>
          {!report ? (
            <p className="muted" style={{ marginTop: 0 }}>Selecione &quot;Prestação&quot; numa campanha para ver arrecadado × aplicado.</p>
          ) : (
            <table className="table">
              <tbody>
                <tr><td>Meta</td><td className="num">{report.goalAmount != null ? brl(report.goalAmount) : '—'}</td></tr>
                <tr><td>Arrecadado</td><td className="num"><strong>{brl(report.raised)}</strong></td></tr>
                <tr><td>Aplicado (fundo/projeto)</td><td className="num">{brl(report.applied)}</td></tr>
                <tr><td>Saldo</td><td className="num">{brl(report.balance)}</td></tr>
              </tbody>
            </table>
          )}
        </Panel>
      </div>

      <div className="rise rise-3" style={{ marginTop: '1rem' }}>
        <ListCard label="Campanhas" count={items.length}>
          {items.length === 0 ? (
            <p className="muted" style={{ padding: '1.25rem' }}>Nenhuma campanha ainda. Crie a primeira ao lado.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Campanha</th><th>Progresso</th><th>Situação</th><th /></tr></thead>
              <tbody>
                {items.map((c) => (
                  <tr key={c.id}>
                    <td>
                      <strong>{c.title}</strong>
                      <br /><span className="muted mono" style={{ fontSize: '0.75rem' }}>/campanha/&lt;tenant&gt;/{c.slug}</span>
                    </td>
                    <td style={{ minWidth: 220 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.8rem' }}>
                        <span>{brl(c.raised)}{c.goalAmount != null && <span className="muted"> / {brl(c.goalAmount)}</span>}</span>
                        {c.goalAmount != null && <span className="muted">{c.percent}%</span>}
                      </div>
                      {c.goalAmount != null && (
                        <div style={{ height: 8, background: 'var(--border)', borderRadius: 4, overflow: 'hidden', marginTop: 3 }}>
                          <div style={{ width: `${Math.min(100, c.percent)}%`, height: '100%', background: 'var(--sap-blue, #0a6ed1)' }} />
                        </div>
                      )}
                    </td>
                    <td>
                      <span className={`badge ${c.active ? 'ok' : 'muted'}`}>{c.active ? 'ativa' : c.status === 'closed' ? 'encerrada' : 'fora da janela'}</span>
                    </td>
                    <td className="num" style={{ whiteSpace: 'nowrap' }}>
                      <button className="btn btn-ghost btn-sm" onClick={() => showReport(c.id)}>Prestação</button>{' '}
                      <button className="btn btn-ghost btn-sm" onClick={() => toggle(c)}>{c.status === 'active' ? 'Encerrar' : 'Reabrir'}</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ListCard>
      </div>
    </>
  );
}
