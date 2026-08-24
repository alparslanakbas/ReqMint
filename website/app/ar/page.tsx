import type { Metadata } from 'next';
import { FaApple, FaLinux, FaWindows } from 'react-icons/fa';
import { ArabicFooter, ArabicHeader } from '../components/ArabicShell';

export const metadata: Metadata = {
  title: 'ReqMint — عمل API بخفة ووضوح',
  description: 'مساحة عمل مكتبية سريعة ومحلية أولاً لإنشاء طلبات HTTP واختبارها ومشاركتها.',
  alternates: { canonical: '/ar', languages: { en: '/', ar: '/ar' } },
};

export default function ArabicHome() {
  return (
    <main className="rtl-shell" lang="ar" dir="rtl">
      <ArabicHeader active="product" />

      <section className="hero arabic-hero" id="top">
        <div className="hero-glow" aria-hidden="true" />
        <div className="hero-copy">
          <div className="eyebrow"><span className="status-dot" aria-hidden="true" />طريقة أخف للعمل مع واجهات API</div>
          <h1>عمل API،<br /><span>من دون عبء.</span></h1>
          <p className="hero-lede">
            مساحة عمل مكتبية سريعة ومحلية أولاً لإنشاء طلبات HTTP واختبارها ومشاركتها، من دون حساب إلزامي أو مساحة عمل سحابية ثقيلة.
          </p>
          <div className="hero-actions">
            <a className="button button-primary" href="/ar/downloads">اختر نظامك <span aria-hidden="true">←</span></a>
            <a className="button button-secondary" href="https://github.com/alparslanakbas/ReqMint">عرض المشروع على GitHub <span aria-hidden="true">↗</span></a>
          </div>
          <ul className="trust-list" aria-label="مبادئ المنتج">
            <li>لا يحتاج إلى حساب</li><li>محلي أولاً</li><li>مصمم للعمل مع Git</li>
          </ul>
          <div className="hero-platforms" aria-label="أنظمة سطح المكتب المدعومة">
            <span>متوفر لـ</span>
            <a href="/ar/downloads" aria-label="ReqMint لنظام Windows"><FaWindows aria-hidden="true" /><b>Windows</b></a>
            <a href="/ar/downloads" aria-label="ReqMint لنظام macOS"><FaApple aria-hidden="true" /><b>macOS</b></a>
            <a href="/ar/downloads" aria-label="ReqMint لنظام Linux"><FaLinux aria-hidden="true" /><b>Linux</b></a>
          </div>
        </div>

        <div className="arabic-product-card" id="product" aria-label="نظرة على مساحة عمل ReqMint">
          <span className="section-kicker">مساحتك هي مصدر الحقيقة</span>
          <h2>أنشئ الطلب محلياً، وراجعه بوضوح، وشاركه بأمان.</h2>
          <div className="arabic-product-points" id="principles">
            <article><b>01</b><h3>سريع افتراضياً</h3><p>مسار مكتبي مركز وسجل محلي محدود.</p></article>
            <article><b>02</b><h3>ملفات يمكنك الوثوق بها</h3><p>مستندات واضحة مصممة للمراجعة والعمل الجماعي.</p></article>
            <article><b>03</b><h3>خصوصية في التصميم</h3><p>لا حساب إجباري ولا قياس عن بعد للمنتج.</p></article>
          </div>
        </div>
      </section>
      <ArabicFooter />
    </main>
  );
}
