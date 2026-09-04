# Workspace format

ReqMint workspaces are ordinary folders designed to be reviewed and shared with Git. The format is versioned so future ReqMint releases can migrate older workspaces safely.

```text
my-api-workspace/
├── reqmint.workspace.json
├── collections/
│   └── users.json
└── environments/
    └── local.json
```

The root `reqmint.workspace.json` file contains the workspace identity and references to collection and environment documents. Requests live in collection files, keeping unrelated Git changes isolated.

## Secret values

An environment variable can be marked as secret, but its value must be `null` in every workspace document:

```json
{
  "name": "API_TOKEN",
  "value": null,
  "isSecret": true
}
```

ReqMint rejects a workspace save if a secret value is present. On Windows, secret values are stored in Windows Credential Manager and remain outside Git-managed files. macOS Keychain and Linux Secret Service adapters are planned; ReqMint does not use a plaintext fallback when a platform vault is unavailable.

## Request variables

Requests can use environment values in URLs, query parameters, headers, bodies, and content types:

```text
{{BASE_URL}}/orders/{{ORDER_ID}}
Authorization: Bearer {{API_TOKEN}}
```

Templates remain unchanged in collection files. ReqMint resolves them from the active environment immediately before sending and reports all missing values together.

For `application/x-www-form-urlencoded` bodies, fields are stored as structured name/value entries. ReqMint resolves templates in both names and values, skips disabled entries, and performs URL encoding only when sending.

Multipart bodies store text fields plus file-field names and display filenames. Local file paths are intentionally excluded from workspace JSON, history, and Git; users reselect files after restarting ReqMint. File content is streamed when the request is sent rather than loaded fully into memory.

## Postman collection import

ReqMint can append Postman Collection v2.1 JSON files to an open workspace. Top-level Postman folders become ReqMint collections and deeper folder names are preserved in request names. URLs, methods, query parameters, headers, raw bodies, URL-encoded forms, multipart forms, and safe variable-based authentication are converted. Scripts and unsupported body/authentication modes are reported rather than silently represented as working features. Literal sensitive values are omitted, and imported upload files must be reselected locally.

## OpenAPI import

ReqMint can append OpenAPI 3.0 and 3.1 definitions in JSON or YAML format to an open workspace. Operations are grouped by their first tag and converted with server URLs, parameters, request-body examples or schemas, multipart uploads, and HTTP Bearer, Basic, or API-key security. Missing examples become environment-variable placeholders. Authentication placeholders must be created as Secret variables. Literal sensitive examples, external references, cookie parameters, and unsupported features are omitted or replaced and reported in the import summary.

## Request authentication

Requests may optionally configure Bearer Token, Basic Auth, or an API key in a header or query parameter. Authentication secrets must be a single environment-variable reference, never a literal value:

```json
{
  "authentication": {
    "type": "Bearer",
    "bearerToken": "{{API_TOKEN}}"
  }
}
```

The referenced variable must exist in the active environment and be marked `isSecret: true`. ReqMint rejects literal authentication secrets before a workspace is written and reads the value from the operating-system vault only when sending. Older request documents without `authentication` remain valid and behave as No Auth. Auth configuration is removed from request-history snapshots.

## Runner assertions

Assertions are optional and older request documents without the field remain valid. They are stored with the request so teams review test expectations through Git:

```json
{
  "assertions": [
    { "kind": "StatusCodeEquals", "expectedStatusCode": 200 },
    { "kind": "MaximumDuration", "maximumDurationMilliseconds": 750 },
    { "kind": "JsonPointerExists", "jsonPointer": "/data/id" }
  ]
}
```

JSON-field checks use JSON Pointer, not executable scripts. Assertion counts, status values, duration limits, pointer length, pointer depth, and escape sequences are validated when loading and saving.

## Safety guarantees

- Workspace and document schema versions are validated when loading and saving.
- Collection and environment references cannot escape their designated folders.
- Workspace documents cannot traverse symbolic links or filesystem reparse points.
- Each JSON document is limited to 16 MiB before parsing or replacing an existing file.
- Duplicate IDs, duplicate file targets, and mismatched references are rejected.
- Literal Bearer tokens, Basic Auth passwords, and API key values cannot be persisted.
- Documents are replaced atomically to avoid partially written JSON files.
- The root manifest is written last so it never points to a document that was not saved.
