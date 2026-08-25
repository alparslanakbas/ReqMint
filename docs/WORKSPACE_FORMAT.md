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
- Documents are replaced atomically to avoid partially written JSON files.
- The root manifest is written last so it never points to a document that was not saved.
