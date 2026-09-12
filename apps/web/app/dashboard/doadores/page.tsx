'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import { FilterBar, FilterChips, ListCard, PageHeader } from '../../components/Fiori';
import { listDonors, setDonorMember, type DonorSummary, type LoginResult } from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

function situacaoBadge(s: string): string {
  if (s === 'recorrente' || s === 'ativo') return 'ok';
  if (s === 'inativo') return 'err';
  return 'muted';
}

type Situacao = 'todos' | 'recorrente' | 'ativo' | 'inativo';

export default function DoadoresPage() {
  const [items, setItems] = useState<DonorSummary[]>([]);
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [situacao, setSituacao] = useState<Situacao>('todos');
  const [q, setQ] = useState('');

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const t = (JSON.parse(raw) as LoginResult).accessToken;
    setToken(t);
    listDonors(t)
      .then(setItems)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, []);

  async function toggleMember(d: DonorSummary) {
    if (!token) return;
    setError(null);
    try {
      await setDonorMember(token, d.id, !d.isMember);
      setItems(await listDonors(token));
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao atualizar membro.'); }
  }

  const filtered = useMemo(() => {
    const term = q.trim().toLowerCase();
    return items.filter((d) => {
      const okS = situacao === 'todos' || d.situacao === situacao;
      const okQ = !term || d.name.toLowerCase().includes(term) || (d.email ?? '').toLowerCase().includes(term);
      return okS && okQ;
    });
  }, [items, situacao, q]);

  return (
    <>
      <PageHeader
        title="Doadores"
        subtitle="Pessoas e empresas que apoiam sua organização — histórico e situação de cada uma."
      />

      <FilterBar>
        <FilterChips
          value={situacao}
          onChange={setSituacao}
          options={[
            { value: 'todos', label: 'Todos' },
            { value: 'recorrente', label: 'Recorrentes' },
            { value: 'ativo', label: 'Ativos' },
            { value: 'inativo', label: 'Inativos' },
          ]}
        />
        <div className="grow" />
        <input
          className="filter-search"
          placeholder="Buscar por nome ou e-mail…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
      </FilterBar>

      <ListCard label="Doadores" count={filtered.length}>
        {error && <p className="error-text" style={{ padding: '1rem' }}>{error}</p>}
        {filtered.length === 0 ? (
          <p className="muted" style={{ padding: '1.25rem' }}>
            {items.length === 0
              ? 'Nenhum doador ainda. Assim que uma doação for registrada, o doador aparece aqui.'
              : 'Nenhum doador corresponde ao filtro.'}
          </p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Doador</th>
                <th>Situação</th>
                <th className="num">Doações</th>
                <th className="num">Total doado</th>
                <th className="num">Última doação</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {filtered.map((d) => (
                <tr key={d.id}>
                  <td>
                    {d.name}
                    {d.isMember && <span className="badge ok" style={{ marginLeft: 6 }}>membro</span>}
                    {d.email && <div className="muted mono" style={{ fontSize: '0.76rem' }}>{d.email}</div>}
                  </td>
                  <td><span className={`badge ${situacaoBadge(d.situacao)}`}>{d.situacao}</span></td>
                  <td className="num">{d.donations}</td>
                  <td className="num">{brl(d.totalPaid)}</td>
                  <td className="num muted">{d.lastPaidAt ? new Date(d.lastPaidAt).toLocaleDateString('pt-BR') : '—'}</td>
                  <td className="num" style={{ whiteSpace: 'nowrap' }}>
                    <button className="btn btn-ghost btn-sm" onClick={() => toggleMember(d)}>{d.isMember ? 'Remover membro' : 'Tornar membro'}</button>{' '}
                    <Link className="btn btn-ghost btn-sm" href={`/dashboard/doadores/${d.id}`}>Abrir</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </ListCard>
    </>
  );
}
