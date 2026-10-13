# 10 — Document upload/download

**What to build:** attaching and retrieving supporting documents against a claim, backed by Azure Blob Storage with a local-filesystem fallback.

**Blocked by:** 05

**Status:** ready-for-agent

- [ ] Uploading a document stores it via the configured storage provider (Azure Blob or local filesystem, switchable via configuration) under a path scoped to organisation and claim, with the filename sanitised against path traversal.
- [ ] `GET /api/claims/{id}/documents` lists documents with metadata (type, name, uploaded by/at).
- [ ] Document retrieval returns a short-lived (1-hour) signed URL rather than streaming bytes through the API.
- [ ] If Azure Blob Storage isn't reachable in local development, the system falls back to local-filesystem storage without code changes — only configuration.
- [ ] The Documents tab supports uploading via a file picker and downloading via the returned link, with loading and error states consistent with the rest of the app.
