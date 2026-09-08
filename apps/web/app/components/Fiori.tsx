'use client';

import Link from 'next/link';
import { Fragment, useEffect, useMemo, useState, type ReactNode } from 'react';

/** Tradução dos status técnicos para português claro (requisito: usuários não-técnicos). */
const STATUS_PT: Record<string, { label: string; kind: 'ok' | 'warn' | 'err' | 'muted' }> = {
  paid: { label: 'Pago', kind: 'ok' },
  pending: { label: 'Pendente', kind: 'warn' },
  failed: { label: 'Falhou', kind: 'err' },
  canceled: { label: 'Cancelado', kind: 'muted' },
  expired: { label: 'Expirado', kind: 'muted' },
  refunded: { label: 'Estornado', kind: 'muted' },
  active: { label: 'Ativa', kind: 'ok' },
  past_due: { label: 'Em atraso', kind: 'err' },
  sent: { label: 'Enviado', kind: 'ok' },
  queued: { label: 'Na fila', kind: 'warn' },
  skipped: { label: 'Ignorado', kind: 'muted' },
  awaiting_approval: { label: 'Aguardando aprovação', kind: 'warn' },
  approved: { label: 'Aprovado', kind: 'ok' },
  rejected: { label: 'Rejeitado', kind: 'err' },
  open: { label: 'Em aberto', kind: 'warn' },
  partial: { label: 'Parcial', kind: 'warn' },
  received: { label: 'Recebido', kind: 'ok' },
  matched: { label: 'Conciliado', kind: 'ok' },
  unmatched: { label: 'Não conciliado', kind: 'warn' },
  ignored: { label: 'Ignorado', kind: 'muted' },
  draft: { label: 'Rascunho', kind: 'warn' },
  closed: { label: 'Fechado', kind: 'muted' },
  recorrente: { label: 'Recorrente', kind: 'ok' },
  ativo: { label: 'Ativo', kind: 'ok' },
  inativo: { label: 'Inativo', kind: 'err' },
};

/** Badge de status com rótulo em português. */
export function StatusBadge({ status }: { status: string }) {
  const s = STATUS_PT[status] ?? { label: status, kind: 'muted' as const };
  return <span className={`badge ${s.kind}`}>{s.label}</span>;
}

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

/** Tour de boas-vindas no primeiro acesso (padrão SAP "Start the tour"). */
export function GuidedTour({
  steps,
}: { steps: { title: string; body: string }[] }) {
  const [i, setI] = useState(-1); // -1 = convite; 0..n = passos

  useEffect(() => {
    if (!localStorage.getItem('fidellis.tour.v1')) setI(-1);
    else setI(-2); // -2 = já visto, não mostra
  }, []);

  function done() { localStorage.setItem('fidellis.tour.v1', '1'); setI(-2); }
  if (i === -2) return null;

  const invite = i === -1;
  const step = invite ? null : steps[i];
  const last = i === steps.length - 1;

  return (
    <div className="tour-overlay">
      <div className="tour-card rise" role="dialog" aria-modal="true">
        <button className="tour-x" aria-label="Fechar" onClick={done}>×</button>
        {invite ? (
          <>
            <h3>Bem-vindo ao Fidellis</h3>
            <p className="muted">Quer um tour rápido para conhecer como gerir sua organização por aqui? Leva menos de 1 minuto.</p>
            <div className="tour-actions">
              <button className="btn btn-primary btn-sm" onClick={() => setI(0)}>Fazer o tour</button>
              <button className="btn btn-ghost btn-sm" onClick={done}>Agora não</button>
            </div>
          </>
        ) : step ? (
          <>
            <div className="tour-step">Passo {i + 1} de {steps.length}</div>
            <h3>{step.title}</h3>
            <p className="muted">{step.body}</p>
            <div className="tour-actions">
              <button className="btn btn-primary btn-sm" onClick={() => (last ? done() : setI(i + 1))}>
                {last ? 'Concluir' : 'Próximo'}
              </button>
              <button className="btn btn-ghost btn-sm" onClick={done}>Pular</button>
            </div>
          </>
        ) : null}
      </div>
    </div>
  );
}

/** Tabela em árvore (SAP tree table): hierarquia Rede→Unidade com expandir/recolher. */
export type TreeRow = { id: string; parentId: string | null; label: ReactNode; cells: ReactNode[] };

export function TreeTable({
  headers, rows, footer,
}: { headers: { label: string; num?: boolean }[]; rows: TreeRow[]; footer?: ReactNode }) {
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const ids = useMemo(() => new Set(rows.map((r) => r.id)), [rows]);
  const childrenOf = useMemo(() => {
    const m = new Map<string, TreeRow[]>();
    rows.forEach((r) => {
      const key = r.parentId && ids.has(r.parentId) ? r.parentId : '__root__';
      (m.get(key) ?? m.set(key, []).get(key)!).push(r);
    });
    return m;
  }, [rows, ids]);

  const toggle = (id: string) =>
    setCollapsed((s) => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; });

  const render = (r: TreeRow, depth: number): ReactNode => {
    const kids = childrenOf.get(r.id) ?? [];
    const open = !collapsed.has(r.id);
    return (
      <Fragment key={r.id}>
        <tr>
          <td>
            <span style={{ paddingLeft: `${depth * 1.25}rem`, display: 'inline-flex', alignItems: 'center', gap: '0.35rem' }}>
              {kids.length > 0 ? (
                <button className="tree-toggle" onClick={() => toggle(r.id)} aria-label={open ? 'Recolher' : 'Expandir'}>{open ? '▾' : '▸'}</button>
              ) : (
                <span className="tree-spacer" />
              )}
              {r.label}
            </span>
          </td>
          {r.cells.map((c, i) => <td key={i} className={headers[i + 1]?.num ? 'num' : undefined}>{c}</td>)}
        </tr>
        {open && kids.map((k) => render(k, depth + 1))}
      </Fragment>
    );
  };

  return (
    <table className="table">
      <thead><tr>{headers.map((h, i) => <th key={i} className={h.num ? 'num' : undefined}>{h.label}</th>)}</tr></thead>
      <tbody>{(childrenOf.get('__root__') ?? []).map((r) => render(r, 0))}</tbody>
      {footer && <tfoot>{footer}</tfoot>}
    </table>
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
