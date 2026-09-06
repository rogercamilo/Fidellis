'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

/** Áreas de gestão financeira (sub-nav "Financeiro"). Cresce a cada incremento do front. */
const AREAS = [
  { href: '/dashboard/tesouraria', label: 'Tesouraria' },
  { href: '/dashboard/receber', label: 'A Receber' },
  { href: '/dashboard/pagar', label: 'A Pagar' },
  { href: '/dashboard/caixa', label: 'Caixa' },
  { href: '/dashboard/fechamento', label: 'Fechamento' },
  { href: '/dashboard/configuracoes', label: 'Configurações' },
  // FE-6 acrescenta: cobrança multi-método (evolui a tela de Cobrança existente).
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
