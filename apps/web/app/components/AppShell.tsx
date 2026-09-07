'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useState, type ReactNode } from 'react';
import type { LoginResult } from '../lib/api';

/** Navegação do ERP, agrupada por módulo. Cresce a cada novo bloco da plataforma. */
const NAV: { label: string; items: { href: string; label: string }[] }[] = [
  {
    label: '',
    items: [{ href: '/dashboard', label: 'Painel' }],
  },
  {
    label: 'Financeiro',
    items: [
      { href: '/dashboard/cobranca', label: 'Cobrança' },
      { href: '/dashboard/recorrencia', label: 'Recorrência' },
      { href: '/dashboard/receber', label: 'Contas a receber' },
      { href: '/dashboard/pagar', label: 'Contas a pagar' },
      { href: '/dashboard/tesouraria', label: 'Tesouraria' },
      { href: '/dashboard/caixa', label: 'Caixa' },
      { href: '/dashboard/conciliacao', label: 'Conciliação' },
      { href: '/dashboard/orcamento', label: 'Orçamento' },
      { href: '/dashboard/projetos', label: 'Projetos & voluntariado' },
      { href: '/dashboard/fechamento', label: 'Fechamento' },
    ],
  },
  {
    label: 'Contabilidade',
    items: [
      { href: '/dashboard/contabilidade', label: 'Balancete & razão' },
      { href: '/dashboard/demonstracoes', label: 'Demonstrações' },
    ],
  },
  {
    label: 'Gestão',
    items: [
      { href: '/dashboard/doadores', label: 'Doadores' },
      { href: '/dashboard/relatorios', label: 'Relatórios' },
      { href: '/dashboard/auditoria', label: 'Auditoria' },
    ],
  },
  {
    label: 'Organização',
    items: [{ href: '/dashboard/configuracoes', label: 'Configurações' }],
  },
];

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [session, setSession] = useState<LoginResult | null>(null);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) setSession(JSON.parse(raw) as LoginResult);
  }, []);

  // Fecha o menu móvel ao navegar.
  useEffect(() => setOpen(false), [pathname]);

  function logout() {
    sessionStorage.removeItem('fidellis.session');
    router.push('/login');
  }

  const isActive = (href: string) =>
    href === '/dashboard' ? pathname === href : pathname.startsWith(href);

  return (
    <div className="app">
      <div className="sidebar-backdrop" data-open={open} onClick={() => setOpen(false)} />

      <aside className="sidebar" data-open={open}>
        <Link href="/dashboard" className="sidebar-brand" style={{ textDecoration: 'none' }}>
          <span className="mark">F</span>
          <span>
            <span className="name">Fidellis</span>
            <span className="tag">Gestão · Terceiro setor</span>
          </span>
        </Link>

        <nav className="sidenav">
          {NAV.map((group, gi) => (
            <div className="nav-group" key={group.label || `g${gi}`}>
              {group.label && <div className="nav-group-label">{group.label}</div>}
              {group.items.map((item) => (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`side-link${isActive(item.href) ? ' active' : ''}`}
                >
                  <span className="side-ico" aria-hidden />
                  {item.label}
                </Link>
              ))}
            </div>
          ))}
        </nav>

        <div className="sidebar-foot">
          {session?.activeTenant && (
            <span className="tenant-chip">
              <span className="dot ok" /> {session.activeTenant}
            </span>
          )}
          <span className="who">{session?.user.displayName ?? session?.user.email ?? '—'}</span>
          <button className="linkbtn" onClick={logout}>Sair</button>
        </div>
      </aside>

      <div className="app-main">
        <header className="appbar">
          <button className="menu-btn" aria-label="Abrir menu" onClick={() => setOpen((v) => !v)}>
            ☰
          </button>
          <div className="spacer" />
          {session?.activeTenant && (
            <span className="tenant-chip">
              <span className="dot ok" /> {session.activeTenant}
            </span>
          )}
        </header>

        <main className="content">{children}</main>
      </div>
    </div>
  );
}
