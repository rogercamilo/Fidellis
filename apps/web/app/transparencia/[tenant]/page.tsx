'use client';

import { useEffect, useState } from 'react';
import { publicTransparency, type TransparencySummary } from '../../lib/api';

const brl = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const currentYear = new Date().getFullYear();

/**
 * Portal público de transparência (RF-FIN-164): resumo consolidado do exercício/trimestre, sem dados
 * pessoais. Acessível sem login — o tenant vem do path (/transparencia/<slug>).
 */
export default function TransparenciaPage({ params }: { params: { tenant: string } }) {
  const { tenant } = params;
  const [year, setYear] = useState(currentYear);
  const [quarter, setQuarter] = useState(0); // 0 = ano inteiro
  const [data, setData] = useState<TransparencySummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    setData(null);
    publicTransparency(tenant, year, quarter || undefined)
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : 'Erro.'));
  }, [tenant, year, quarter]);

  const years = Array.from({ length: 6 }, (_, i) => currentYear - i);

  return (
    <div className="auth-wrap" style={{ alignItems: 'start', paddingTop: '3rem' }}>
      <div style={{ width: '100%', maxWidth: 560 }}>
        <div className="auth-brand" style={{ justifyContent: 'center' }}>
          <span className="mark">F</span> Fidellis
        </div>

        <div className="auth-card rise">
          <h1 style={{ fontSize: '1.4rem' }}>Transparência</h1>
          <p className="muted" style={{ marginTop: '-0.25rem' }}>
            Prestação de contas de <strong>{tenant}</strong> — resumo consolidado, sem dados pessoais.
          </p>

          <div style={{ display: 'flex', gap: '0.75rem', marginTop: '1rem' }}>
            <div className="field" style={{ flex: 1 }}>
              <label htmlFor="tr-year">Exercício</label>
              <select id="tr-year" value={year} onChange={(e) => setYear(Number(e.target.value))}>
                {years.map((y) => <option key={y} value={y}>{y}</option>)}
              </select>
            </div>
            <div className="field" style={{ flex: 1 }}>
              <label htmlFor="tr-q">Período</label>
              <select id="tr-q" value={quarter} onChange={(e) => setQuarter(Number(e.target.value))}>
                <option value={0}>Ano inteiro</option>
                <option value={1}>1º trimestre</option>
                <option value={2}>2º trimestre</option>
                <option value={3}>3º trimestre</option>
                <option value={4}>4º trimestre</option>
              </select>
            </div>
          </div>

          {error && <p className="error-text">{error}</p>}

          {!data ? (
            <p className="muted" style={{ marginTop: '1rem' }}>Carregando…</p>
          ) : (
            <table className="table" style={{ marginTop: '1rem' }}>
              <tbody>
                <tr><td>Receitas do período</td><td className="num" style={{ color: '#2f9e6b' }}>{brl(data.revenues)}</td></tr>
                <tr><td>Despesas do período</td><td className="num" style={{ color: '#d0483c' }}>−{brl(data.expenses)}</td></tr>
                <tr><td><strong>{data.surplus >= 0 ? 'Superávit' : 'Déficit'} do período</strong></td><td className="num"><strong>{brl(data.surplus)}</strong></td></tr>
                <tr><td style={{ paddingTop: '0.75rem' }}>Ativos</td><td className="num" style={{ paddingTop: '0.75rem' }}>{brl(data.assets)}</td></tr>
                <tr><td>Passivos</td><td className="num">{brl(data.liabilities)}</td></tr>
                <tr><td><strong>Patrimônio líquido</strong></td><td className="num"><strong>{brl(data.netEquity)}</strong></td></tr>
              </tbody>
            </table>
          )}

          <p className="muted" style={{ fontSize: '0.8rem', marginTop: '1rem' }}>
            Fidellis · prestação de contas do terceiro setor.
          </p>
        </div>
      </div>
    </div>
  );
}
