import type { Metadata } from 'next';
import { guides } from '../lib/docs';

export const metadata: Metadata = {
  title: 'Documentation — ReqMint',
  description: 'Learn how to build requests, use environments, run collections, collaborate through Git, and control local data in ReqMint.',
};

const categories = [...new Set(guides.map((guide) => guide.category))];

export default function DocsHome() {
  return (
    <main className="docs-shell">
      <header className="site-header docs-header">
        <a className="brand" href="/" aria-label="ReqMint home"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a>
        <nav className="desktop-nav" aria-label="Primary navigation"><a href="/">Product</a><a href="/downloads">Downloads</a><a className="nav-active" href="/docs">Docs</a><a href="https://github.com/alparslanakbas/ReqMint">GitHub</a></nav>
        <a className="header-cta" href="https://github.com/alparslanakbas/ReqMint/issues">Get help <span aria-hidden="true">↗</span></a>
      </header>

      <section className="docs-hero">
        <div className="eyebrow"><span className="status-dot" aria-hidden="true" />ReqMint documentation</div>
        <h1>Learn the workflow.<br /><span>Keep the control.</span></h1>
        <p>Clear, task-focused guides for every important ReqMint option—from your first request to safe Git collaboration.</p>
        <a className="button button-primary" href="/docs/quick-start">Start with your first request <span aria-hidden="true">→</span></a>
      </section>

      <div className="docs-layout">
        <aside className="docs-sidebar" aria-label="Documentation categories">
          <span className="sidebar-title">On this page</span>
          {categories.map((category) => <a href={`#${category.toLowerCase().replaceAll(' ', '-')}`} key={category}>{category}</a>)}
          <span className="sidebar-title resources">Resources</span>
          <a href="https://github.com/alparslanakbas/ReqMint/issues">Report an issue ↗</a>
          <a href="https://github.com/alparslanakbas/ReqMint">Source code ↗</a>
        </aside>

        <div className="guide-groups">
          {categories.map((category) => (
            <section className="guide-group" id={category.toLowerCase().replaceAll(' ', '-')} key={category}>
              <div className="group-heading"><span>{category}</span><small>{guides.filter((guide) => guide.category === category).length} guides</small></div>
              <div className="guide-list">
                {guides.filter((guide) => guide.category === category).map((guide) => (
                  <a className="guide-card" href={`/docs/${guide.slug}`} key={guide.slug}>
                    <div><h2>{guide.title}</h2><p>{guide.summary}</p></div>
                    <span>{guide.readTime} <b aria-hidden="true">→</b></span>
                  </a>
                ))}
              </div>
            </section>
          ))}
        </div>
      </div>

      <footer className="inner-footer"><a className="brand" href="/"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a><p>Documentation for the public preview.</p><div><a href="/downloads">Downloads</a><a href="/privacy">Privacy</a><a href="/support">Support</a></div></footer>
    </main>
  );
}
