import type { Metadata } from 'next';
import { TrustPage } from '../components/TrustPage';

export const metadata: Metadata = {
  title: 'Privacy — ReqMint',
  description: 'How ReqMint stores local data, sends network requests, and protects sensitive API content.',
};

export default function PrivacyPage() {
  return (
    <TrustPage
      eyebrow="Privacy by design"
      title="Your API work stays"
      accent="under your control."
      summary="ReqMint is a local-first developer tool. The public preview requires no account, includes no advertising, and does not upload workspaces to a ReqMint service."
      updated="24 August 2026"
    >
      <section id="local-data"><span className="section-number">01</span><h2>Data stored on your device</h2><p>ReqMint stores settings, onboarding progress, request history, collection-run summaries, and user-selected workspaces locally. Workspace files remain in the folder you choose and can be reviewed or shared like ordinary files.</p><p>History retention is bounded. Sensitive request headers and configured secret values are redacted before history is persisted.</p></section>
      <section id="network"><span className="section-number">02</span><h2>Network activity</h2><p>ReqMint sends HTTP traffic when you run a request or collection. The local onboarding sample uses a temporary loopback service and does not require an external API.</p><p>Git network operations require an explicit action and use the remote configured in your repository. ReqMint does not provide a hosted synchronization service.</p></section>
      <section id="telemetry"><span className="section-number">03</span><h2>Accounts and telemetry</h2><p>The current public-preview code does not require a ReqMint account and does not enable product analytics or crash telemetry. Platform stores and Git hosting providers may independently process installation, update, repository, or diagnostic data under their own terms.</p></section>
      <section id="remove"><span className="section-number">04</span><h2>Review and remove local data</h2><p>You can clear local history inside ReqMint, remove workspace files through your operating system, and remove application settings through the operating system&apos;s application-data controls.</p><aside className="doc-note"><b>Privacy questions</b><p>Open a GitHub issue without including credentials, request bodies, tokens, or other private data. Security vulnerabilities should use the private reporting route.</p></aside></section>
    </TrustPage>
  );
}
