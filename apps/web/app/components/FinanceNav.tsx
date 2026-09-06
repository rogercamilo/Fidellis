'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

/** Áreas de gestão financeira (sub-nav "Financeiro"). Cresce a cada incremento do front. */
const AREAS = [
  { href: '/dashboard/tesouraria', label: 'Tesouraria' },
  { href: '/dashboard/configuracoes', label: 'Configurações' },
  // FE-3..6 acrescentam: Receber, Pagar, Caixa, Fechamento.
];

/** Sub-navegação segmentada das telas financeiras (decisão de navegação A). */
export function FinanceNav() {
  const pathname = usePathname();
  return (
    <nav className="subnav rise" style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem' }}>
      {AREAS.map((a) => (
        <Link
          key={a.href}
          href={a.href}
          className={`navlink${pathname.startsWith(a.href) ? ' active' : ''}`}
        >
          {a.label}
        </Link>
      ))}
    </nav>
  );
}
