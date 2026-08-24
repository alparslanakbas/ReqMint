import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'ReqMint — API work, without the weight',
  description: 'A fast, local-first desktop workspace for building, testing, and sharing HTTP requests.',
  metadataBase: new URL('https://reqmint.alparslanayt.chatgpt.site'),
  icons: { icon: '/reqmint-icon.png' },
  openGraph: {
    title: 'ReqMint — API work, without the weight',
    description: 'A fast, local-first desktop workspace for building, testing, and sharing HTTP requests.',
    type: 'website',
    images: [{ url: '/og.png', width: 1200, height: 630, alt: 'ReqMint — API work, without the weight.' }],
  },
  twitter: {
    card: 'summary_large_image',
    title: 'ReqMint — API work, without the weight',
    description: 'A fast, local-first desktop workspace for building, testing, and sharing HTTP requests.',
    images: ['/og.png'],
  },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body>{children}</body></html>;
}
