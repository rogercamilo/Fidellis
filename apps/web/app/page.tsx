import Link from 'next/link';

const modules = [
  { title: 'Doações & PIX', desc: 'Dízimos, ofertas e campanhas com cobrança PIX, conciliação e recibos automáticos.' },
  { title: 'Rede → Unidade', desc: 'Diocese, paróquias, comunidades e filiais — com visibilidade em cascata.' },
  { title: 'Recorrência + dunning', desc: 'Dízimo mensal com régua de recuperação (D+1, D+3, D+5).' },
  { title: 'Prestação de contas', desc: 'Plano de contas, balancete consolidado e recibos numerados.' },
];

export default function HomePage() {
  return (
    <div>
      <header
        style={{
          background: 'linear-gradient(180deg, #10202e, #0b1622)',
          borderBottom: '2px solid var(--gold)',
        }}
      >
        <div style={{ maxWidth: 1120, margin: '0 auto', padding: '1rem 1.5rem', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span className="brand"><span className="mark">F</span> Fidellis</span>
          <nav style={{ display: 'flex', gap: '0.6rem' }}>
            <Link className="btn btn-dark" href="/login">Entrar</Link>
            <Link className="btn btn-primary" href="/signup">Criar instituição</Link>
          </nav>
        </div>
      </header>

      {/* Hero */}
      <section
        style={{
          position: 'relative',
          color: '#e9eef3',
          background:
            'radial-gradient(1100px 520px at 78% -12%, rgba(216,165,49,0.18), transparent 60%), linear-gradient(180deg, #0b1622, #0a131d)',
          overflow: 'hidden',
        }}
      >
        <div style={{ maxWidth: 1120, margin: '0 auto', padding: '5rem 1.5rem 5.5rem' }} className="rise">
          <span className="badge" style={{ background: 'rgba(216,165,49,0.14)', color: 'var(--gold-bright)', borderColor: 'rgba(216,165,49,0.4)' }}>
            Assinatura · 0% de taxa sobre doações
          </span>
          <h1 style={{ color: '#fff', fontSize: 'clamp(2.4rem, 6vw, 4rem)', lineHeight: 1.05, margin: '1.25rem 0 0.75rem', maxWidth: 780 }}>
            A plataforma de doações do <span style={{ color: 'var(--gold-bright)' }}>terceiro setor religioso</span>.
          </h1>
          <p style={{ color: '#aebccb', fontSize: '1.2rem', maxWidth: 620 }}>
            Dízimos, ofertas e campanhas com PIX, recorrência, recibos e prestação de contas — sua
            instituição fica com <strong style={{ color: '#fff' }}>100% das doações</strong>.
          </p>
          <div style={{ display: 'flex', gap: '0.75rem', marginTop: '1.75rem', flexWrap: 'wrap' }}>
            <Link className="btn btn-primary" href="/signup" style={{ padding: '0.75rem 1.5rem', fontSize: '1rem' }}>Começar agora</Link>
            <Link className="btn btn-dark" href="/login" style={{ padding: '0.75rem 1.5rem', fontSize: '1rem' }}>Entrar</Link>
          </div>
        </div>
      </section>

      {/* Módulos */}
      <section style={{ maxWidth: 1120, margin: '0 auto', padding: '3.5rem 1.5rem' }}>
        <h2 style={{ fontSize: '1.6rem' }}>Uma base pensada para redes religiosas</h2>
        <p className="muted" style={{ maxWidth: 560, marginBottom: '1.75rem' }}>
          Multi-tenant, hierarquia Rede→Unidade e contabilidade — do dízimo ao balancete.
        </p>
        <div className="grid cols-2">
          {modules.map((m) => (
            <div key={m.title} className="panel">
              <div className="panel-body">
                <h3 style={{ margin: 0 }}>{m.title}</h3>
                <p className="muted" style={{ margin: '0.4rem 0 0' }}>{m.desc}</p>
              </div>
            </div>
          ))}
        </div>
      </section>

      <footer style={{ background: 'var(--ink)', color: '#8a97a4' }}>
        <div style={{ maxWidth: 1120, margin: '0 auto', padding: '1.25rem 1.5rem', display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem' }}>
          <span className="brand" style={{ fontSize: '1rem' }}><span className="mark" style={{ width: 22, height: 22 }}>F</span> Fidellis</span>
          <span style={{ fontSize: '0.85rem' }}>Assinatura 0% de taxa · terceiro setor religioso</span>
        </div>
      </footer>
    </div>
  );
}
