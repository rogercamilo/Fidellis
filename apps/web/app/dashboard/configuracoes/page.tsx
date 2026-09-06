'use client';

import { useCallback, useEffect, useState } from 'react';
import { FinanceNav } from '../../components/FinanceNav';
import { Panel } from '../../components/Panel';
import {
  createCategory, createCostCenter, createDonorType, createFund,
  getFinanceSettings, listCategories, listCostCenters, listDonorTypes, listFunds,
  updateFinanceSettings,
  type CostCenter, type Fund, type DonorTypeItem, type FinanceCategoryItem, type LoginResult,
} from '../../lib/api';

export default function ConfiguracoesPage() {
  const [token, setToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [recurringLabel, setRecurringLabel] = useState('Dízimo');
  const [onetimeLabel, setOnetimeLabel] = useState('Oferta');
  const [savedMsg, setSavedMsg] = useState<string | null>(null);

  const [costCenters, setCostCenters] = useState<CostCenter[]>([]);
  const [funds, setFunds] = useState<Fund[]>([]);
  const [donorTypes, setDonorTypes] = useState<DonorTypeItem[]>([]);
  const [categories, setCategories] = useState<FinanceCategoryItem[]>([]);

  const [ccCode, setCcCode] = useState('');
  const [ccName, setCcName] = useState('');
  const [fCode, setFCode] = useState('');
  const [fName, setFName] = useState('');
  const [fRestriction, setFRestriction] = useState('free');
  const [fPurpose, setFPurpose] = useState('');
  const [dtName, setDtName] = useState('');
  const [catKind, setCatKind] = useState('expense');
  const [catName, setCatName] = useState('');

  const refresh = useCallback(async (t: string) => {
    try {
      const [s, cc, fn, dt, ct] = await Promise.all([
        getFinanceSettings(t), listCostCenters(t), listFunds(t), listDonorTypes(t), listCategories(t),
      ]);
      setRecurringLabel(s.recurringLabel);
      setOnetimeLabel(s.onetimeLabel);
      setCostCenters(cc);
      setFunds(fn);
      setDonorTypes(dt);
      setCategories(ct);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar.');
    }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (raw) {
      const t = (JSON.parse(raw) as LoginResult).accessToken;
      setToken(t);
      void refresh(t);
    }
  }, [refresh]);

  function guard<T>(fn: () => Promise<T>) {
    return async () => {
      if (!token) return setError('Sessão não encontrada.');
      setError(null);
      try { await fn(); await refresh(token); }
      catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
    };
  }

  const saveSettings = guard(async () => {
    await updateFinanceSettings(token!, { recurringLabel, onetimeLabel });
    setSavedMsg('Salvo.');
    setTimeout(() => setSavedMsg(null), 2000);
  });
  const addCostCenter = guard(async () => { await createCostCenter(token!, { code: ccCode, name: ccName }); setCcCode(''); setCcName(''); });
  const addFund = guard(async () => { await createFund(token!, { code: fCode, name: fName, restriction: fRestriction, purpose: fPurpose || undefined }); setFCode(''); setFName(''); setFPurpose(''); });
  const addDonorType = guard(async () => { await createDonorType(token!, { name: dtName }); setDtName(''); });
  const addCategory = guard(async () => { await createCategory(token!, { kind: catKind, name: catName }); setCatName(''); });

  return (
    <>
      <div className="page-head rise">
        <div>
          <h1>Configurações financeiras</h1>
          <p className="subtitle">Nomenclatura, dimensões gerenciais, tipos de doador e rubricas do plano de contas.</p>
        </div>
      </div>

      <FinanceNav />

      {error && <p className="error-text">{error}</p>}

      <div className="grid cols-2 rise rise-2" style={{ alignItems: 'start' }}>
        <Panel title="Nomenclatura" actions={savedMsg && <span className="badge ok">{savedMsg}</span>}>
          <div className="field">
            <label htmlFor="rl">Doação recorrente</label>
            <input id="rl" value={recurringLabel} onChange={(e) => setRecurringLabel(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="ol">Doação pontual</label>
            <input id="ol" value={onetimeLabel} onChange={(e) => setOnetimeLabel(e.target.value)} />
          </div>
          <button className="btn btn-primary" onClick={saveSettings}>Salvar</button>
        </Panel>

        <Panel title="Tipos de doador" flush>
          <div style={{ display: 'flex', gap: '0.5rem', padding: '0.75rem 1rem' }}>
            <input placeholder="Ex.: Membro" value={dtName} onChange={(e) => setDtName(e.target.value)} />
            <button className="btn btn-ghost btn-sm" onClick={addDonorType}>Adicionar</button>
          </div>
          <ul className="list">
            {donorTypes.map((d) => (
              <li key={d.id}>{d.name}{d.isRecurringDefault && <span className="badge ok" style={{ marginLeft: 6 }}>recorrente-default</span>}</li>
            ))}
          </ul>
        </Panel>

        <Panel title="Centros de custo" flush>
          <div style={{ display: 'flex', gap: '0.5rem', padding: '0.75rem 1rem' }}>
            <input placeholder="Código" style={{ width: 110 }} value={ccCode} onChange={(e) => setCcCode(e.target.value)} />
            <input placeholder="Nome" value={ccName} onChange={(e) => setCcName(e.target.value)} />
            <button className="btn btn-ghost btn-sm" onClick={addCostCenter}>Adicionar</button>
          </div>
          <ul className="list">
            {costCenters.map((c) => (
              <li key={c.id}><code>{c.code}</code> {c.name}{c.isDefault && <span className="badge muted" style={{ marginLeft: 6 }}>default</span>}</li>
            ))}
          </ul>
        </Panel>

        <Panel title="Fundos (com/sem restrição)" flush>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', padding: '0.75rem 1rem' }}>
            <input placeholder="Código" style={{ width: 90 }} value={fCode} onChange={(e) => setFCode(e.target.value)} />
            <input placeholder="Nome" style={{ flex: 1 }} value={fName} onChange={(e) => setFName(e.target.value)} />
            <select value={fRestriction} onChange={(e) => setFRestriction(e.target.value)}>
              <option value="free">Livre</option>
              <option value="restricted">Restrito</option>
            </select>
            {fRestriction === 'restricted' && (
              <input placeholder="Finalidade (obrigatória)" style={{ flexBasis: '100%' }} value={fPurpose} onChange={(e) => setFPurpose(e.target.value)} />
            )}
            <button className="btn btn-ghost btn-sm" onClick={addFund}>Adicionar</button>
          </div>
          <ul className="list">
            {funds.map((f) => (
              <li key={f.id}>
                <code>{f.code}</code> {f.name}{' '}
                <span className={`badge ${f.restriction === 'restricted' ? 'warn' : 'muted'}`}>{f.restriction === 'restricted' ? 'restrito' : 'livre'}</span>
                {f.purpose && <span className="muted" style={{ marginLeft: 6, fontSize: '0.8rem' }}>· {f.purpose}</span>}
              </li>
            ))}
          </ul>
        </Panel>

        <Panel title="Rubricas (receita/despesa)" flush>
          <div style={{ display: 'flex', gap: '0.5rem', padding: '0.75rem 1rem' }}>
            <select value={catKind} onChange={(e) => setCatKind(e.target.value)}>
              <option value="expense">Despesa</option>
              <option value="revenue">Receita</option>
            </select>
            <input placeholder="Nome da rubrica" style={{ flex: 1 }} value={catName} onChange={(e) => setCatName(e.target.value)} />
            <button className="btn btn-ghost btn-sm" onClick={addCategory}>Adicionar</button>
          </div>
          <ul className="list">
            {categories.map((c) => (
              <li key={c.id}><span className={`badge ${c.kind === 'revenue' ? 'ok' : 'warn'}`}>{c.kind === 'revenue' ? 'receita' : 'despesa'}</span> {c.name}</li>
            ))}
          </ul>
        </Panel>
      </div>
    </>
  );
}
