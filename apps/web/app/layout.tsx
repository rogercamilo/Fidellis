import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { IBM_Plex_Mono, Inter } from 'next/font/google';
import './globals.css';

// Inter aproxima o tipo "72" da SAP (UI). IBM Plex Mono para dados/números tabulares.
const sans = Inter({
  subsets: ['latin'],
  weight: ['400', '500', '600', '700'],
  variable: '--font-inter',
  display: 'swap',
});

const mono = IBM_Plex_Mono({
  subsets: ['latin'],
  weight: ['400', '500', '600'],
  variable: '--font-plex-mono',
  display: 'swap',
});

export const metadata: Metadata = {
  title: 'Fidellis — Gestão para instituições do terceiro setor',
  description:
    'ERP de gestão administrativa para organizações do terceiro setor: finanças, prestação de contas e governança em uma plataforma.',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="pt-BR" className={`${sans.variable} ${mono.variable}`}>
      <body>{children}</body>
    </html>
  );
}
