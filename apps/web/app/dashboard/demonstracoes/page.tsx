'use client';

import { useCallback, useEffect, useState } from 'react';
import { PageHeader } from '../../components/Fiori';
import { Panel } from '../../components/Panel';
import {
  approveSnapshot, exportLedgerCsv, exportTrialBalanceCsv, generateSnapshot, listMyOrganizations,
  listSnapshots, reportBalanceSheet, reportCashFlow, reportDmpl, reportIncome, reportSegregated,
  type LoginResult, type Organization, type ReportBalanceSheet, type ReportCashFlow, type ReportDmpl,
  type ReportIncome, type ReportSegregated, type SnapshotSummary,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const currentYear = new Date().getFullYear();

/** Papéis que assinam a prestação de contas (governança). Espelha o backend (StatementSnapshotEndpoints). */
const SIGNER_ROLES = ['admin', 'fiscal_council'];

export default function DemonstracoesPage() {
  const [token, setToken] = useState<string | null>(null);
  const [role, setRole] = useState<string | null>(null);
  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [year, setYear] = useState(currentYear);
  const [org, setOrg] = useState(''); // '' = consolidado da rede

  const [income, setIncome] = useState<ReportIncome | null>(null);
  const [balance, setBalance] = useState<ReportBalanceSheet | null>(null);
  const [seg, setSeg] = useState<ReportSegregated | null>(null);
  const [dmpl, setDmpl] = useState<ReportDmpl | null>(null);
  const [cashflow, setCashflow] = useState<ReportCashFlow | null>(null);
  const [snapshots, setSnapshots] = useState<SnapshotSummary[]>([]);

  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async (t: string, y: number, o: string) => {
    setError(null);
    try {
      const orgArg = o || undefined;
      const [inc, bal, sg, dm, cf, snaps] = await Promise.all([
        reportIncome(t, y, orgArg),
        reportBalanceSheet(t, y, orgArg),
        reportSegregated(t, y, orgArg),
        reportDmpl(t, y, orgArg),
        reportCashFlow(t, y, orgArg),
        listSnapshots(t, y),
      ]);
      setIncome(inc); setBalance(bal); setSeg(sg); setDmpl(dm); setCashflow(cf); setSnapshots(snaps);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao carregar demonstrações.');
    }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const session = JSON.parse(raw) as LoginResult;
    setToken(session.accessToken);
    setRole(session.tenants.find((m) => m.slug === session.activeTenant)?.role ?? null);
    listMyOrganizations(session.accessToken).then(setOrgs).catch(() => setOrgs([]));
  }, []);

  useEffect(() => {
    if (token) void load(token, year, org);
  }, [token, year, org, load]);

  async function onGenerate() {
    if (!token) return;
    setBusy(true);
    try {
      await generateSnapshot(token, year);
      setSnapshots(await listSnapshots(token, year));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao gerar snapshot.');
    } finally {
      setBusy(false);
    }
  }

  async function onApprove(id: string) {
    if (!token) return;
    setBusy(true);
    try {
      await approveSnapshot(token, id);
      setSnapshots(await listSnapshots(token, year));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao aprovar snapshot.');
    } finally {
      setBusy(false);
    }
  }

  const canSign = role !== null && SIGNER_ROLES.includes(role);
  const years = Array.from({ length: 6 }, (_, i) => currentYear - i);

  return (
    <>
      <PageHeader
        title="Demonstrações"
        subtitle="Resultado (DRE), Balanço, Fluxo de Caixa e Mutações do PL por exercício e unidade — no padrão ITG 2002."
        actions={
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end' }}>
            <div className="field" style={{ margin: 0 }}>
              <label htmlFor="dm-year">Exercício</label>
              <select id="dm-year" value={year} onChange={(e) => setYear(Number(e.target.value))}>
                {years.map((y) => <option key={y} value={y}>{y}</option>)}
              </select>
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label htmlFor="dm-org">Unidade</label>
              <select id="dm-org" value={org} onChange={(e) => setOrg(e.target.value)}>
                <option value="">Consolidado (rede)</option>
                {orgs.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
              </select>
            </div>
          </div>
        }
      />

      {error && <p className="error-text">{error}</p>}

      <div className="kpi-grid rise rise-2">
        <div className="kpi">
          <div className="kpi-label">Receitas</div>
          <div className="kpi-value" style={{ color: '#2f9e6b' }}><span className="cur">R$</span>{(income?.revenues ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Despesas</div>
          <div className="kpi-value" style={{ color: '#d0483c' }}><span className="cur">R$</span>{(income?.expenses ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">{(income?.surplus ?? 0) >= 0 ? 'Superávit' : 'Déficit'}</div>
          <div className="kpi-value"><span className="cur">R$</span>{Math.abs(income?.surplus ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Caixa final (DFC)</div>
          <div className="kpi-value"><span className="cur">R$</span>{(cashflow?.closingCash ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div>
        </div>
      </div>

      <div className="grid cols-2 rise rise-3" style={{ marginTop: '1rem', alignItems: 'start' }}>
        <Panel title="Demonstração do Resultado (DRE/DRP)" flush>
          {!income ? <p className="muted" style={{ padding: '1rem' }}>Carregando…</p> : (
            <table className="table">
              <thead><tr><th>Conta</th><th className="num">Saldo</th></tr></thead>
              <tbody>
                {income.revenueLines.map((l) => (
                  <tr key={l.code}><td>{l.name}</td><td className="num" style={{ color: '#2f9e6b' }}>{brl(l.balance)}</td></tr>
                ))}
                {income.expenseLines.map((l) => (
                  <tr key={l.code}><td>{l.name}</td><td className="num" style={{ color: '#d0483c' }}>−{brl(l.balance)}</td></tr>
                ))}
                <tr><td><strong>{income.surplus >= 0 ? 'Superávit' : 'Déficit'} do período</strong></td><td className="num"><strong>{brl(income.surplus)}</strong></td></tr>
              </tbody>
            </table>
          )}
        </Panel>

        <Panel title="Balanço Patrimonial" flush actions={balance && <span className={`badge ${balance.balanced ? 'ok' : 'err'}`}>{balance.balanced ? 'Ativo = Passivo + PL' : 'Desbalanceado'}</span>}>
          {!balance ? <p className="muted" style={{ padding: '1rem' }}>Carregando…</p> : (
            <table className="table">
              <tbody>
                <tr><td><strong>Ativo</strong></td><td className="num"><strong>{brl(balance.assets)}</strong></td></tr>
                {balance.assetLines.map((l) => <tr key={l.code}><td style={{ paddingLeft: '1.5rem' }} className="muted">{l.name}</td><td className="num">{brl(l.balance)}</td></tr>)}
                <tr><td><strong>Passivo</strong></td><td className="num"><strong>{brl(balance.liabilities)}</strong></td></tr>
                {balance.liabilityLines.map((l) => <tr key={l.code}><td style={{ paddingLeft: '1.5rem' }} className="muted">{l.name}</td><td className="num">{brl(l.balance)}</td></tr>)}
                <tr><td><strong>Patrimônio Líquido</strong></td><td className="num"><strong>{brl(balance.equityAccounts + balance.surplus)}</strong></td></tr>
                <tr><td style={{ paddingLeft: '1.5rem' }} className="muted">Superávit/déficit do período</td><td className="num">{brl(balance.surplus)}</td></tr>
              </tbody>
            </table>
          )}
        </Panel>
      </div>

      <div className="grid cols-2 rise rise-4" style={{ marginTop: '1rem', alignItems: 'start' }}>
        <Panel title="Fluxo de Caixa (DFC — método direto)" flush>
          {!cashflow ? <p className="muted" style={{ padding: '1rem' }}>Carregando…</p> : (
            <table className="table">
              <tbody>
                <tr><td>Caixa inicial</td><td className="num">{brl(cashflow.openingCash)}</td></tr>
                <tr><td>Entradas</td><td className="num" style={{ color: '#2f9e6b' }}>+{brl(cashflow.inflows)}</td></tr>
                <tr><td>Saídas</td><td className="num" style={{ color: '#d0483c' }}>−{brl(cashflow.outflows)}</td></tr>
                <tr><td>Geração de caixa</td><td className="num">{brl(cashflow.netCash)}</td></tr>
                <tr><td><strong>Caixa final</strong></td><td className="num"><strong>{brl(cashflow.closingCash)}</strong></td></tr>
              </tbody>
            </table>
          )}
        </Panel>

        <Panel title="Mutações do PL (DMPL) e segregação" flush>
          {!dmpl || !seg ? <p className="muted" style={{ padding: '1rem' }}>Carregando…</p> : (
            <table className="table">
              <tbody>
                <tr><td>PL inicial</td><td className="num">{brl(dmpl.openingEquity)}</td></tr>
                <tr><td>+ Superávit livre</td><td className="num">{brl(dmpl.freeSurplus)}</td></tr>
                <tr><td>+ Superávit restrito</td><td className="num">{brl(dmpl.restrictedSurplus)}</td></tr>
                <tr><td><strong>PL final</strong></td><td className="num"><strong>{brl(dmpl.closingEquity)}</strong></td></tr>
                <tr><td className="muted" style={{ paddingTop: '0.75rem' }}>Recursos livres (resultado)</td><td className="num" style={{ paddingTop: '0.75rem' }}>{brl(seg.free.surplus)}</td></tr>
                <tr><td className="muted">Recursos com restrição (resultado)</td><td className="num">{brl(seg.restricted.surplus)}</td></tr>
              </tbody>
            </table>
          )}
        </Panel>
      </div>

      <div className="rise rise-5" style={{ marginTop: '1rem' }}>
        <Panel
          title="Prestação de contas (snapshots)"
          actions={
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              <button className="linkbtn" onClick={() => token && exportLedgerCsv(token, year)}>Exportar razão (CSV)</button>
              <button className="linkbtn" onClick={() => token && exportTrialBalanceCsv(token, year)}>Exportar balancete (CSV)</button>
              <button className="btn btn-ghost" onClick={onGenerate} disabled={busy}>Gerar snapshot {year}</button>
            </div>
          }
          flush
        >
          {snapshots.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum snapshot deste exercício. Gere um para congelar as demonstrações.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Gerado em</th><th>Status</th><th>Hash</th><th>Aprovado por</th><th /></tr></thead>
              <tbody>
                {snapshots.map((s) => (
                  <tr key={s.id}>
                    <td>{new Date(s.createdAt).toLocaleString('pt-BR')}</td>
                    <td><span className={`badge ${s.status === 'approved' ? 'ok' : 'warn'}`}>{s.status === 'approved' ? 'assinado' : 'rascunho'}</span></td>
                    <td className="muted" style={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{s.hash.slice(0, 12)}…</td>
                    <td className="muted">{s.approvedBy ?? '—'}</td>
                    <td className="num">
                      {s.status !== 'approved' && canSign && (
                        <button className="linkbtn" onClick={() => onApprove(s.id)} disabled={busy}>Aprovar / assinar</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {!canSign && snapshots.some((s) => s.status !== 'approved') && (
            <p className="muted" style={{ padding: '0 1rem 1rem' }}>Somente admin ou conselho fiscal assina a prestação de contas.</p>
          )}
        </Panel>
      </div>
    </>
  );
}
