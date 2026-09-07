'use client';

import { useEffect, useMemo, useState } from 'react';
import { FilterBar, ListCard, PageHeader } from '../../components/Fiori';
import { listAuditLog, type AuditEntry, type LoginResult } from '../../lib/api';

export default function AuditoriaPage() {
  const [items, setItems] = useState<AuditEntry[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [q, setQ] = useState('');

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const token = (JSON.parse(raw) as LoginResult).accessToken;
    listAuditLog(token)
      .then(setItems)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, []);

  const filtered = useMemo(() => {
    const term = q.trim().toLowerCase();
    if (!term) return items;
    return items.filter((a) => a.action.toLowerCase().includes(term) || a.entity.toLowerCase().includes(term));
  }, [items, q]);

  return (
    <>
      <PageHeader
        title="Auditoria"
        subtitle="Registro de todas as ações sensíveis da sua organização — transparência e conformidade (LGPD)."
      />

      <FilterBar>
        <div className="grow" />
        <input
          className="filter-search"
          placeholder="Buscar por ação ou entidade…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
      </FilterBar>

      <ListCard label="Eventos" count={filtered.length}>
        {error && <p className="error-text" style={{ padding: '1rem' }}>{error}</p>}
        {filtered.length === 0 ? (
          <p className="muted" style={{ padding: '1.25rem' }}>
            {items.length === 0 ? 'Nenhum evento registrado ainda.' : 'Nenhum evento corresponde à busca.'}
          </p>
        ) : (
          <table className="table">
            <thead>
              <tr><th>Data e hora</th><th>Ação</th><th>Entidade</th><th>Responsável</th></tr>
            </thead>
            <tbody>
              {filtered.map((a) => (
                <tr key={a.id}>
                  <td className="muted">{new Date(a.createdAt).toLocaleString('pt-BR')}</td>
                  <td className="mono">{a.action}</td>
                  <td>{a.entity}{a.entityId && <span className="muted mono" style={{ fontSize: '0.72rem' }}> · {a.entityId.slice(0, 8)}</span>}</td>
                  <td className="muted mono" style={{ fontSize: '0.72rem' }}>{a.actorUserId ? a.actorUserId.slice(0, 8) : 'público'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </ListCard>
    </>
  );
}
