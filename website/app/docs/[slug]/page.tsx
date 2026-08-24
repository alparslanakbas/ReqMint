import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { guideBySlug, guides } from '../../lib/docs';

export function generateStaticParams() {
  return guides.map((guide) => ({ slug: guide.slug }));
}

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const guide = guideBySlug(slug);
  return guide ? { title: `${guide.title} — ReqMint Docs`, description: guide.summary } : {};
}

export default async function GuidePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const guide = guideBySlug(slug);
  if (!guide) notFound();

  const currentIndex = guides.findIndex((item) => item.slug === guide.slug);
  const previous = guides[currentIndex - 1];
  const next = guides[currentIndex + 1];

  return (
    <main className="docs-shell">
      <header className="site-header docs-header">
        <a className="brand" href="/" aria-label="ReqMint home"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a>
        <nav className="desktop-nav" aria-label="Primary navigation"><a href="/">Product</a><a href="/downloads">Downloads</a><a className="nav-active" href="/docs">Docs</a><a href="https://github.com/alparslanakbas/ReqMint">GitHub</a></nav>
        <div className="header-actions"><a className="language-link" href={`/ar/docs/${slug}`} lang="ar" dir="rtl">العربية</a><a className="header-cta" href="/docs">All guides <span aria-hidden="true">←</span></a></div>
      </header>

      <div className="article-layout">
        <aside className="article-sidebar">
          <a className="back-link" href="/docs">← Documentation</a>
          {guides.map((item) => <a className={item.slug === guide.slug ? 'active' : ''} href={`/docs/${item.slug}`} key={item.slug}><small>{item.category}</small>{item.title}</a>)}
        </aside>

        <article className="doc-article">
          <div className="article-kicker"><span>{guide.category}</span><span>{guide.readTime} read</span></div>
          <h1>{guide.title}</h1>
          <p className="article-summary">{guide.summary}</p>
          <div className="article-rule" />
          {guide.sections.map((section, index) => (
            <section key={section.heading}>
              <span className="section-number">{String(index + 1).padStart(2, '0')}</span>
              <h2>{section.heading}</h2>
              {section.paragraphs?.map((paragraph) => <p key={paragraph}>{paragraph}</p>)}
              {section.steps && <ol>{section.steps.map((step) => <li key={step}>{step}</li>)}</ol>}
              {section.note && <aside className="doc-note"><b>Good to know</b><p>{section.note}</p></aside>}
            </section>
          ))}
          <nav className="article-pagination" aria-label="Guide pagination">
            {previous ? <a href={`/docs/${previous.slug}`}><small>Previous</small><span>← {previous.title}</span></a> : <span />}
            {next && <a className="next" href={`/docs/${next.slug}`}><small>Next</small><span>{next.title} →</span></a>}
          </nav>
        </article>
      </div>
      <footer className="inner-footer"><a className="brand" href="/"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a><p>Documentation for the public preview.</p><div><a href="/privacy">Privacy</a><a href="/security">Security</a><a href="/support">Support</a></div></footer>
    </main>
  );
}
