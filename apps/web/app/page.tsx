import Link from 'next/link';

const modules = [
  { title: 'Doações & Campanhas', desc: 'Dízimos, ofertas e campanhas com PIX, cartão e boleto.' },
  { title: 'Hierarquia Rede→Unidade', desc: 'Diocese→paróquias, congregações e institutos, com consolidação.' },
  { title: 'CRM 360º do doador', desc: 'Histórico, recorrência e régua de relacionamento.' },
  { title: 'Prestação de contas', desc: 'Recibos e razão contábil automáticos por unidade.' },
];

export default function HomePage() {
  return (
    <main className="container">
      <span className="badge">Assinatura • 0% de taxa sobre doações</span>
      <h1 style={{ fontSize: '2.5rem', margin: '1rem 0 0.5rem' }}>Fidellis</h1>
      <p className="muted" style={{ maxWidth: 640, fontSize: '1.15rem' }}>
        Captação de doações para o terceiro setor religioso. Sua instituição fica com{' '}
        <strong style={{ color: 'var(--fg)' }}>100% das doações</strong> — você paga apenas a
        assinatura, sem taxa por transação.
      </p>

      <div style={{ marginTop: '1.5rem', display: 'flex', gap: '0.75rem' }}>
        <Link className="btn" href="/login">
          Entrar
        </Link>
        <a className="btn" style={{ background: 'transparent', color: 'var(--accent)', border: '1px solid var(--border)' }} href="#modulos">
          Conhecer os módulos
        </a>
      </div>

      <section id="modulos" className="grid">
        {modules.map((m) => (
          <div key={m.title} className="card">
            <h3 style={{ marginTop: 0 }}>{m.title}</h3>
            <p className="muted" style={{ marginBottom: 0 }}>
              {m.desc}
            </p>
          </div>
        ))}
      </section>

      <p className="muted" style={{ marginTop: '2rem', fontSize: '0.9rem' }}>
        Scaffold do produto — arquitetura multi-tenant (schema-per-tenant), auth própria e core .NET
        modular.
      </p>
    </main>
  );
}
