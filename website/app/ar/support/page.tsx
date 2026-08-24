import type { Metadata } from 'next';
import { ArabicTrustPage } from '../../components/ArabicShell';

export const metadata: Metadata = {
  title: 'الدعم — ReqMint', description: 'اعثر على أدلة ReqMint وأبلغ عن خطأ واقترح ميزة أو أرسل ثغرة بشكل خاص.',
  alternates: { canonical: '/ar/support', languages: { en: '/support', ar: '/ar/support' } },
};

const supportPaths = [
  { number: '01', title: 'تعلّم مسار عمل', copy: 'اتبع أدلة تركز على الطلبات والبيئات وتشغيل المجموعات وGit ووضع الخلفية والسمات واللغة.', label: 'تصفح الوثائق', href: '/ar/docs' },
  { number: '02', title: 'أبلغ عن خطأ قابل للتكرار', copy: 'شارك إصدار ReqMint ونظام التشغيل والسلوك المتوقع والفعلي وأقل خطوات آمنة لتكرار المشكلة.', label: 'فتح مشكلة GitHub', href: 'https://github.com/alparslanakbas/ReqMint/issues/new' },
  { number: '03', title: 'اقترح تحسيناً', copy: 'اشرح مسار عمل المطور والمشكلة التي واجهتها والنتيجة الناجحة التي تتوقعها.', label: 'طلب ميزة', href: 'https://github.com/alparslanakbas/ReqMint/issues/new' },
];

export default function ArabicSupportPage() {
  return (
    <ArabicTrustPage eyebrow="دعم المعاينة العامة" title="ابدأ بمسار" accent="الدعم الصحيح." summary="تغطي الوثائق مسارات العمل الشائعة، وتبقي مشكلات GitHub الأخطاء والأفكار العادية شفافة، بينما تظل بلاغات الأمان الحساسة خاصة.">
      <div className="support-grid">{supportPaths.map((path) => <section key={path.number}><span className="section-number">{path.number}</span><h2>{path.title}</h2><p>{path.copy}</p><a className="text-link" href={path.href}>{path.label} <span aria-hidden="true">←</span></a></section>)}</div>
      <section className="support-safety" id="safe-reporting"><span className="section-number">04</span><h2>اجعل البلاغ آمناً للمشاركة</h2><p>احذف رؤوس التفويض وملفات تعريف الارتباط ومفاتيح API وكلمات المرور والعناوين الخاصة ومحتوى الطلبات والاستجابات وبيانات العملاء وأسرار المستودع قبل إرفاق الصور أو السجلات أو العينات.</p><aside className="doc-note"><b>ثغرة محتملة؟</b><p>لا تفتح مشكلة عامة. استخدم <a href="/ar/security">صفحة الأمان</a> ونموذج GitHub الخاص.</p></aside></section>
    </ArabicTrustPage>
  );
}
