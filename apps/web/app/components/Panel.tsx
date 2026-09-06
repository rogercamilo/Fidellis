import type { ReactNode } from 'react';

/** Painel/"widget" com cabeçalho titulado — bloco base do console. */
export function Panel({
  title,
  actions,
  children,
  flush = false,
  className,
}: {
  title: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  flush?: boolean;
  className?: string;
}) {
  return (
    <section className={`panel${className ? ` ${className}` : ''}`}>
      <div className="panel-header">
        <h2 className="panel-title">{title}</h2>
        {actions && <div className="panel-actions">{actions}</div>}
      </div>
      <div className={`panel-body${flush ? ' flush' : ''}`}>{children}</div>
    </section>
  );
}
