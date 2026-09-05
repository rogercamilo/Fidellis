'use client';

import { useCallback, useEffect, useState } from 'react';
import { createOrganization, listOrganizations, type Organization } from '../lib/api';

/**
 * Seletor de unidade (organization). Lista as unidades do tenant e permite criar uma nova inline —
 * evita que o usuário precise digitar um uuid (causa do erro 400 anterior).
 */
export function OrganizationPicker({
  token,
  value,
  onChange,
}: {
  token: string | null;
  value: string;
  onChange: (id: string) => void;
}) {
  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [newName, setNewName] = useState('');
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (t: string) => {
      try {
        const list = await listOrganizations(t);
        setOrgs(list);
        if (!value && list[0]) onChange(list[0].id);
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Falha ao carregar unidades.');
      }
    },
    [value, onChange],
  );

  useEffect(() => {
    if (token) void load(token);
  }, [token, load]);

  async function add() {
    if (!token || !newName.trim()) return;
    setCreating(true);
    setError(null);
    try {
      const created = await createOrganization(token, newName.trim());
      setNewName('');
      const list = await listOrganizations(token);
      setOrgs(list);
      onChange(created.id);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Falha ao criar unidade.');
    } finally {
      setCreating(false);
    }
  }

  return (
    <div>
      <label htmlFor="org-select">Unidade (paróquia/comunidade)</label>
      {orgs.length > 0 ? (
        <select id="org-select" value={value} onChange={(e) => onChange(e.target.value)}>
          {orgs.map((o) => (
            <option key={o.id} value={o.id}>
              {o.name}
            </option>
          ))}
        </select>
      ) : (
        <p className="muted" style={{ fontSize: '0.85rem', margin: '0.25rem 0' }}>
          Nenhuma unidade cadastrada — crie a primeira abaixo.
        </p>
      )}

      <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
        <input
          placeholder="Nova unidade (ex.: Paróquia São José)"
          value={newName}
          onChange={(e) => setNewName(e.target.value)}
        />
        <button
          type="button"
          className="btn"
          onClick={add}
          disabled={creating}
          style={{ background: 'transparent', color: 'var(--accent)', border: '1px solid var(--border)', whiteSpace: 'nowrap' }}
        >
          {creating ? '…' : '+ Criar'}
        </button>
      </div>

      {error && <p style={{ color: '#ff7a7a', fontSize: '0.85rem' }}>{error}</p>}
    </div>
  );
}
