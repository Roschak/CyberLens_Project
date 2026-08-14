# Storage backends

Uploaded files (chat attachments, generated reports) are stored through the `IFileStorage` abstraction. Select a backend in **Settings → Storage**. Files are always served to the browser through the `/files/{path}` endpoint regardless of backend, so switching providers doesn't change URLs in the UI.

| Backend | `Provider` value | Config keys |
|---------|------------------|-------------|
| Local filesystem (default) | `FileSystem` | `FileSystemRoot` |
| Azure Blob Storage | `AzureBlob` | `AzureBlobConnectionString`, `AzureBlobContainer` |
| Amazon S3 | `S3` | `S3AccessKey`, `S3SecretKey`, `S3Region`, `S3Bucket` |
| MinIO (S3-compatible) | `MinIO` | `MinioEndpoint`, `MinioAccessKey`, `MinioSecretKey`, `MinioBucket` |

## How it works

- `StorageService` (singleton) reads the current provider from settings and lazily builds the matching backend. When settings change, it rebuilds on next use — no restart needed for storage credential changes.
- `SaveUploadAsync` stores uploads under `uploads/yyyy/MM/<guid>_<name>` and returns a `/files/...` URL.
- Reports are stored under `reports/yyyy/MM/...`.
- MinIO reuses the S3 client with a custom `ServiceURL` and path-style addressing. Containers/buckets are created automatically if missing.

## Path safety

Storage paths are sanitized (`..` rejected, backslashes normalized) before use, on every backend.
