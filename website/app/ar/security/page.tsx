import type { Metadata } from 'next';
import { ArabicTrustPage } from '../../components/ArabicShell';

export const metadata: Metadata = {
  title: 'الأمان — ReqMint', description: 'نطاق أمان ReqMint وضمانات الإصدار والإبلاغ الخاص عن الثغرات.',
  alternates: { canonical: '/ar/security', languages: { en: '/security', ar: '/ar/security' } },
};

export default function ArabicSecurityPage() {
  return (
    <ArabicTrustPage eyebrow="أمان مسؤول" title="أبلغ بشكل خاص." accent="احمِ المستخدمين أولاً." summary="يجب إرسال الثغرات المحتملة عبر قناة GitHub الخاصة، وليس ضمن مشكلة عامة أو سجل أو صورة أو ملف مساحة عمل.">
      <section id="report"><span className="section-number">01</span><h2>أبلغ عن الثغرة بشكل خاص</h2><p>استخدم نموذج GitHub الخاص. أرفق الإصدار أو الالتزام المتأثر ونظام التشغيل وخطوات التكرار والأثر وأي إجراء مقترح.</p><a className="button button-primary trust-action" href="https://github.com/alparslanakbas/ReqMint/security/advisories/new">فتح بلاغ خاص <span aria-hidden="true">↗</span></a><aside className="doc-note warning"><b>لا تنشر الأسرار</b><p>لا تضع بيانات الاعتماد أو الرموز أو المحتوى الخاص أو تفاصيل الاستغلال أو مواد التوقيع أو بيانات المستخدم في مشكلة عامة.</p></aside></section>
      <section id="scope"><span className="section-number">02</span><h2>النطاق عالي الأولوية</h2><ul><li>كشف بيانات الاعتماد أو حفظ الأسرار بطريقة غير آمنة</li><li>تحليل غير آمن لمساحات العمل أو المجموعات</li><li>تنفيذ أوامر أو شفرة</li><li>تجاوز التحقق من الشهادات</li><li>نشاط شبكة غير متوقع أو غير مصرح به</li><li>عمليات Git خارج النطاق الذي أكده المستخدم</li></ul></section>
      <section id="release"><span className="section-number">03</span><h2>ضمانات الإصدار</h2><p>يجب أن يجتاز كل مرشح إصدار اختبارات الأنظمة وفحوص التبعيات. تتطلب الحزم العامة أيضاً بنية موثقة وبصمات صحيحة وبوابات التوقيع أو التوثيق الخاصة بالنظام.</p></section>
      <section id="support"><span className="section-number">04</span><h2>الأخطاء العادية تذهب إلى الدعم</h2><p>إذا لم تشكل المشكلة خطراً أمنياً أو خطراً على الخصوصية، فاستخدم صفحة الدعم العامة.</p><a className="text-link" href="/ar/support">زيارة الدعم <span aria-hidden="true">←</span></a></section>
    </ArabicTrustPage>
  );
}
