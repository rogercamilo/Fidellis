'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useState, type ReactNode } from 'react';
import type { LoginResult } from '../lib/api';

const NAV = [
  { href: '/dashboard', label: 'Painel' },
  { href: '/dashboard/cobranca', label: 'Cobrança' },
  { href: '/dashboard/recorrencia', label: 'Recorrência' },
  { href: '/dashboard/doadores', label: 'Doadores' },
  { href: '/dashboard/recibos', label: 'Recibos' },
  { href: '/dashboard/configuracoes', label: 'Financeiro' },
  { href: '/dashboard/contabilidade', label: 'Contabilidade' },
  { href: '/dashboard/relatorios', label: 'Relatórios' },
  { href: '/dashboard/auditoria', label: 'Auditoria' },
];

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [session, setSession] = useState<LoginResult | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) setSession(JSON.parse(raw) as LoginResult);
  }, []);

  function logout() {
    sessionStorage.removeItem('fidellis.session');
    router.push('/login');
  }

  const isActive = (href: string) =>
    href === '/dashboard' ? pathname === href : pathname.startsWith(href);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <header className="topbar">
        <div className="topbar-inner">
          <Link href="/dashboard" className="brand" style={{ textDecoration: 'none' }}>
            <span className="mark">F</span> Fidellis
          </Link>
          <nav className="topnav">
            {NAV.map((n) => (
              <Link key={n.href} href={n.href} className={`navlink${isActive(n.href) ? ' active' : ''}`}>
                {n.label}
              </Link>
            ))}
          </nav>
          <div className="topbar-right">
            {session?.activeTenant && (
              <span className="tenant-chip">
                <span className="dot ok" /> {session.activeTenant}
              </span>
            )}
            <span className="muted" style={{ color: '#aeb9c4' }}>
              {session?.user.displayName ?? session?.user.email ?? '—'}
            </span>
            <button className="linkbtn" onClick={logout}>
              Sair
            </button>
          </div>
        </div>
      </header>

      <main className="container" style={{ flex: 1, width: '100%' }}>
        {children}
      </main>

      <footer className="statusbar">
        <div className="statusbar-inner">
          <span className="live">● online</span>
          <span className="sep">|</span>
          <span>Tenant: {session?.activeTenant ?? '—'}</span>
          <span className="sep">|</span>
          <span>{session?.user.email ?? '—'}</span>
          <span className="sep" style={{ marginLeft: 'auto' }} />
          <span className="muted" style={{ color: '#6b7885' }}>Fidellis · assinatura 0% de taxa</span>
        </div>
      </footer>
    </div>
  );
}
