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

ReqMint rejects a workspace save if a secret value is present. Secret storage will use the operating system credential vault and will remain outside Git-managed files.

## Safety guarantees

- Workspace and document schema versions are validated when loading and saving.
- Collection and environment references cannot escape their designated folders.
- Duplicate IDs, duplicate file targets, and mismatched references are rejected.
- Documents are replaced atomically to avoid partially written JSON files.
- The root manifest is written last so it never points to a document that was not saved.
