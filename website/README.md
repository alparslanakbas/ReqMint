# ReqMint website

The ReqMint website is a presentation, platform-download, and documentation surface. It does not process payments, create ReqMint accounts, or collect newsletter addresses.

## Routes

- `/` — product presentation
- `/downloads` — trusted platform channels and release status
- `/docs` — task-focused documentation index
- `/docs/:slug` — detailed guides

Until the public release gates pass, download controls remain visibly unavailable rather than linking to unsigned or temporary workflow artifacts. Microsoft Store and other platform links are activated only after their final listings exist.

## Local validation

Install the pinned dependencies, run the production build, and complete the dependency audit before publishing. Social metadata uses `public/og.png`; the site-wide icon uses the same ReqMint mark as the desktop application.
