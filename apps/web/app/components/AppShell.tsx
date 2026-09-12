'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useState, type ReactNode } from 'react';
import { bootstrapStatus, type BootstrapStatus, type LoginResult } from '../lib/api';

// Conjuntos de papéis (D-03). Menu filtrado pelo papel; a trava de escrita real fica no back.
const OPERATORS = ['admin', 'coordinator'];                                              // lançam
const APPROVERS = ['admin', 'coordinator', 'council_officer', 'council_chair'];           // aprovam
const OVERSIGHT = [...APPROVERS, 'fiscal_council'];                                        // + fiscaliza
const ACCOUNTING = [...OVERSIGHT, 'accountant'];                                           // + contador

/**
 * Navegação do ERP reorganizada em Entradas/Saídas (D-04) e filtrada por papel (D-03). `roles` ausente =
 * visível a todos; senão o grupo só aparece para os papéis listados (papel nulo/dev vê tudo).
 */
const NAV: { label: string; roles?: string[]; items: { href: string; label: string }[] }[] = [
  { label: '', items: [{ href: '/dashboard', label: 'Início' }] },
  {
    label: 'Entradas',
    roles: OVERSIGHT,
    items: [
      { href: '/dashboard/cobranca', label: 'Cobrança' },
      { href: '/dashboard/recorrencia', label: 'Recorrência' },
      { href: '/dashboard/campanhas', label: 'Campanhas' },
      { href: '/dashboard/caixa', label: 'Caixa' },
      { href: '/dashboard/receber', label: 'Contas a receber' },
      { href: '/dashboard/doadores', label: 'Doadores' },
    ],
  },
  {
    label: 'Saídas',
    roles: OVERSIGHT,
    items: [
      { href: '/dashboard/pagar', label: 'Contas a pagar' },
      { href: '/dashboard/tesouraria', label: 'Tesouraria' },
    ],
  },
  {
    label: 'Contabilidade',
    roles: ACCOUNTING,
    items: [
      { href: '/dashboard/contabilidade', label: 'Balancete & razão' },
      { href: '/dashboard/demonstracoes', label: 'Demonstrações' },
      { href: '/dashboard/conciliacao', label: 'Conciliação' },
      { href: '/dashboard/fechamento', label: 'Fechamento' },
    ],
  },
  {
    label: 'Prestação de contas',
    roles: ACCOUNTING,
    items: [
      { href: '/dashboard/orcamento', label: 'Orçamento' },
      { href: '/dashboard/projetos', label: 'Projetos & voluntariado' },
      { href: '/dashboard/relatorios', label: 'Relatórios' },
      { href: '/dashboard/auditoria', label: 'Auditoria' },
    ],
  },
  {
    label: 'Organização',
    roles: OPERATORS,
    items: [
      { href: '/dashboard/equipe', label: 'Equipe & conselho' },
      { href: '/dashboard/configuracoes', label: 'Configurações' },
    ],
  },
];

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [session, setSession] = useState<LoginResult | null>(null);
  const [open, setOpen] = useState(false);
  const [bootstrap, setBootstrap] = useState<BootstrapStatus | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const s = JSON.parse(raw) as LoginResult;
    setSession(s);
    if (s.accessToken) bootstrapStatus(s.accessToken).then(setBootstrap).catch(() => undefined);
  }, [pathname]);

  useEffect(() => setOpen(false), [pathname]);

  function logout() {
    sessionStorage.removeItem('fidellis.session');
    router.push('/login');
  }

  const isActive = (href: string) =>
    href === '/dashboard' ? pathname === href : pathname.startsWith(href);

  // Título do app atual no shell bar (detalhe SAP Fiori): melhor correspondência da rota (menu completo).
  const allItems = NAV.flatMap((g) => g.items);
  const current =
    allItems
      .filter((it) => (it.href === '/dashboard' ? pathname === it.href : pathname.startsWith(it.href)))
      .sort((a, b) => b.href.length - a.href.length)[0]?.label ?? 'Início';

  // Progressive disclosure por papel (D-03): sem papel (dev) vê tudo; senão só os grupos do papel.
  const role = session?.tenants.find((t) => t.slug === session.activeTenant)?.role;
  const nav = NAV.filter((g) => !g.roles || !role || g.roles.includes(role));

  return (
    <div className="app">
      <header className="shellbar">
        <button className="icon-btn menu-btn" aria-label="Menu" onClick={() => setOpen((v) => !v)}>☰</button>
        <Link href="/dashboard" className="shellbar-brand" style={{ textDecoration: 'none' }}>
          <span className="mark">F</span> Fidellis
        </Link>
        <span className="shell-sep" aria-hidden>›</span>
        <span className="shell-title">{current} <span aria-hidden>⌄</span></span>
        <div className="shellbar-search" aria-hidden>
          <span>⌕</span> Buscar aplicativos, contas, doadores…
        </div>
        <div className="shellbar-right">
          {session?.activeTenant && (
            <span className="tenant-chip"><span className="dot ok" /> {session.activeTenant}</span>
          )}
          <span className="who">{session?.user.displayName ?? session?.user.email ?? '—'}</span>
          <button className="icon-btn" aria-label="Sair" title="Sair" onClick={logout}>⎋</button>
        </div>
      </header>

      <div className="shell-body">
        <div className="sidenav-backdrop" data-open={open} onClick={() => setOpen(false)} />
        <aside className="sidenav-fiori" data-open={open}>
          {nav.map((group, gi) => (
            <div className="nav-group" key={group.label || `g${gi}`}>
              {group.label && <div className="nav-group-label">{group.label}</div>}
              {group.items.map((item) => (
                <Link key={item.href} href={item.href} className={`side-link${isActive(item.href) ? ' active' : ''}`}>
                  <span className="side-ico" aria-hidden />
                  {item.label}
                </Link>
              ))}
            </div>
          ))}
        </aside>

        <main className="content">
          {bootstrap?.inBootstrap && !pathname.startsWith('/dashboard/equipe') && (
            <div
              role="status"
              style={{
                display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap',
                padding: '0.6rem 0.9rem', marginBottom: '1rem', borderRadius: 8,
                background: '#fef7e6', border: '1px solid #e9b949', color: '#6b4e00',
              }}
            >
              <span aria-hidden>⚠️</span>
              <span style={{ flex: 1, minWidth: 200 }}>
                Sua equipe ainda não está completa. Convide ao menos dois aprovadores para liberar a aprovação de pagamentos.
              </span>
              <Link href="/dashboard/equipe" className="btn btn-primary btn-sm">Montar equipe</Link>
            </div>
          )}
          {children}
        </main>
      </div>
    </div>
  );
}
