import type { ReactNode } from 'react';

type TrustPageProps = {
  eyebrow: string;
  title: string;
  accent: string;
  summary: string;
  updated?: string;
  arabicHref?: string;
  children: ReactNode;
};

export function TrustPage({ eyebrow, title, accent, summary, updated, arabicHref = '/ar', children }: TrustPageProps) {
  return (
    <main className="trust-shell">
      <header className="site-header docs-header">
        <a className="brand" href="/" aria-label="ReqMint home"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a>
        <nav className="desktop-nav" aria-label="Primary navigation"><a href="/#product">Product</a><a href="/downloads">Downloads</a><a href="/docs">Docs</a><a href="https://github.com/alparslanakbas/ReqMint">GitHub</a></nav>
        <div className="header-actions"><a className="language-link" href={arabicHref} lang="ar" dir="rtl">العربية</a><a className="header-cta" href="/support">Get help <span aria-hidden="true">↗</span></a></div>
      </header>

      <section className="trust-hero">
        <div className="eyebrow"><span className="status-dot" aria-hidden="true" />{eyebrow}</div>
        <h1>{title}<br /><span>{accent}</span></h1>
        <p>{summary}</p>
        {updated && <small className="trust-updated">Last updated · {updated}</small>}
      </section>

      <div className="trust-layout">
        <aside className="trust-sidebar" aria-label="Trust and support pages">
          <span>Trust center</span>
          <a href="/privacy">Privacy</a>
          <a href="/security">Security</a>
          <a href="/support">Support</a>
          <a href="https://github.com/alparslanakbas/ReqMint">Source code ↗</a>
        </aside>
        <article className="trust-content">{children}</article>
      </div>

      <footer className="inner-footer"><a className="brand" href="/"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a><p>Local-first API work.</p><div><a href="/privacy">Privacy</a><a href="/security">Security</a><a href="/support">Support</a></div></footer>
    </main>
  );
}
