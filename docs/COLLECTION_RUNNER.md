# Collection Runner

ReqMint's Collection Runner executes the saved requests in the selected collection in document order. The first vertical slice is deliberately deterministic and local: one request runs at a time, the selected environment is resolved immediately before each request, and cancellation stops the active request before any later request begins.

## Result boundary

Run results retain only the saved request identity and name, outcome category, HTTP status code, and duration. Response bodies, resolved URLs, query values, request or response headers, exception messages, and secret values are not copied into the run report. Missing variables, timeouts, transport failures, and invalid request configuration are represented by fixed categories suitable for localization.

The runner continues after an HTTP or request-level failure by default so the user receives a complete collection report. The optional stop-on-failure setting marks later requests as not run. A cancelled active request is marked cancelled, later requests are marked not run, and the completed prefix remains visible.

## Resource and safety limits

- Runs are sequential; the initial implementation does not create parallel network pressure.
- A single run is limited to 1,000 requests and requires unique non-empty request identifiers.
- The UI runs only persisted collection data. Unsaved request, collection, or environment editor changes must be saved or discarded first.
- Secret values are retrieved from the platform vault through the existing template resolver and exist only in the resolved request passed to the HTTP executor.
- Progress reports expose the completed count and the latest sanitized result only.

Future slices can add declarative assertions, data-file iterations, result export, and history retention without widening this sensitive-data boundary.
