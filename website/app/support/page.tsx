import type { Metadata } from 'next';
import { TrustPage } from '../components/TrustPage';

export const metadata: Metadata = {
  title: 'Support — ReqMint',
  description: 'Find ReqMint guides, report a bug, request a feature, or disclose a security issue privately.',
};

const supportPaths = [
  { number: '01', title: 'Learn a workflow', copy: 'Follow task-focused guides for requests, environments, collection runs, Git collaboration, background mode, themes, and language.', label: 'Browse documentation', href: '/docs' },
  { number: '02', title: 'Report a reproducible bug', copy: 'Share the ReqMint version, operating system, expected behavior, actual behavior, and the smallest safe set of steps that reproduces the problem.', label: 'Open a GitHub issue', href: 'https://github.com/alparslanakbas/ReqMint/issues/new' },
  { number: '03', title: 'Suggest an improvement', copy: 'Explain the developer workflow, the limitation you encountered, and what a successful outcome would look like.', label: 'Request a feature', href: 'https://github.com/alparslanakbas/ReqMint/issues/new' },
];

export default function SupportPage() {
  return (
    <TrustPage
      eyebrow="Public preview support"
      title="Start with the right"
      accent="support path."
      summary="Documentation handles the common workflows. GitHub issues keep ordinary bugs and ideas transparent. Sensitive security reports always stay private."
    >
      <div className="support-grid">
        {supportPaths.map((path) => <section key={path.number}><span className="section-number">{path.number}</span><h2>{path.title}</h2><p>{path.copy}</p><a className="text-link" href={path.href}>{path.label} <span aria-hidden="true">→</span></a></section>)}
      </div>
      <section className="support-safety" id="safe-reporting"><span className="section-number">04</span><h2>Keep reports safe to share</h2><p>Remove authorization headers, cookies, API keys, passwords, private URLs, request and response bodies, customer data, and repository secrets before attaching screenshots, logs, or workspace samples.</p><aside className="doc-note"><b>Possible vulnerability?</b><p>Do not open a public issue. Use the <a href="/security">security page</a> and GitHub&apos;s private vulnerability reporting form.</p></aside></section>
    </TrustPage>
  );
}
