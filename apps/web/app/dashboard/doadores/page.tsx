'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Panel } from '../../components/Panel';
import { listDonors, type DonorSummary, type LoginResult } from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

function situacaoBadge(s: string): string {
  if (s === 'recorrente') return 'ok';
  if (s === 'ativo') return 'ok';
  if (s === 'inativo') return 'err';
  return 'muted';
}

export default function DoadoresPage() {
  const [items, setItems] = useState<DonorSummary[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const token = (JSON.parse(raw) as LoginResult).accessToken;
    listDonors(token)
      .then(setItems)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, []);

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Doadores</h1>
          <p className="subtitle">CRM 360º — histórico, situação e relacionamento por doador.</p>
        </div>
      </div>

      <div className="rise rise-2">
        <Panel title="Base de doadores" actions={<span className="muted">{items.length} total</span>} flush>
          {error && <p className="error-text" style={{ padding: '1rem' }}>{error}</p>}
          {items.length === 0 ? (
            <p className="muted" style={{ padding: '1rem' }}>Nenhum doador ainda.</p>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Doador</th>
                  <th>Situação</th>
                  <th className="num">Doações</th>
                  <th className="num">Total</th>
                  <th className="num">Última</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((d) => (
                  <tr key={d.id}>
                    <td>
                      {d.name}
                      {d.email && <div className="muted mono" style={{ fontSize: '0.76rem' }}>{d.email}</div>}
                    </td>
                    <td><span className={`badge ${situacaoBadge(d.situacao)}`}>{d.situacao}</span></td>
                    <td className="num">{d.donations}</td>
                    <td className="num">{brl(d.totalPaid)}</td>
                    <td className="num muted">{d.lastPaidAt ? new Date(d.lastPaidAt).toLocaleDateString('pt-BR') : '—'}</td>
                    <td className="num"><Link className="btn btn-ghost btn-sm" href={`/dashboard/doadores/${d.id}`}>abrir</Link></td>
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
