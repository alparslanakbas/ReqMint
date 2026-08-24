import type { ReactNode } from 'react';

export function ArabicHeader({ active }: { active?: 'product' | 'downloads' | 'docs' }) {
  return (
    <header className="site-header docs-header">
      <a className="brand" href="/ar" aria-label="الصفحة الرئيسية لـ ReqMint"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a>
      <nav className="desktop-nav" aria-label="التنقل الرئيسي">
        <a className={active === 'product' ? 'nav-active' : ''} href="/ar">المنتج</a>
        <a className={active === 'downloads' ? 'nav-active' : ''} href="/ar/downloads">التنزيلات</a>
        <a className={active === 'docs' ? 'nav-active' : ''} href="/ar/docs">الوثائق</a>
        <a href="https://github.com/alparslanakbas/ReqMint">GitHub</a>
      </nav>
      <div className="header-actions">
        <a className="language-link" href="/" lang="en" dir="ltr">English</a>
        <a className="header-cta" href="/ar/support">الحصول على مساعدة <span aria-hidden="true">↗</span></a>
      </div>
    </header>
  );
}

export function ArabicFooter({ message = 'عمل API محلي أولاً.' }: { message?: string }) {
  return (
    <footer className="inner-footer">
      <a className="brand" href="/ar"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a>
      <p>{message}</p>
      <div><a href="/ar/docs">الوثائق</a><a href="/ar/privacy">الخصوصية</a><a href="/ar/security">الأمان</a><a href="/ar/support">الدعم</a></div>
    </footer>
  );
}

type ArabicTrustPageProps = {
  eyebrow: string;
  title: string;
  accent: string;
  summary: string;
  updated?: string;
  children: ReactNode;
};

export function ArabicTrustPage({ eyebrow, title, accent, summary, updated, children }: ArabicTrustPageProps) {
  return (
    <main className="trust-shell rtl-shell" lang="ar" dir="rtl">
      <ArabicHeader />
      <section className="trust-hero">
        <div className="eyebrow"><span className="status-dot" aria-hidden="true" />{eyebrow}</div>
        <h1>{title}<br /><span>{accent}</span></h1>
        <p>{summary}</p>
        {updated && <small className="trust-updated">آخر تحديث · {updated}</small>}
      </section>
      <div className="trust-layout">
        <aside className="trust-sidebar" aria-label="صفحات الثقة والدعم">
          <span>مركز الثقة</span>
          <a href="/ar/privacy">الخصوصية</a>
          <a href="/ar/security">الأمان</a>
          <a href="/ar/support">الدعم</a>
          <a href="https://github.com/alparslanakbas/ReqMint">الشفرة المصدرية ↗</a>
        </aside>
        <article className="trust-content">{children}</article>
      </div>
      <ArabicFooter />
    </main>
  );
}
