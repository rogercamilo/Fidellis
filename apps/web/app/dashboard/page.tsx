'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Panel } from '../components/Panel';
import {
  listMyOrganizations,
  listReceipts,
  listRecurring,
  type LoginResult,
  type Organization,
  type ReceiptSummary,
  type RecurringDonation,
} from '../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function DashboardPage() {
  const [session, setSession] = useState<LoginResult | null>(null);
  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [receipts, setReceipts] = useState<ReceiptSummary[]>([]);
  const [recurring, setRecurring] = useState<RecurringDonation[]>([]);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const s = JSON.parse(raw) as LoginResult;
    setSession(s);
    const t = s.accessToken;
    listMyOrganizations(t).then(setOrgs).catch(() => {});
    listReceipts(t).then(setReceipts).catch(() => {});
    listRecurring(t).then(setRecurring).catch(() => {});
  }, []);

  if (!session) {
    return (
      <Panel title="Sessão">
        <p className="muted">
          Sessão não encontrada. <Link href="/login">Faça login</Link> para continuar.
        </p>
      </Panel>
    );
  }

  const activeRecurring = recurring.filter((r) => r.status === 'active').length;
  const total = receipts.reduce((s, r) => s + r.amount, 0);

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Painel</h1>
          <p className="subtitle">
            Olá, {session.user.displayName ?? session.user.email} · visão geral da sua rede.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '0.6rem', flexWrap: 'wrap' }}>
          <Link className="btn btn-primary" href="/dashboard/cobranca">Nova cobrança</Link>
          <Link className="btn btn-ghost" href="/dashboard/recorrencia">Recorrência</Link>
        </div>
      </div>

      <div className="kpi-grid rise rise-2">
        <div className="kpi">
          <div className="kpi-label">Arrecadado (recibos)</div>
          <div className="kpi-value"><span className="cur">R$</span>{total.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Recibos emitidos</div>
          <div className="kpi-value">{receipts.length}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Recorrências ativas</div>
          <div className="kpi-value">{activeRecurring}</div>
        </div>
        <div className="kpi">
          <div className="kpi-label">Unidades</div>
          <div className="kpi-value">{orgs.length}</div>
        </div>
      </div>

      <div className="grid cols-2 rise rise-3">
        <Panel
          title="Suas unidades"
          actions={<Link href="/dashboard/cobranca">gerenciar</Link>}
          flush
        >
          {orgs.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>
              Nenhuma unidade ainda. Crie a primeira em <Link href="/dashboard/cobranca">Cobrança</Link>.
            </p>
          ) : (
            <table className="table">
              <tbody>
                {orgs.map((o) => (
                  <tr key={o.id}>
                    <td><span className="dot ok" style={{ marginRight: 8 }} />{o.name}</td>
                    <td className="num muted" style={{ fontSize: '0.78rem' }}>
                      {o.parentId ? 'filial' : 'matriz'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>

        <Panel title="Informações da sessão">
          <dl className="kv">
            <dt>Usuário</dt>
            <dd>{session.user.displayName ?? '—'}</dd>
            <dt>E-mail</dt>
            <dd className="mono">{session.user.email}</dd>
            <dt>Tenant ativo</dt>
            <dd>{session.activeTenant ?? '—'}</dd>
            <dt>Instituições</dt>
            <dd>{session.tenants.map((t) => t.name).join(', ') || '—'}</dd>
            <dt>Assinatura</dt>
            <dd><span className="badge ok">0% de taxa</span></dd>
          </dl>
        </Panel>
      </div>

      <div style={{ marginTop: '1rem' }} className="rise rise-4">
        <Panel title="Últimos recibos" actions={<Link href="/dashboard/recibos">ver todos</Link>} flush>
          {receipts.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum recibo emitido ainda.</p>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Doador</th>
                  <th className="num">Valor</th>
                  <th className="num">Emitido</th>
                </tr>
              </thead>
              <tbody>
                {receipts.slice(0, 5).map((r) => (
                  <tr key={r.id}>
                    <td className="mono">{r.number}</td>
                    <td>{r.donorName}</td>
                    <td className="num">{brl(r.amount)}</td>
                    <td className="num muted">{new Date(r.issuedAt).toLocaleDateString('pt-BR')}</td>
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
