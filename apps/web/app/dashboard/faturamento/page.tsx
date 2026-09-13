'use client';

import { useCallback, useEffect, useState } from 'react';
import { ListCard, PageHeader, StatusBadge } from '../../components/Fiori';
import { Panel } from '../../components/Panel';
import {
  cancelFiscalDocument, listFiscalDocuments, listReceivables, registerFiscalDocument,
  FISCAL_DOC_TYPES, type FiscalDocType, type FiscalDocument, type LoginResult, type Receivable,
} from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const date = (s: string) => new Date(s).toLocaleDateString('pt-BR');
const isInvoiceable = (r: Receivable) => r.source === 'service' || r.source === 'sale';

export default function FaturamentoPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [items, setItems] = useState<FiscalDocument[]>([]);
  const [receivables, setReceivables] = useState<Receivable[]>([]);

  const [receivableId, setReceivableId] = useState('');
  const [type, setType] = useState<FiscalDocType>('nfse');
  const [number, setNumber] = useState('');
  const [series, setSeries] = useState('');
  const [accessKey, setAccessKey] = useState('');
  const [issuedAt, setIssuedAt] = useState('');
  const [description, setDescription] = useState('');

  const refresh = useCallback(async (t: string) => {
    try {
      const [docs, recv] = await Promise.all([listFiscalDocuments(t), listReceivables(t)]);
      setItems(docs);
      setReceivables(recv.filter(isInvoiceable));
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro ao carregar.'); }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) {
      const t = (JSON.parse(raw) as LoginResult).accessToken;
      setToken(t);
      void refresh(t);
    }
  }, [refresh]);

  const selected = receivables.find((r) => r.id === receivableId);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return setError('Sessão não encontrada.');
    if (!selected) return setError('Selecione o recebível comercial (serviço/venda) que a nota documenta.');
    if (!number) return setError('Número da nota é obrigatório.');
    setError(null);
    try {
      await registerFiscalDocument(token, {
        organizationId: selected.organizationId, type, number, amount: selected.amount, receivableId: selected.id,
        series: series || undefined, accessKey: accessKey || undefined,
        description: description || undefined, issuedAt: issuedAt ? new Date(issuedAt).toISOString() : undefined,
      });
      setNumber(''); setSeries(''); setAccessKey(''); setIssuedAt(''); setDescription('');
      await refresh(token);
    } catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  async function cancel(d: FiscalDocument) {
    if (!token || !window.confirm(`Cancelar a nota ${d.number}?`)) return;
    setError(null);
    try { await cancelFiscalDocument(token, d.id); await refresh(token); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
  }

  return (
    <>
      <PageHeader
        title="Faturamento (NF)"
        subtitle="Registro de notas fiscais (NFS-e/NF-e) emitidas por fora, vinculadas a um recebível comercial (serviço/venda). Doações usam recibo, não nota fiscal."
      />

      {error && <p className="error-text">{error}</p>}

      <div className="grid cols-2 rise rise-2" style={{ marginTop: '1rem', alignItems: 'start' }}>
        <Panel title="Registrar nota fiscal">
          {receivables.length === 0 ? (
            <p className="muted">
              Nenhum recebível comercial. Crie um recebível de tipo <strong>Serviço</strong> ou{' '}
              <strong>Venda</strong> em <em>Contas a receber</em> para poder registrar a nota.
            </p>
          ) : (
            <form onSubmit={onSubmit}>
              <div className="field">
                <label htmlFor="f-recv">Recebível comercial</label>
                <select id="f-recv" value={receivableId} onChange={(e) => setReceivableId(e.target.value)} required>
                  <option value="">Selecione…</option>
                  {receivables.map((r) => (
                    <option key={r.id} value={r.id}>
                      {(r.description || (r.source === 'sale' ? 'Venda' : 'Serviço'))} — {brl(r.amount)}
                    </option>
                  ))}
                </select>
              </div>
              <div style={{ display: 'flex', gap: '0.75rem' }}>
                <div className="field" style={{ width: 170 }}>
                  <label htmlFor="f-type">Tipo</label>
                  <select id="f-type" value={type} onChange={(e) => setType(e.target.value as FiscalDocType)}>
                    {FISCAL_DOC_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                  </select>
                </div>
                <div className="field" style={{ flex: 1 }}>
                  <label htmlFor="f-num">Número</label>
                  <input id="f-num" value={number} onChange={(e) => setNumber(e.target.value)} required />
                </div>
                <div className="field" style={{ width: 90 }}>
                  <label htmlFor="f-serie">Série</label>
                  <input id="f-serie" value={series} onChange={(e) => setSeries(e.target.value)} />
                </div>
              </div>
              <div style={{ display: 'flex', gap: '0.75rem' }}>
                <div className="field" style={{ flex: 1 }}>
                  <label htmlFor="f-key">Chave / protocolo</label>
                  <input id="f-key" value={accessKey} onChange={(e) => setAccessKey(e.target.value)} />
                </div>
                <div className="field" style={{ width: 160 }}>
                  <label htmlFor="f-date">Emissão</label>
                  <input id="f-date" type="date" value={issuedAt} onChange={(e) => setIssuedAt(e.target.value)} />
                </div>
              </div>
              <div className="field">
                <label htmlFor="f-desc">Descrição (opcional)</label>
                <input id="f-desc" value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
              {selected && <p className="muted" style={{ fontSize: '0.8rem' }}>Valor da nota: <strong>{brl(selected.amount)}</strong> (do recebível).</p>}
              <button className="btn btn-primary" type="submit">Registrar nota</button>
            </form>
          )}
        </Panel>

        <ListCard label="Notas registradas" count={items.length}>
          {items.length === 0 ? (
            <p className="muted" style={{ padding: '1.25rem' }}>Nenhuma nota registrada ainda.</p>
          ) : (
            <table className="table">
              <thead><tr><th>Número</th><th>Tipo</th><th className="num">Valor</th><th>Emissão</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {items.map((d) => (
                  <tr key={d.id}>
                    <td>{d.number}{d.series && <span className="muted"> / {d.series}</span>}{d.description && <><br /><span className="muted" style={{ fontSize: '0.75rem' }}>{d.description}</span></>}</td>
                    <td className="muted">{d.type === 'nfse' ? 'NFS-e' : 'NF-e'}</td>
                    <td className="num"><strong>{brl(d.amount)}</strong></td>
                    <td className="muted">{date(d.issuedAt)}</td>
                    <td><StatusBadge status={d.status} /></td>
                    <td className="num">{d.status !== 'canceled' && <button className="btn btn-ghost btn-sm" onClick={() => cancel(d)}>Cancelar</button>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ListCard>
      </div>
    </>
  );
}
