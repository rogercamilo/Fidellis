'use client';

import { Fragment, useCallback, useEffect, useMemo, useState } from 'react';
import { ListCard, PageHeader, StatusBadge } from '../../components/Fiori';
import { Panel } from '../../components/Panel';
import {
  ignoreLine, importStatement, lineSuggestions, listStatements, listTreasuryAccounts, matchLine, statementLines,
  type BankStatement, type LoginResult, type MatchCandidate, type StatementLine, type TreasuryAccount,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const date = (s: string) => new Date(s + (s.length <= 10 ? 'T00:00:00' : '')).toLocaleDateString('pt-BR');

export default function ConciliacaoPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [accounts, setAccounts] = useState<TreasuryAccount[]>([]);
  const [statements, setStatements] = useState<BankStatement[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [lines, setLines] = useState<StatementLine[]>([]);
  const [suggestFor, setSuggestFor] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<MatchCandidate[]>([]);

  const [accountId, setAccountId] = useState('');
  const [format, setFormat] = useState('ofx');
  const [reference, setReference] = useState('');
  const [content, setContent] = useState('');

  const accountName = useMemo(() => new Map(accounts.map((a) => [a.id, a.name])), [accounts]);

  const refresh = useCallback(async (t: string) => {
    try {
      const [ac, st] = await Promise.all([listTreasuryAccounts(t), listStatements(t)]);
      setAccounts(ac); setStatements(st);
      if (!accountId && ac.some((a) => a.kind === 'bank')) setAccountId(ac.find((a) => a.kind === 'bank')!.id);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao carregar.'); }
  }, [accountId]);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) { const t = (JSON.parse(raw) as LoginResult).accessToken; setToken(t); void refresh(t); }
  }, [refresh]);

  async function openStatement(id: string) {
    if (!token) return;
    setSelected(id); setSuggestFor(null); setSuggestions([]);
    try { setLines(await statementLines(token, id)); } catch { setLines([]); }
  }

  async function doImport(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    if (!accountId || !content.trim()) return setError('Selecione a conta e informe o conteúdo do arquivo.');
    setError(null);
    try {
      const r = await importStatement(token, { accountId, content, format, reference: reference || undefined });
      setContent(''); setReference('');
      await refresh(token);
      await openStatement(r.id);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao importar.'); }
  }

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setContent(await file.text());
    if (file.name.toLowerCase().endsWith('.ret') || file.name.toLowerCase().endsWith('.cnab')) setFormat('cnab');
    else setFormat('ofx');
    setReference(file.name);
  }

  async function suggest(lineId: string) {
    if (!token) return;
    setSuggestFor(lineId);
    try { setSuggestions(await lineSuggestions(token, lineId)); } catch { setSuggestions([]); }
  }

  async function confirm(lineId: string, c: MatchCandidate) {
    if (!token) return;
    setError(null);
    try { await matchLine(token, lineId, { type: c.type, id: c.id }); setSuggestFor(null); if (selected) await openStatement(selected); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro ao casar.'); }
  }

  async function ignore(lineId: string) {
    if (!token) return;
    try { await ignoreLine(token, lineId); if (selected) await openStatement(selected); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro ao ignorar.'); }
  }

  return (
    <>
      <PageHeader
        title="Conciliação bancária"
        subtitle="Importe o extrato do banco (OFX/CNAB) e concilie cada lançamento com contas a receber e a pagar."
      />

      {error && <p className="error-text">{error}</p>}

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        <Panel title="Importar extrato">
          <form onSubmit={doImport}>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
              <div className="field" style={{ flex: 1 }}>
                <label htmlFor="cc-acc">Conta</label>
                <select id="cc-acc" value={accountId} onChange={(e) => setAccountId(e.target.value)}>
                  <option value="">—</option>
                  {accounts.filter((a) => a.kind === 'bank').map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                </select>
              </div>
              <div className="field" style={{ width: 120 }}>
                <label htmlFor="cc-fmt">Formato</label>
                <select id="cc-fmt" value={format} onChange={(e) => setFormat(e.target.value)}>
                  <option value="ofx">OFX</option>
                  <option value="cnab">CNAB</option>
                </select>
              </div>
            </div>
            <div className="field">
              <label htmlFor="cc-file">Arquivo (.ofx / .ret)</label>
              <input id="cc-file" type="file" accept=".ofx,.ret,.cnab,.txt" onChange={onFile} />
            </div>
            <div className="field">
              <label htmlFor="cc-content">Conteúdo (ou cole aqui)</label>
              <textarea id="cc-content" rows={4} value={content} onChange={(e) => setContent(e.target.value)} className="mono" style={{ fontSize: '0.75rem' }} />
            </div>
            <button className="btn btn-primary" type="submit">Importar</button>
          </form>
        </Panel>

        <ListCard label="Extratos" count={statements.length}>
          {statements.length === 0 ? (
            <p className="muted" style={{ padding: '1.25rem' }}>Nenhum extrato importado.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Referência</th><th>Conta</th><th>Formato</th><th></th></tr></thead>
              <tbody>
                {statements.map((s) => (
                  <tr key={s.id} style={selected === s.id ? { background: '#f5f7fa' } : undefined}>
                    <td>{s.reference || date(s.importedAt)}</td>
                    <td className="muted">{accountName.get(s.accountId) ?? '—'}</td>
                    <td><span className="badge muted">{s.format}</span></td>
                    <td className="num"><button className="btn btn-ghost btn-sm" onClick={() => openStatement(s.id)}>Abrir</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ListCard>
      </div>

      {selected && (
        <div className="rise rise-3" style={{ marginTop: '1rem' }}>
          <ListCard label="Linhas do extrato" count={lines.length}>
            {lines.length === 0 ? (
              <p className="muted" style={{ padding: '1.25rem' }}>Sem linhas.</p>
            ) : (
              <table className="table">
                <thead><tr><th>Data</th><th>Histórico</th><th className="num">Valor</th><th>Status</th><th></th></tr></thead>
                <tbody>
                  {lines.map((l) => (
                    <Fragment key={l.id}>
                      <tr>
                        <td className="muted">{date(l.postedAt)}</td>
                        <td>{l.memo || '—'}</td>
                        <td className="num" style={{ color: l.amount < 0 ? '#d0483c' : '#2f9e6b' }}>{brl(l.amount)}</td>
                        <td><StatusBadge status={l.status} /></td>
                        <td className="num" style={{ whiteSpace: 'nowrap' }}>
                          {l.status === 'unmatched' && (
                            <>
                              <button className="btn btn-ghost btn-sm" onClick={() => suggest(l.id)}>Sugestões</button>
                              <button className="btn btn-ghost btn-sm" style={{ marginLeft: 6 }} onClick={() => ignore(l.id)}>Ignorar</button>
                            </>
                          )}
                        </td>
                      </tr>
                      {suggestFor === l.id && (
                        <tr>
                          <td colSpan={5} style={{ background: '#f8fafc' }}>
                            {suggestions.length === 0 ? (
                              <span className="muted">Nenhum candidato (valor/data ± 3 dias).</span>
                            ) : (
                              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem' }}>
                                {suggestions.map((c) => (
                                  <div key={`${c.type}-${c.id}`} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                    <span className="badge muted">{c.type === 'receivable' ? 'a receber' : 'a pagar'}</span>
                                    <span>{c.description}</span>
                                    <span className="muted">{brl(c.amount)} · vence {date(c.date)}</span>
                                    <button className="btn btn-primary btn-sm" style={{ marginLeft: 'auto' }} onClick={() => confirm(l.id, c)}>Casar</button>
                                  </div>
                                ))}
                              </div>
                            )}
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            )}
          </ListCard>
        </div>
      )}
    </>
  );
}
