import type { Metadata } from 'next';
import { ArabicTrustPage } from '../../components/ArabicShell';

export const metadata: Metadata = {
  title: 'الخصوصية — ReqMint', description: 'كيف يخزن ReqMint البيانات المحلية ويرسل طلبات الشبكة ويحمي محتوى API الحساس.',
  alternates: { canonical: '/ar/privacy', languages: { en: '/privacy', ar: '/ar/privacy' } },
};

export default function ArabicPrivacyPage() {
  return (
    <ArabicTrustPage eyebrow="الخصوصية في التصميم" title="يبقى عمل API" accent="تحت سيطرتك." summary="ReqMint أداة محلية أولاً. لا تتطلب المعاينة العامة حساباً ولا تعرض إعلانات ولا ترفع مساحات العمل إلى خدمة ReqMint." updated="24 أغسطس 2026">
      <section id="local-data"><span className="section-number">01</span><h2>البيانات المخزنة على جهازك</h2><p>يخزن ReqMint الإعدادات وتقدم البدء وسجل الطلبات وملخصات تشغيل المجموعات ومساحات العمل التي تختارها محلياً. تبقى ملفات مساحة العمل في المجلد الذي تحدده ويمكن مراجعتها أو مشاركتها كملفات عادية.</p><p>مدة الاحتفاظ بالسجل محدودة، وتُنقح رؤوس الطلبات الحساسة وقيم الأسرار المهيأة قبل حفظ السجل.</p></section>
      <section id="network"><span className="section-number">02</span><h2>نشاط الشبكة</h2><p>يرسل ReqMint حركة HTTP عندما تشغّل طلباً أو مجموعة. تستخدم العينة التعليمية خدمة محلية مؤقتة ولا تحتاج واجهة API خارجية.</p><p>تتطلب عمليات Git الشبكية إجراءً صريحاً وتستخدم المستودع البعيد الذي أعددته. لا يقدم ReqMint خدمة مزامنة مستضافة.</p></section>
      <section id="telemetry"><span className="section-number">03</span><h2>الحسابات والقياس عن بعد</h2><p>لا تتطلب شفرة المعاينة العامة حساب ReqMint ولا تفعّل تحليلات استخدام المنتج أو القياس عن بعد للأعطال. قد تعالج متاجر الأنظمة ومزودات Git بياناتها وفق شروطها.</p></section>
      <section id="remove"><span className="section-number">04</span><h2>راجع البيانات المحلية وأزلها</h2><p>يمكنك مسح السجل من داخل ReqMint وحذف ملفات مساحة العمل عبر نظام التشغيل وإزالة إعدادات التطبيق من عناصر التحكم في بيانات التطبيقات.</p><aside className="doc-note"><b>أسئلة الخصوصية</b><p>افتح مشكلة في GitHub دون تضمين بيانات اعتماد أو رموز أو محتوى خاص. استخدم مسار الإبلاغ الخاص للثغرات.</p></aside></section>
    </ArabicTrustPage>
  );
}
