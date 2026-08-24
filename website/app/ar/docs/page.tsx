import type { Metadata } from 'next';
import { ArabicFooter, ArabicHeader } from '../../components/ArabicShell';
import { arabicGuides } from '../../lib/docs-ar';

export const metadata: Metadata = {
  title: 'الوثائق — ReqMint',
  description: 'تعلم إنشاء الطلبات واستخدام البيئات وتشغيل المجموعات والتعاون باستخدام Git والتحكم في بيانات ReqMint المحلية.',
  alternates: { canonical: '/ar/docs', languages: { en: '/docs', ar: '/ar/docs' } },
};

const categories = [...new Set(arabicGuides.map((guide) => guide.category))];
const categoryId = (category: string) => `category-${categories.indexOf(category) + 1}`;

export default function ArabicDocsHome() {
  return (
    <main className="docs-shell rtl-shell" lang="ar" dir="rtl">
      <ArabicHeader active="docs" />
      <section className="docs-hero">
        <div className="eyebrow"><span className="status-dot" aria-hidden="true" />وثائق ReqMint</div>
        <h1>تعلّم مسار العمل.<br /><span>واحتفظ بالتحكم.</span></h1>
        <p>أدلة واضحة تركز على المهمة لكل خيار مهم، من طلبك الأول إلى التعاون الآمن باستخدام Git.</p>
        <a className="button button-primary" href="/ar/docs/quick-start">ابدأ بطلبك الأول <span aria-hidden="true">←</span></a>
      </section>
      <div className="docs-layout">
        <aside className="docs-sidebar" aria-label="فئات الوثائق">
          <span className="sidebar-title">في هذه الصفحة</span>
          {categories.map((category) => <a href={`#${categoryId(category)}`} key={category}>{category}</a>)}
          <span className="sidebar-title resources">الموارد</span>
          <a href="https://github.com/alparslanakbas/ReqMint/issues">الإبلاغ عن مشكلة ↗</a>
          <a href="https://github.com/alparslanakbas/ReqMint">الشفرة المصدرية ↗</a>
        </aside>
        <div className="guide-groups">
          {categories.map((category) => (
            <section className="guide-group" id={categoryId(category)} key={category}>
              <div className="group-heading"><span>{category}</span><small>{arabicGuides.filter((guide) => guide.category === category).length} أدلة</small></div>
              <div className="guide-list">
                {arabicGuides.filter((guide) => guide.category === category).map((guide) => (
                  <a className="guide-card" href={`/ar/docs/${guide.slug}`} key={guide.slug}>
                    <div><h2>{guide.title}</h2><p>{guide.summary}</p></div><span>{guide.readTime} <b aria-hidden="true">←</b></span>
                  </a>
                ))}
              </div>
            </section>
          ))}
        </div>
      </div>
      <ArabicFooter message="وثائق المعاينة العامة." />
    </main>
  );
}
