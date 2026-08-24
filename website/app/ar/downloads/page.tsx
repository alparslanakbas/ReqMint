import type { Metadata } from 'next';
import { ArabicFooter, ArabicHeader } from '../../components/ArabicShell';

export const metadata: Metadata = {
  title: 'التنزيلات — ReqMint',
  description: 'اختر حزمة ReqMint المكتبية لنظام Windows أو macOS أو Linux.',
  alternates: { canonical: '/ar/downloads', languages: { en: '/downloads', ar: '/ar/downloads' } },
};

const downloads = [
  { icon: 'W', platform: 'Windows', detail: 'Windows 10 و11 · x64 وARM64', channel: 'Microsoft Store', note: 'تثبيت موقّع من المتجر وتحديثات تلقائية.' },
  { icon: 'M', platform: 'macOS', detail: 'macOS 14+ · Intel وApple Silicon', channel: 'تطبيق مكتبي موقّع', note: 'توقيع Developer ID وتوثيق Apple شرطان للإصدار.' },
  { icon: 'L', platform: 'Linux', detail: 'أرشيفات محمولة لمعمارية x64 وARM64', channel: 'تنزيل موثّق', note: 'أرشيف مكتفٍ ذاتياً مع بصمة SHA-256 مطابقة.' },
];

export default function ArabicDownloadsPage() {
  return (
    <main className="downloads-shell rtl-shell" lang="ar" dir="rtl">
      <ArabicHeader active="downloads" />
      <section className="downloads-hero">
        <div className="eyebrow"><span className="status-dot" aria-hidden="true" />تطبيق مكتبي متعدد الأنظمة</div>
        <h1>ReqMint<br /><span>لنظامك.</span></h1>
        <p>ستُبنى كل حزمة عامة من الشفرة المختبرة نفسها، وتُراجع للمعمارية المستهدفة، وتوزع عبر القناة الموثوقة لنظامها.</p>
        <div className="preview-notice"><span aria-hidden="true">i</span><div><b>التنزيلات العامة غير متاحة بعد.</b><small>ستُفعّل الأزرار بعد اكتمال التوقيع وفحوص الأنظمة واعتماد نسخة الإصدار.</small></div></div>
      </section>
      <section className="download-grid" aria-label="خيارات تنزيل ReqMint">
        {downloads.map((download) => (
          <article key={download.platform}>
            <div className="download-card-head"><span>{download.icon}</span><small>{download.channel}</small></div>
            <h2>{download.platform}</h2><p>{download.detail}</p>
            <button type="button" disabled>{download.platform === 'Windows' ? 'احصل عليه من Microsoft' : `تنزيل ${download.platform}`}<span aria-hidden="true">←</span></button>
            <small className="download-note">{download.note}</small>
          </article>
        ))}
      </section>
      <section className="download-integrity"><div><span className="section-kicker">سلامة الإصدار</span><h2>ثق بالحزمة، لا بالصفحة فقط.</h2></div><div className="integrity-list"><p><b>01 · مختبرة</b><span>تعمل اختبارات الإصدار على Windows وmacOS وLinux.</span></p><p><b>02 · موثّقة</b><span>تُفحص المعماريات والبيانات الوصفية وبنية الحزمة والبصمات آلياً.</span></p><p><b>03 · موقّعة</b><span>يجب أن تجتاز حزم Windows وmacOS العامة متطلبات الثقة الخاصة بنظامها.</span></p></div></section>
      <ArabicFooter message="المعاينة العامة قريباً." />
    </main>
  );
}
