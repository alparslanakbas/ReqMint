export type GuideSection = {
  heading: string;
  paragraphs?: string[];
  steps?: string[];
  note?: string;
};

export type Guide = {
  slug: string;
  category: string;
  title: string;
  summary: string;
  readTime: string;
  sections: GuideSection[];
};

export const guides: Guide[] = [
  {
    slug: 'quick-start',
    category: 'Getting started',
    title: 'Send your first request',
    summary: 'Open ReqMint, create a local workspace, and inspect a real response.',
    readTime: '4 min',
    sections: [
      { heading: 'Start with the guided sample', paragraphs: ['On first launch, ReqMint offers a private, disposable tutorial workspace. It uses a loopback API running only on your device, so you can learn the request flow without contacting an external service.'], steps: ['Choose Start tutorial on the welcome screen.', 'Continue through the privacy summary.', 'Create the disposable sample workspace when prompted.'] },
      { heading: 'Review the request', paragraphs: ['The tutorial creates an environment variable and a GET request that uses it. The resolved address appears in the request editor before you send anything.'], steps: ['Confirm that the method is GET.', 'Review the URL and the resolved environment value.', 'Select Send and wait for the response panel.'] },
      { heading: 'Understand the response', paragraphs: ['ReqMint shows the HTTP status, duration, headers, and formatted body together. Save the request after reviewing the response to complete the tutorial.'], note: 'You can skip, resume, restart, or remove the tutorial workspace at any time.' },
    ],
  },
  {
    slug: 'requests',
    category: 'Core workflow',
    title: 'Build and send requests',
    summary: 'Methods, URLs, query values, headers, bodies, cancellation, and response review.',
    readTime: '7 min',
    sections: [
      { heading: 'Choose a method and address', paragraphs: ['Select the HTTP method, then enter an absolute URL or a URL containing environment placeholders such as {{baseUrl}}. ReqMint resolves placeholders immediately before execution.'] },
      { heading: 'Add request data', steps: ['Use Query for URL query-string values.', 'Use Headers for content negotiation, authorization, and custom metadata.', 'Use Body for supported request content.', 'Disable a row when you want to keep it without sending it.'] },
      { heading: 'Send or cancel', paragraphs: ['Select Send to execute the request. Long-running work is asynchronous and cancellable; cancelling does not save a partial response as a successful history item.'] },
      { heading: 'Save deliberately', paragraphs: ['Save a useful request into a collection with a clear name. ReqMint keeps unsaved changes visible and asks before a destructive navigation or close action.'], note: 'Certificate verification remains enabled by default. ReqMint never silently persists a verification bypass.' },
    ],
  },
  {
    slug: 'environments',
    category: 'Core workflow',
    title: 'Use environments and variables',
    summary: 'Keep local, staging, and production values separate without duplicating requests.',
    readTime: '6 min',
    sections: [
      { heading: 'Create an environment', steps: ['Open Environments in the workspace sidebar.', 'Create an environment such as Local or Staging.', 'Add a variable name and value, then save.'] },
      { heading: 'Reference a value', paragraphs: ['Use double braces in request fields: {{baseUrl}}, {{tenantId}}, or another variable name. ReqMint resolves request templates against the selected environment.'] },
      { heading: 'Protect secrets', paragraphs: ['Mark sensitive values as secrets where supported. ReqMint redacts configured secrets from local history and scans managed workspace files before supported Git publish operations.'], note: 'Do not commit real credentials. Secret scanning is a final safety net, not a replacement for secure credential handling.' },
    ],
  },
  {
    slug: 'collection-runner',
    category: 'Testing',
    title: 'Run collections and assertions',
    summary: 'Execute ordered request suites with data rows, assertions, cancellation, and reports.',
    readTime: '8 min',
    sections: [
      { heading: 'Prepare the collection', paragraphs: ['Add saved requests to a collection and order them intentionally. Collection runs are sequential by default so results stay deterministic.'] },
      { heading: 'Add declarative assertions', steps: ['Check an expected status code.', 'Set a maximum duration.', 'Verify a response header.', 'Check JSON path existence or value.', 'Use body contains or pattern matching when appropriate.'] },
      { heading: 'Run with data', paragraphs: ['Choose a supported local data file to repeat the collection for multiple rows. Preview and validation limits protect the app from unexpectedly large inputs.'] },
      { heading: 'Review and export', paragraphs: ['The results screen separates passed, failed, skipped, and cancelled requests. Export local JSON or JUnit results when you need a build artifact or CI-friendly report.'], note: 'Run exports exclude full response bodies and secret values by design.' },
    ],
  },
  {
    slug: 'git-collaboration',
    category: 'Collaboration',
    title: 'Collaborate through Git',
    summary: 'Review workspace changes, commit safely, check remote state, and keep control.',
    readTime: '9 min',
    sections: [
      { heading: 'Use an ordinary repository', paragraphs: ['A ReqMint workspace is a normal folder with reviewable JSON documents. Git remains optional, and ReqMint never stores Git credentials.'] },
      { heading: 'Review before committing', steps: ['Open Git status inside the workspace.', 'Inspect only the ReqMint-managed file changes.', 'Stage the intended workspace files.', 'Review the exact staged diff and security scan result.', 'Enter a valid commit message and confirm.'] },
      { heading: 'Check and update explicitly', paragraphs: ['Remote checks and pushes require separate user confirmation. A fast-forward update is offered only for a clean workspace whose incoming changes stay entirely inside ReqMint-managed paths.'] },
      { heading: 'Know the boundaries', paragraphs: ['ReqMint does not rebase, force-push, rewrite history, switch branches automatically, or resolve arbitrary repository files. Complex repository work stays in your preferred Git tool.'], note: 'Any unscannable, oversized, conflicted, or out-of-scope change fails closed.' },
    ],
  },
  {
    slug: 'history-privacy',
    category: 'Privacy',
    title: 'Control local history and privacy',
    summary: 'Understand what stays on the device, what is redacted, and how to remove it.',
    readTime: '5 min',
    sections: [
      { heading: 'What ReqMint stores', paragraphs: ['Application settings, onboarding progress, bounded request history, collection-run summaries, and user-selected workspace files remain on your device. ReqMint has no hosted workspace sync service.'] },
      { heading: 'History safety', paragraphs: ['Sensitive headers and configured secret values are redacted before request history is persisted. Collection-run exports store summaries and assertion evidence rather than full response bodies.'] },
      { heading: 'Remove local data', steps: ['Clear request or collection-run history from its screen.', 'Delete a disposable tutorial workspace from the operating system.', 'Remove a normal workspace folder only when you no longer need its files or Git history.', 'Use the operating system application-data controls to remove settings after uninstalling.'] },
    ],
  },
  {
    slug: 'background-mode',
    category: 'Desktop',
    title: 'Choose close and background behavior',
    summary: 'Keep ReqMint available in the system tray or exit fully when the window closes.',
    readTime: '3 min',
    sections: [
      { heading: 'Choose once, change later', paragraphs: ['The first time you close the window, ReqMint asks whether closing should minimize the app to the system tray or exit completely. Your choice is stored locally.'] },
      { heading: 'Use the tray', steps: ['Close the main window after selecting background behavior.', 'Use the ReqMint tray icon to reopen the window.', 'Choose Exit from the tray menu when you want to stop the application fully.'] },
      { heading: 'Protect unsaved work', paragraphs: ['Unsaved request, environment, and collection edits are checked before a close action can discard them.'] },
    ],
  },
  {
    slug: 'themes-language',
    category: 'Personalization',
    title: 'Change theme and language',
    summary: 'Choose a calm, modern, or vivid visual theme and switch between English and Turkish.',
    readTime: '4 min',
    sections: [
      { heading: 'Select a theme', paragraphs: ['ReqMint themes use the same semantic color contract, so request states, warnings, focus, and Git diffs remain understandable across every approved palette.'] },
      { heading: 'Switch language', paragraphs: ['Choose English or Turkish from application settings. The shell, onboarding experience, background controls, Git safety messages, and runner results update from the same localization system.'] },
      { heading: 'Accessibility expectations', paragraphs: ['Visible focus, keyboard navigation, screen-reader names, scaling, and supported high-contrast behavior are public-release gates.'], note: 'If a theme makes an important state difficult to distinguish, report it with the theme name and operating system.' },
    ],
  },
];

export const guideBySlug = (slug: string) => guides.find((guide) => guide.slug === slug);
