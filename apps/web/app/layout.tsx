import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import './globals.css';

export const metadata: Metadata = {
  title: 'Fidellis — Doações para o terceiro setor religioso',
  description:
    'Plataforma de captação de dízimos, ofertas e campanhas com assinatura de 0% de taxa sobre as doações.',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="pt-BR">
      <body>{children}</body>
    </html>
  );
}
