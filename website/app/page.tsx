import { FaApple, FaLinux, FaWindows } from 'react-icons/fa';

function BrandMark() {
  return <span className="brand-mark" aria-hidden="true">R</span>;
}

export default function Home() {
  return (
    <main>
      <header className="site-header">
        <a className="brand" href="#top" aria-label="ReqMint home">
          <BrandMark /><span>ReqMint</span>
        </a>
        <nav className="desktop-nav" aria-label="Primary navigation">
          <a href="#product">Product</a>
          <a href="/downloads">Downloads</a>
          <a href="/docs">Docs</a>
          <a href="https://github.com/alparslanakbas/ReqMint">GitHub</a>
        </nav>
        <div className="header-actions">
          <a className="language-link" href="/ar" lang="ar" dir="rtl">العربية</a>
          <a className="header-cta" href="/docs">Read the docs <span aria-hidden="true">↗</span></a>
        </div>
      </header>

      <section className="hero hero-showcase" id="top">
        <div className="hero-glow" aria-hidden="true" />
        <div className="hero-copy">
          <div className="eyebrow"><span className="status-dot" aria-hidden="true" />A lighter way to work with APIs</div>
          <h1>API work,<br /><span>without the weight.</span></h1>
          <p className="hero-lede">
            A fast, local-first desktop workspace for building, testing, and sharing HTTP requests—without a required account or a heavy cloud workspace.
          </p>
          <div className="hero-actions">
            <a className="button button-primary" href="/downloads">Choose your platform <span aria-hidden="true">→</span></a>
            <a className="button button-secondary" href="https://github.com/alparslanakbas/ReqMint">View on GitHub <span aria-hidden="true">↗</span></a>
          </div>
          <ul className="trust-list" aria-label="Product principles">
            <li>No account required</li><li>Local-first</li><li>Git-native</li>
          </ul>
          <div className="hero-platforms" aria-label="Supported desktop platforms">
            <span>Built for</span>
            <a href="/downloads" aria-label="ReqMint for Windows"><FaWindows aria-hidden="true" /><b>Windows</b></a>
            <a href="/downloads" aria-label="ReqMint for macOS"><FaApple aria-hidden="true" /><b>macOS</b></a>
            <a href="/downloads" aria-label="ReqMint for Linux"><FaLinux aria-hidden="true" /><b>Linux</b></a>
          </div>
        </div>

        <div className="product-stage" aria-label="ReqMint request workspace preview">
          <div className="stage-orbit orbit-one" aria-hidden="true" />
          <div className="stage-orbit orbit-two" aria-hidden="true" />
          <div className="app-window">
            <div className="app-titlebar">
              <div className="app-brand"><BrandMark /><span>ReqMint</span></div>
              <div className="window-actions" aria-hidden="true"><span /><span /><span /></div>
            </div>
            <div className="app-layout">
              <aside className="app-sidebar" aria-hidden="true">
                <span className="sidebar-label">Workspace</span>
                <span className="sidebar-item active">Orders API</span>
                <span className="sidebar-item">Authentication</span>
                <span className="sidebar-item">Health check</span>
                <span className="sidebar-label second">Environments</span>
                <span className="sidebar-item">Local</span>
              </aside>
              <div className="request-panel">
                <div className="request-tabs"><span className="request-tab active">Request</span><span className="request-tab">Runner</span><span className="request-tab">History</span></div>
                <div className="url-row"><span className="method">GET</span><span className="url">{'{{baseUrl}}'}/orders/42</span><span className="send">Send</span></div>
                <div className="request-meta"><span className="active">Body</span><span>Headers <b>3</b></span><span>Assertions <b>2</b></span></div>
                <div className="response-head"><div><span className="response-label">Response</span><span className="response-time">148 ms</span></div><span className="status-code">200 OK</span></div>
                <pre className="response-code" aria-label="Example JSON response"><code>{`{
  "id": 42,
  "status": "ready",
  "customer": {
    "name": "Ada Lovelace"
  }
}`}</code></pre>
              </div>
            </div>
          </div>
          <div className="stage-badge badge-local"><span aria-hidden="true">⌁</span><div><b>Local-first</b><small>Your data stays yours</small></div></div>
          <div className="stage-badge badge-git"><span aria-hidden="true">⑂</span><div><b>Git-ready</b><small>Reviewable by design</small></div></div>
        </div>
      </section>

      <section className="feature-story" aria-labelledby="feature-story-heading">
        <div className="feature-copy">
          <span className="section-kicker">Your workspace is the source of truth</span>
          <h2 id="feature-story-heading">From first request to team review—without changing tools.</h2>
          <p>Build the request visually, save it as a readable workspace document, and review the exact change before it reaches your repository.</p>
          <ul>
            <li><b>Build quickly</b><span>Requests, environments, assertions, and local history in one focused desktop flow.</span></li>
            <li><b>Review clearly</b><span>Stable files and an exact Git diff keep changes understandable.</span></li>
            <li><b>Publish safely</b><span>Managed-path limits and secret checks fail closed before supported Git operations.</span></li>
          </ul>
          <a className="text-link" href="/docs/git-collaboration">Explore Git collaboration <span aria-hidden="true">→</span></a>
        </div>
        <div className="git-showcase" aria-label="Example ReqMint Git review">
          <div className="git-toolbar"><span><i aria-hidden="true">⑂</i> Review workspace changes</span><b>3 files ready</b></div>
          <div className="git-files"><span className="active">collections/orders.json <b>+8</b></span><span>environments/local.json <b>+2</b></span><span>workspace.json <b>+1</b></span></div>
          <pre><code>{`@@ request: Get order @@
 "method": "GET",
 "url": "{{baseUrl}}/orders/{{orderId}}",
 "assertions": [
   { "statusCode": 200 },
   { "maxDurationMs": 800 }
 ]`}</code></pre>
          <div className="git-check"><span aria-hidden="true">✓</span><div><b>Security check passed</b><small>No likely secrets in managed files</small></div></div>
        </div>
      </section>

      <section className="principles" id="product" aria-labelledby="principles-heading">
        <div className="principles-heading"><span className="section-kicker">A calmer API workflow</span><h2 id="principles-heading">The essentials are the product.</h2><p>ReqMint focuses on the daily request loop instead of pulling your work into another account, dashboard, or proprietary cloud.</p></div>
        <div className="principle-grid">
          <article><span>01</span><h3>Fast by default</h3><p>A native desktop rhythm with a focused interface and bounded local history.</p></article>
          <article><span>02</span><h3>Files you can trust</h3><p>Readable workspace documents designed for review, version control, and teams.</p></article>
          <article><span>03</span><h3>Private by design</h3><p>No forced account, no product telemetry, and no ReqMint-hosted workspace sync.</p></article>
        </div>
      </section>

      <section className="docs-promo">
        <div><span className="section-kicker">Documentation that starts with a task</span><h2>Know what every option does.</h2><p>Follow focused guides for requests, environments, collection runs, Git safety, privacy, background behavior, themes, and language.</p></div>
        <div className="docs-promo-links"><a href="/docs/quick-start"><span>01</span><b>Send your first request</b><small>4 min guide →</small></a><a href="/docs/collection-runner"><span>02</span><b>Run collections and assertions</b><small>8 min guide →</small></a><a href="/docs/git-collaboration"><span>03</span><b>Collaborate through Git</b><small>9 min guide →</small></a></div>
      </section>

      <footer>
        <a className="brand" href="#top"><BrandMark /><span>ReqMint</span></a>
        <p>API work, without the weight.</p>
        <div><a href="/docs">Documentation</a><a href="/privacy">Privacy</a><a href="/security">Security</a><a href="/support">Support</a></div>
      </footer>
    </main>
  );
}
