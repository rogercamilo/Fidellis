'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { GuidedTour } from '../components/Fiori';
import {
  listMyOrganizations, listReceipts, reportingOverview,
  type LoginResult, type Organization, type ReceiptSummary, type ReportingOverview,
} from '../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Aplicativos por módulo — tiles do launchpad. Ícone é um glifo simples (placeholder do icon set). */
const APPS: { label: string; items: { href: string; title: string; sub: string; ico: string }[] }[] = [
  {
    label: 'Financeiro',
    items: [
      { href: '/dashboard/cobranca', title: 'Cobrança', sub: 'Gerar cobranças e doações', ico: '↗' },
      { href: '/dashboard/receber', title: 'Contas a receber', sub: 'Promessas e recebíveis', ico: '⇊' },
      { href: '/dashboard/pagar', title: 'Contas a pagar', sub: 'Títulos e aprovações', ico: '⇈' },
      { href: '/dashboard/tesouraria', title: 'Tesouraria', sub: 'Contas, saldos e fluxo', ico: '≋' },
      { href: '/dashboard/conciliacao', title: 'Conciliação', sub: 'Extratos bancários', ico: '⇄' },
      { href: '/dashboard/orcamento', title: 'Orçamento', sub: 'Previsto × realizado', ico: '▤' },
    ],
  },
  {
    label: 'Contabilidade',
    items: [
      { href: '/dashboard/contabilidade', title: 'Balancete & razão', sub: 'Contas e lançamentos', ico: '≡' },
      { href: '/dashboard/demonstracoes', title: 'Demonstrações', sub: 'DRE, Balanço, DFC, DMPL', ico: '▦' },
    ],
  },
  {
    label: 'Gestão',
    items: [
      { href: '/dashboard/doadores', title: 'Doadores', sub: 'CRM e relacionamento', ico: '☺' },
      { href: '/dashboard/relatorios', title: 'Relatórios', sub: 'Dashboards da rede', ico: '◔' },
      { href: '/dashboard/auditoria', title: 'Auditoria', sub: 'Trilha e LGPD', ico: '❖' },
      { href: '/dashboard/configuracoes', title: 'Configurações', sub: 'Organização e equipe', ico: '⚙' },
    ],
  },
];

export default function DashboardPage() {
  const [session, setSession] = useState<LoginResult | null>(null);
  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [receipts, setReceipts] = useState<ReceiptSummary[]>([]);
  const [overview, setOverview] = useState<ReportingOverview | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const s = JSON.parse(raw) as LoginResult;
    setSession(s);
    const t = s.accessToken;
    listMyOrganizations(t).then(setOrgs).catch(() => {});
    listReceipts(t).then(setReceipts).catch(() => {});
    reportingOverview(t).then(setOverview).catch(() => {});
  }, []);

  const kpis = [
    { label: 'Arrecadado', value: `R$ ${brl(overview?.totalRaised ?? 0)}`, foot: 'Total recebido na rede' },
    { label: 'Recibos emitidos', value: String(receipts.length), foot: 'No período' },
    { label: 'Recorrências ativas', value: String(overview?.activeRecurring ?? 0), foot: 'Dízimos e apoios mensais' },
    { label: 'Unidades', value: String(orgs.length), foot: 'Matriz e filiais' },
  ];

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Início</h1>
          <p className="subtitle">
            Olá, {session?.user.displayName ?? session?.user.email ?? 'bem-vindo'} — este é o painel da sua organização.
          </p>
        </div>
        <Link className="btn btn-primary" href="/dashboard/cobranca">Nova cobrança</Link>
      </div>

      <section className="lp-section rise rise-2">
        <h2 className="lp-section-title">Indicadores</h2>
        <div className="tile-grid">
          {kpis.map((k) => (
            <div className="tile tile-kpi" key={k.label}>
              <div className="tile-label">{k.label}</div>
              <div className="tile-value">{k.value}</div>
              <div className="tile-foot">{k.foot}</div>
            </div>
          ))}
        </div>
      </section>

      {APPS.map((group, gi) => (
        <section className={`lp-section rise rise-${gi + 3}`} key={group.label}>
          <h2 className="lp-section-title">{group.label}</h2>
          <div className="tile-grid">
            {group.items.map((app) => (
              <Link className="tile tile-app" href={app.href} key={app.href}>
                <div className="tile-ico" aria-hidden>{app.ico}</div>
                <div className="tile-title">{app.title}</div>
                <div className="tile-sub">{app.sub}</div>
              </Link>
            ))}
          </div>
        </section>
      ))}

      <GuidedTour
        steps={[
          { title: 'Navegue por módulos', body: 'Use o menu à esquerda para acessar Financeiro, Contabilidade, Gestão e as Configurações da sua organização.' },
          { title: 'Comece pelos indicadores', body: 'Os blocos no topo mostram os números-chave. Clique em um aplicativo para abrir a tela correspondente.' },
          { title: 'Tudo em linguagem simples', body: 'Cada tela explica o que faz logo abaixo do título. Você não precisa ser contador para operar o Fidellis.' },
        ]}
      />
    </>
  );
}
