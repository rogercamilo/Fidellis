'use client';

import { useEffect, useState } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Panel } from '../../components/Panel';
import {
  reportingByUnit,
  reportingOverview,
  reportingTimeseries,
  type LoginResult,
  type MonthPoint,
  type ReportingOverview,
  type UnitReport,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const brlShort = (n: number) => n.toLocaleString('pt-BR', { notation: 'compact', maximumFractionDigits: 1 });
const monthLabel = (m: string) => {
  const [y, mo] = m.split('-');
  return `${mo}/${y.slice(2)}`;
};

const PIE_COLORS = ['#d8a531', '#2f6fb0', '#2f9e6b', '#c98a1e', '#8a5cd8', '#d0483c'];

export default function RelatoriosPage() {
  const [overview, setOverview] = useState<ReportingOverview | null>(null);
  const [series, setSeries] = useState<MonthPoint[]>([]);
  const [units, setUnits] = useState<UnitReport[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const token = (JSON.parse(raw) as LoginResult).accessToken;
    reportingOverview(token).then(setOverview).catch((e) => setError(e.message));
    reportingTimeseries(token, 12).then(setSeries).catch(() => {});
    reportingByUnit(token).then(setUnits).catch(() => {});
  }, []);

  const seriesData = series.map((p) => ({ ...p, label: monthLabel(p.month) }));
  const methodData = overview?.byMethod.map((m) => ({ name: m.method.toUpperCase(), value: m.total })) ?? [];

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Relatórios</h1>
          <p className="subtitle">Dashboards e consolidação da rede (Rede→Unidade).</p>
        </div>
      </div>

      {error && <p className="error-text">{error}</p>}

      <div className="kpi-grid rise rise-2">
        <div className="kpi">
          <div className="kpi-label">Arrecadado</div>
          <div className="kpi-value"><span className="cur">R$</span>{(overview?.totalRaised ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Doações</div>
          <div className="kpi-value">{overview?.donationsCount ?? 0}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Ticket médio</div>
          <div className="kpi-value"><span className="cur">R$</span>{(overview?.avgTicket ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Doadores ativos</div>
          <div className="kpi-value">{overview?.activeDonors ?? 0}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Recorrências ativas</div>
          <div className="kpi-value">{overview?.activeRecurring ?? 0}</div>
        </div>
      </div>

      <div className="rise rise-3">
        <Panel title="Arrecadação mensal (12 meses)">
          <div style={{ width: '100%', height: 280 }}>
            <ResponsiveContainer>
              <BarChart data={seriesData} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e6e9ed" vertical={false} />
                <XAxis dataKey="label" tick={{ fontSize: 12, fill: '#64717e' }} axisLine={{ stroke: '#dde2e8' }} tickLine={false} />
                <YAxis tickFormatter={(v) => brlShort(Number(v))} tick={{ fontSize: 12, fill: '#64717e' }} axisLine={false} tickLine={false} width={54} />
                <Tooltip
                  formatter={(v: number | string) => [brl(Number(v)), 'Arrecadado']}
                  labelStyle={{ color: '#16212c' }}
                  contentStyle={{ borderRadius: 8, border: '1px solid #dde2e8', fontFamily: 'var(--font-sans)' }}
                />
                <Bar dataKey="total" fill="#d8a531" radius={[4, 4, 0, 0]} maxBarSize={48} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Panel>
      </div>

      <div className="grid cols-2 rise rise-4" style={{ marginTop: '1rem', alignItems: 'start' }}>
        <Panel title="Por método">
          {methodData.length === 0 ? (
            <p className="muted">Sem doações no período.</p>
          ) : (
            <div style={{ width: '100%', height: 240 }}>
              <ResponsiveContainer>
                <PieChart>
                  <Pie data={methodData} dataKey="value" nameKey="name" innerRadius={55} outerRadius={90} paddingAngle={2}>
                    {methodData.map((_, i) => <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />)}
                  </Pie>
                  <Tooltip formatter={(v: number | string) => brl(Number(v))} contentStyle={{ borderRadius: 8, border: '1px solid #dde2e8' }} />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}
        </Panel>

        <Panel title="Consolidação por unidade" flush>
          {units.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhuma unidade visível.</p>
          ) : (
            <table className="table">
              <thead>
                <tr><th>Unidade</th><th className="num">Doações</th><th className="num">Total</th></tr>
              </thead>
              <tbody>
                {units.map((u) => (
                  <tr key={u.organizationId}>
                    <td>{u.parentId && <span className="muted">↳ </span>}{u.name}</td>
                    <td className="num">{u.count}</td>
                    <td className="num">{brl(u.total)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td>Consolidado</td>
                  <td className="num">{units.reduce((s, u) => s + u.count, 0)}</td>
                  <td className="num">{brl(units.reduce((s, u) => s + u.total, 0))}</td>
                </tr>
              </tfoot>
            </table>
          )}
        </Panel>
      </div>
    </>
  );
}
