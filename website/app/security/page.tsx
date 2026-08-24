import type { Metadata } from 'next';
import { TrustPage } from '../components/TrustPage';

export const metadata: Metadata = {
  title: 'Security — ReqMint',
  description: 'ReqMint security scope, release safeguards, and private vulnerability reporting.',
};

export default function SecurityPage() {
  return (
    <TrustPage
      eyebrow="Responsible security"
      title="Report privately."
      accent="Protect users first."
      summary="Potential vulnerabilities belong in ReqMint’s private GitHub reporting channel—not in public issues, logs, screenshots, or workspace files."
      arabicHref="/ar/security"
    >
      <section id="report"><span className="section-number">01</span><h2>Report a vulnerability privately</h2><p>Use GitHub&apos;s private vulnerability reporting form. Include the affected version or commit, operating system, reproducible steps, impact, and any suggested mitigation.</p><a className="button button-primary trust-action" href="https://github.com/alparslanakbas/ReqMint/security/advisories/new">Open private report <span aria-hidden="true">↗</span></a><aside className="doc-note warning"><b>Never publish secrets</b><p>Do not place credentials, tokens, private request or response content, exploit details, signing material, or user data in a public issue.</p></aside></section>
      <section id="scope"><span className="section-number">02</span><h2>High-priority scope</h2><ul><li>Credential disclosure or unsafe secret persistence</li><li>Unsafe workspace or collection parsing</li><li>Command or code execution</li><li>Certificate-verification bypass</li><li>Unexpected or unauthorized network activity</li><li>Git operations outside the user-confirmed scope</li></ul></section>
      <section id="release"><span className="section-number">03</span><h2>Release safeguards</h2><p>Every release candidate must pass cross-platform tests and dependency checks. Public packages additionally require verified structure, checksums, and the relevant platform signing or notarization gates.</p><p>Security, credential protection, signing, notarization, and store-certification gates cannot be waived for a public package.</p></section>
      <section id="support"><span className="section-number">04</span><h2>Ordinary bugs belong in support</h2><p>If the issue does not create a security or privacy risk, use the public support page so expected behavior, logs, and reproducible steps can be discussed openly.</p><a className="text-link" href="/support">Visit support <span aria-hidden="true">→</span></a></section>
    </TrustPage>
  );
}
