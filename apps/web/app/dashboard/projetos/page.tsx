'use client';

import { useCallback, useEffect, useState } from 'react';
import { PageHeader } from '../../components/Fiori';
import { OrganizationPicker } from '../../components/OrganizationPicker';
import { Panel } from '../../components/Panel';
import {
  listProjects, listVolunteerWork, mroscReport, recordVolunteerWork,
  type LoginResult, type MroscReport, type Project, type VolunteerWork,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const today = () => new Date().toISOString().slice(0, 10);

export default function ProjetosPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [projects, setProjects] = useState<Project[]>([]);
  const [volunteer, setVolunteer] = useState<VolunteerWork[]>([]);

  // MROSC
  const [mroscProject, setMroscProject] = useState('');
  const [report, setReport] = useState<MroscReport | null>(null);

  // Registro de voluntariado
  const [org, setOrg] = useState('');
  const [description, setDescription] = useState('');
  const [fairValue, setFairValue] = useState('');
  const [performedOn, setPerformedOn] = useState(today());
  const [vProject, setVProject] = useState('');
  const [busy, setBusy] = useState(false);

  const refresh = useCallback(async (t: string) => {
    try {
      const [projs, vw] = await Promise.all([listProjects(t), listVolunteerWork(t)]);
      setProjects(projs);
      setVolunteer(vw);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao carregar.');
    }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const t = (JSON.parse(raw) as LoginResult).accessToken;
    setToken(t);
    void refresh(t);
  }, [refresh]);

  useEffect(() => {
    if (!token || !mroscProject) { setReport(null); return; }
    mroscReport(token, mroscProject)
      .then(setReport)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro na prestação MROSC.'));
  }, [token, mroscProject]);

  async function onRecord(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!org || !description || !fairValue) return setError('Unidade, descrição e valor são obrigatórios.');
    setError(null);
    setBusy(true);
    try {
      await recordVolunteerWork(token, {
        organizationId: org,
        description: description.trim(),
        fairValue: Number(fairValue),
        performedOn,
        projectId: vProject || undefined,
      });
      setDescription(''); setFairValue('');
      setVolunteer(await listVolunteerWork(token));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao registrar.');
    } finally {
      setBusy(false);
    }
  }

  const totalVolunteer = volunteer.reduce((s, v) => s + v.fairValue, 0);

  return (
    <>
      <PageHeader
        title="Projetos & voluntariado"
        subtitle="Prestação de contas de parcerias (convênios/editais) e registro do trabalho voluntário a valor justo."
      />

      {error && <p className="error-text">{error}</p>}

      <div className="rise rise-2">
        <Panel title="Prestação de contas MROSC (por projeto)">
          <div className="field" style={{ maxWidth: 360 }}>
            <label htmlFor="mrosc-proj">Projeto / parceria</label>
            <select id="mrosc-proj" value={mroscProject} onChange={(e) => setMroscProject(e.target.value)}>
              <option value="">Selecione um projeto…</option>
              {projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </div>

          {report && (
            <table className="table" style={{ marginTop: '0.5rem' }}>
              <tbody>
                <tr><td>Previsto (convênios/editais)</td><td className="num">{brl(report.expected)}</td></tr>
                <tr><td>Recebido</td><td className="num" style={{ color: '#2f9e6b' }}>{brl(report.received)}</td></tr>
                <tr><td>Gasto no projeto</td><td className="num" style={{ color: '#d0483c' }}>−{brl(report.spent)}</td></tr>
                <tr><td><strong>Saldo (recebido − gasto)</strong></td><td className="num"><strong>{brl(report.balance)}</strong></td></tr>
              </tbody>
            </table>
          )}
          {mroscProject && !report && <p className="muted" style={{ marginTop: '0.5rem' }}>Carregando…</p>}
          {projects.length === 0 && <p className="muted" style={{ marginTop: '0.5rem' }}>Nenhum projeto cadastrado — crie um em Financeiro › Configurações.</p>}
        </Panel>
      </div>

      <div className="grid cols-2 rise rise-3" style={{ marginTop: '1rem', alignItems: 'start' }}>
        <Panel title="Trabalho voluntário registrado" actions={<span className="badge muted">{brl(totalVolunteer)}</span>} flush>
          {volunteer.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum registro ainda. Lance o valor justo dos serviços voluntários.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Data</th><th>Descrição</th><th className="num">Valor justo</th></tr></thead>
              <tbody>
                {volunteer.map((v) => (
                  <tr key={v.id}>
                    <td>{new Date(v.performedOn).toLocaleDateString('pt-BR')}</td>
                    <td>{v.description}</td>
                    <td className="num">{brl(v.fairValue)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>

        <Panel title="Registrar trabalho voluntário">
          <form onSubmit={onRecord}>
            <div className="field"><OrganizationPicker token={token} value={org} onChange={setOrg} /></div>
            <div className="field">
              <label htmlFor="vw-desc">Descrição do serviço</label>
              <input id="vw-desc" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Ex.: 40h de assessoria jurídica pro bono" required />
            </div>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
              <div className="field" style={{ width: 160 }}>
                <label htmlFor="vw-val">Valor justo (R$)</label>
                <input id="vw-val" type="number" step="0.01" min="0.01" value={fairValue} onChange={(e) => setFairValue(e.target.value)} required />
              </div>
              <div className="field" style={{ width: 170 }}>
                <label htmlFor="vw-date">Data</label>
                <input id="vw-date" type="date" value={performedOn} onChange={(e) => setPerformedOn(e.target.value)} required />
              </div>
            </div>
            <div className="field">
              <label htmlFor="vw-proj">Projeto (opcional)</label>
              <select id="vw-proj" value={vProject} onChange={(e) => setVProject(e.target.value)}>
                <option value="">—</option>
                {projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </div>
            <button className="btn btn-primary" type="submit" disabled={busy}>Registrar</button>
          </form>
        </Panel>
      </div>
    </>
  );
}
