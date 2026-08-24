import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Downloads — ReqMint',
  description: 'Choose the ReqMint desktop package for Windows, macOS, or Linux.',
};

const downloads = [
  { icon: 'W', platform: 'Windows', detail: 'Windows 10 and 11 · x64 and ARM64', channel: 'Microsoft Store', note: 'Store-signed installation and automatic updates.' },
  { icon: 'M', platform: 'macOS', detail: 'macOS 14+ · Intel and Apple Silicon', channel: 'Signed desktop app', note: 'Developer ID signing and Apple notarization are release gates.' },
  { icon: 'L', platform: 'Linux', detail: 'x64 and ARM64 portable archives', channel: 'Verified download', note: 'Self-contained archive with a matching SHA-256 checksum.' },
];

export default function DownloadsPage() {
  return (
    <main className="downloads-shell">
      <header className="site-header docs-header">
        <a className="brand" href="/" aria-label="ReqMint home"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a>
        <nav className="desktop-nav" aria-label="Primary navigation"><a href="/">Product</a><a className="nav-active" href="/downloads">Downloads</a><a href="/docs">Docs</a><a href="https://github.com/alparslanakbas/ReqMint">GitHub</a></nav>
        <a className="header-cta" href="/docs/quick-start">Quick start <span aria-hidden="true">↗</span></a>
      </header>

      <section className="downloads-hero">
        <div className="eyebrow"><span className="status-dot" aria-hidden="true" />Cross-platform desktop app</div>
        <h1>ReqMint for<br /><span>your system.</span></h1>
        <p>Every public package will be built from the same tested source, verified for its target architecture, and distributed through its trusted platform channel.</p>
        <div className="preview-notice"><span aria-hidden="true">i</span><div><b>Public downloads are not open yet.</b><small>The buttons will activate only after signing, platform checks, and release-candidate approval are complete.</small></div></div>
      </section>

      <section className="download-grid" aria-label="ReqMint download options">
        {downloads.map((download) => (
          <article key={download.platform}>
            <div className="download-card-head"><span>{download.icon}</span><small>{download.channel}</small></div>
            <h2>{download.platform}</h2>
            <p>{download.detail}</p>
            <button type="button" disabled>{download.platform === 'Windows' ? 'Get it from Microsoft' : `Download for ${download.platform}`}<span aria-hidden="true">→</span></button>
            <small className="download-note">{download.note}</small>
          </article>
        ))}
      </section>

      <section className="download-integrity"><div><span className="section-kicker">Release integrity</span><h2>Trust the package, not just the page.</h2></div><div className="integrity-list"><p><b>01 · Tested</b><span>Release tests run on Windows, macOS, and Linux.</span></p><p><b>02 · Verified</b><span>Architectures, metadata, package structure, and checksums are checked automatically.</span></p><p><b>03 · Signed</b><span>Public Windows and macOS packages must pass their platform trust gates.</span></p></div></section>

      <footer className="inner-footer"><a className="brand" href="/"><span className="brand-mark" aria-hidden="true">R</span><span>ReqMint</span></a><p>Public preview coming soon.</p><div><a href="/docs">Documentation</a><a href="https://github.com/alparslanakbas/ReqMint">GitHub</a></div></footer>
    </main>
  );
}
