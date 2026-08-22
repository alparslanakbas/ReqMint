# Collection Runner

ReqMint's Collection Runner executes the saved requests in the selected collection in document order. The first vertical slice is deliberately deterministic and local: one request runs at a time, the selected environment is resolved immediately before each request, and cancellation stops the active request before any later request begins.

## Result boundary

Run results retain only the saved request identity and name, outcome category, HTTP status code, and duration. Response bodies, resolved URLs, query values, request or response headers, exception messages, and secret values are not copied into the run report. Missing variables, timeouts, transport failures, and invalid request configuration are represented by fixed categories suitable for localization.

The runner continues after an HTTP or request-level failure by default so the user receives a complete collection report. The optional stop-on-failure setting marks later requests as not run. A cancelled active request is marked cancelled, later requests are marked not run, and the completed prefix remains visible.

## Resource and safety limits

- Runs are sequential; the initial implementation does not create parallel network pressure.
- A single run is limited to 1,000 requests and requires unique non-empty request identifiers.
- Data-driven runs accept at most 100 rows and 5,000 total request executions.
- The UI runs only persisted collection data. Unsaved request, collection, or environment editor changes must be saved or discarded first.
- Secret values are retrieved from the platform vault through the existing template resolver and exist only in the resolved request passed to the HTTP executor.
- Progress reports expose the completed count and the latest sanitized result only.

The same sensitive-data boundary applies to local run history.

## Declarative assertions

Saved requests can define up to 50 bounded assertions. The first supported rules are an exact HTTP status code, a maximum response duration in milliseconds, and JSON-field existence through RFC 6901 JSON Pointer syntax. A request with assertions passes only when every assertion passes; this allows an expected non-success response such as HTTP 404 to be treated as a successful test. Requests without assertions retain the normal 2xx success rule.

JSON assertions parse only the already bounded response preview with a maximum depth of 64. A truncated or malformed body cannot produce a false pass and is reported as not evaluated. Result objects retain only the assertion kind and outcome; expected values, JSON content, resolved request data, and exception messages are not copied into reports.

## Safe result export

Completed and cancelled runs can be exported locally as an indented JSON report or JUnit-compatible XML. Both formats are generated from the sanitized in-memory result model, so they contain collection and request identifiers and names, outcome categories, status codes, durations, and assertion outcomes only. They never contain resolved URLs, query values, headers, request or response bodies, environment values, vault secrets, stack traces, or raw exception messages.

JSON reports carry an explicit schema version for future compatibility. JUnit reports map failed assertions to `failure`, safe request errors to `error`, and cancelled or unexecuted requests to `skipped`; error messages come from a fixed allowlist rather than runtime exception text. ReqMint asks the user to choose the destination and does not upload reports automatically.

## Data-driven iterations

The user may select a local UTF-8 JSON or CSV data file before starting a run. ReqMint executes the entire saved collection once for every data row. A matching data field has precedence over the selected environment for that iteration, while unmatched variables continue to resolve from the environment and platform vault. Iterations and requests remain strictly sequential, and stop-on-failure or cancellation marks every later execution as not run.

The selected file is limited to 1 MiB, 100 rows, 100 fields per row, 4,096 characters per value, and 5,000 total request executions. Only flat strings, numbers, and booleans are accepted from JSON; CSV supports quoted commas, quotes, and line endings. Data values are held only for the active Runner screen and are never copied into result objects or exports. Reports include the non-sensitive one-based iteration number so duplicate request executions can be distinguished. See [Collection Runner data files](COLLECTION_RUN_DATA.md) for examples.

## Local run history

ReqMint stores a separate, sanitized history for each collection in the local application database. A history entry contains only collection and request identifiers and names, recording time, outcome categories, status codes, durations, iteration numbers, and assertion outcomes. The table and its serialized request-result model do not have fields for URLs, query values, headers, request or response bodies, data-file values, environment values, secrets, stack traces, or raw exception messages.

The retention setting keeps between 10 and 200 reports per collection and defaults to 50. New entries are inserted and older entries outside the configured limit are deleted in one transaction. A single serialized result is capped at 2 MiB so unusually large runs cannot grow the database without bound; the completed result remains available for immediate export when this history cap is exceeded. Previous runs can be reopened and exported through the same sanitized JSON and JUnit pipeline. Clearing history requires explicit confirmation, is scoped to the selected workspace and collection, and also removes the displayed in-memory report.

## Filtering and rerunning failures

The result view can show all, passed, failed, or skipped executions without changing the underlying report. The failed filter includes both assertion/HTTP failures and safe request errors; the skipped filter includes cancelled and not-run executions.

ReqMint can rerun only failed or errored execution keys while preserving their original collection and iteration order. The runner validates every request identifier, iteration number, and duplicate selection before sending any request. Reruns are marked explicitly in sanitized history and JSON/JUnit exports.

Data-driven reruns are available only while the original bounded data set remains in memory on the active Runner screen. Data values are deliberately excluded from results and local history, so a historical data-driven run cannot reconstruct its inputs; the UI asks the user to select the data file and run the full collection again instead of silently using incomplete or changed values.
