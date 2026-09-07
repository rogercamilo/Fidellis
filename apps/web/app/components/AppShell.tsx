'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useState, type ReactNode } from 'react';
import type { LoginResult } from '../lib/api';

/** Navegação do ERP, agrupada por módulo (estilo SAP Fiori). Cresce a cada novo bloco. */
const NAV: { label: string; items: { href: string; label: string }[] }[] = [
  { label: '', items: [{ href: '/dashboard', label: 'Início' }] },
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
  { label: 'Organização', items: [{ href: '/dashboard/configuracoes', label: 'Configurações' }] },
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

  useEffect(() => setOpen(false), [pathname]);

  function logout() {
    sessionStorage.removeItem('fidellis.session');
    router.push('/login');
  }

  const isActive = (href: string) =>
    href === '/dashboard' ? pathname === href : pathname.startsWith(href);

  return (
    <div className="app">
      <header className="shellbar">
        <button className="icon-btn menu-btn" aria-label="Menu" onClick={() => setOpen((v) => !v)}>☰</button>
        <Link href="/dashboard" className="shellbar-brand" style={{ textDecoration: 'none' }}>
          <span className="mark">F</span> Fidellis
        </Link>
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
          {NAV.map((group, gi) => (
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

        <main className="content">{children}</main>
      </div>
    </div>
  );
}
