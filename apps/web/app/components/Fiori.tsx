'use client';

import Link from 'next/link';
import type { ReactNode } from 'react';

/** Cabeçalho de página dinâmica (List Report): título + subtítulo em linguagem clara + ações globais. */
export function PageHeader({
  title, subtitle, actions,
}: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="page-head rise">
      <div>
        <h1>{title}</h1>
        {subtitle && <p className="subtitle">{subtitle}</p>}
      </div>
      {actions && <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>{actions}</div>}
    </div>
  );
}

/** Barra de filtro (variante + filtros), no padrão SAP "Standard / filtros". */
export function FilterBar({
  variant = 'Padrão', children,
}: { variant?: string; children?: ReactNode }) {
  return (
    <div className="filterbar rise rise-2">
      <span className="filter-variant">{variant} ⌄</span>
      <span className="fb-sep" />
      {children}
    </div>
  );
}

/** Grupo de chips de filtro (ex.: situação). */
export function FilterChips<T extends string>({
  value, options, onChange,
}: { value: T; options: { value: T; label: string }[]; onChange: (v: T) => void }) {
  return (
    <div className="filter-chips">
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          className={`chip${value === o.value ? ' active' : ''}`}
          onClick={() => onChange(o.value)}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

/** Cartão de lista com toolbar (título + contagem à esquerda, ações à direita) + conteúdo (tabela). */
export function ListCard({
  label, count, actions, children,
}: { label: string; count: number; actions?: ReactNode; children: ReactNode }) {
  return (
    <div className="panel rise rise-3">
      <div className="obj-toolbar">
        <span className="obj-count">{label} <span className="n">({count})</span></span>
        {actions && <div className="panel-actions">{actions}</div>}
      </div>
      {children}
    </div>
  );
}

/** Header de Object Page: voltar + título + subtítulo + fatos-chave + ações. */
export function ObjectHeader({
  backHref, backLabel, title, subtitle, facts, actions,
}: {
  backHref: string; backLabel: string; title: string; subtitle?: string;
  facts?: ReactNode; actions?: ReactNode;
}) {
  return (
    <div className="obj-header rise">
      <Link href={backHref} className="obj-back">← {backLabel}</Link>
      <div className="obj-headrow">
        <div>
          <h1>{title}</h1>
          {subtitle && <p className="subtitle" style={{ margin: 0 }}>{subtitle}</p>}
        </div>
        {actions && <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>{actions}</div>}
      </div>
      {facts && <div className="obj-facts">{facts}</div>}
    </div>
  );
}

export function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="fact">
      <span className="fact-label">{label}</span>
      <span className="fact-value">{children}</span>
    </div>
  );
}

/** Barra de âncoras das seções do Object Page. */
export function AnchorBar({ items }: { items: { id: string; label: string }[] }) {
  return (
    <div className="anchorbar rise rise-2">
      {items.map((it, i) => (
        <a key={it.id} href={`#${it.id}`} className={`anchor${i === 0 ? ' active' : ''}`}>{it.label}</a>
      ))}
    </div>
  );
}

/** Seção do Object Page: título + descrição em linguagem clara + conteúdo. */
export function ObjectSection({
  id, title, sub, actions, children, delay = 3,
}: { id: string; title: string; sub?: string; actions?: ReactNode; children: ReactNode; delay?: number }) {
  return (
    <section id={id} className={`obj-section rise rise-${delay}`}>
      <div className="obj-section-head">
        <div>
          <h2>{title}</h2>
          {sub && <p className="obj-section-sub">{sub}</p>}
        </div>
        {actions && <div className="panel-actions">{actions}</div>}
      </div>
      {children}
    </section>
  );
}
