'use client';

import { useEffect, useState } from 'react';
import { Panel } from '../../components/Panel';
import { listAuditLog, type AuditEntry, type LoginResult } from '../../lib/api';

export default function AuditoriaPage() {
  const [items, setItems] = useState<AuditEntry[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const token = (JSON.parse(raw) as LoginResult).accessToken;
    listAuditLog(token)
      .then(setItems)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, []);

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Auditoria</h1>
          <p className="subtitle">Trilha das ações sensíveis do tenant (LGPD e governança).</p>
        </div>
      </div>

      <div className="rise rise-2">
        <Panel title="Eventos" actions={<span className="muted">{items.length}</span>} flush>
          {error && <p className="error-text" style={{ padding: '1rem' }}>{error}</p>}
          {items.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum evento registrado ainda.</p>
          ) : (
            <table className="table">
              <thead>
                <tr><th>Data</th><th>Ação</th><th>Entidade</th><th>Ator</th></tr>
              </thead>
              <tbody>
                {items.map((a) => (
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
        </Panel>
      </div>
    </>
  );
}
