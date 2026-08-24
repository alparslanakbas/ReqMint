import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { ArabicFooter, ArabicHeader } from '../../../components/ArabicShell';
import { arabicGuideBySlug, arabicGuides } from '../../../lib/docs-ar';

export function generateStaticParams() {
  return arabicGuides.map((guide) => ({ slug: guide.slug }));
}

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const guide = arabicGuideBySlug(slug);
  return guide ? { title: `${guide.title} — وثائق ReqMint`, description: guide.summary, alternates: { canonical: `/ar/docs/${slug}`, languages: { en: `/docs/${slug}`, ar: `/ar/docs/${slug}` } } } : {};
}

export default async function ArabicGuidePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const guide = arabicGuideBySlug(slug);
  if (!guide) notFound();
  const currentIndex = arabicGuides.findIndex((item) => item.slug === guide.slug);
  const previous = arabicGuides[currentIndex - 1];
  const next = arabicGuides[currentIndex + 1];

  return (
    <main className="docs-shell rtl-shell" lang="ar" dir="rtl">
      <ArabicHeader active="docs" />
      <div className="article-layout">
        <aside className="article-sidebar">
          <a className="back-link" href="/ar/docs">← جميع الوثائق</a>
          {arabicGuides.map((item) => <a className={item.slug === guide.slug ? 'active' : ''} href={`/ar/docs/${item.slug}`} key={item.slug}><small>{item.category}</small>{item.title}</a>)}
        </aside>
        <article className="doc-article">
          <div className="article-kicker"><span>{guide.category}</span><span>قراءة {guide.readTime}</span></div>
          <h1>{guide.title}</h1><p className="article-summary">{guide.summary}</p><div className="article-rule" />
          {guide.sections.map((section, index) => (
            <section key={section.heading}>
              <span className="section-number">{String(index + 1).padStart(2, '0')}</span><h2>{section.heading}</h2>
              {section.paragraphs?.map((paragraph) => <p key={paragraph}>{paragraph}</p>)}
              {section.steps && <ol>{section.steps.map((step) => <li key={step}>{step}</li>)}</ol>}
              {section.note && <aside className="doc-note"><b>من المفيد أن تعرف</b><p>{section.note}</p></aside>}
            </section>
          ))}
          <nav className="article-pagination" aria-label="التنقل بين الأدلة">
            {previous ? <a href={`/ar/docs/${previous.slug}`}><small>السابق</small><span>→ {previous.title}</span></a> : <span />}
            {next && <a className="next" href={`/ar/docs/${next.slug}`}><small>التالي</small><span>{next.title} ←</span></a>}
          </nav>
        </article>
      </div>
      <ArabicFooter message="وثائق المعاينة العامة." />
    </main>
  );
}
