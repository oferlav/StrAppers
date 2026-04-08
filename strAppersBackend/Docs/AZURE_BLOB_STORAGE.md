# Azure Blob Storage (resource attachments)

The API uploads board resource files to **Azure Blob Storage** and stores the returned **HTTPS URL** in the `Resources` table (`Url` column), same as pasted links.

## What you need from Azure

1. A **Storage account** (standard general-purpose v2 is fine).
2. A **connection string** for that account (used by the backend only — never put this in the frontend).

## Azure Portal setup

1. **Create storage account** (if you do not have one): Portal → Storage accounts → Create.
   - Region: same as your App Service if possible.
   - Redundancy: LRS is usually enough for documents.
2. After deployment, open the storage account → **Security + networking** (or **Access keys**).
3. Under **Access keys**, copy **Connection string** for **key1** (or key2).

## App configuration (backend)

Add the connection string to configuration (choose one pattern):

### `appsettings.Production.json` (local / IIS)

```json
"AzureStorage": {
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net",
  "ContainerName": "resources",
  "PublicBlobAccess": true,
  "MaxBytes": 26214400
}
```

### Azure App Service (recommended for production)

In the App Service → **Configuration** → **Application settings**, add:

| Name | Value |
|------|--------|
| `AzureStorage__ConnectionString` | (paste full connection string) |
| `AzureStorage__ContainerName` | `resources` (optional) |
| `AzureStorage__PublicBlobAccess` | `true` or `false` (optional, default `true`) |

Use **double underscores** `__` for nested JSON sections in environment variables.

### Public vs private blobs

- **`PublicBlobAccess`: `true` (default)**  
  The container is created with **blob-level public read** so the stored `https://…blob.core.windows.net/…` URL opens directly in the browser (simplest for “resource links”).

- **`PublicBlobAccess`: `false`**  
  Blobs are private. Stored URLs still point at Azure, but anonymous access will fail. Users can open files via the backend proxy:  
  `GET /api/AzureBlobStorage/use/file/{resourceId}`  
  (returns the file with `Content-Disposition`.)

## API endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/AzureBlobStorage/use/upload-blob` | Multipart: `file`, `boardId`, `studentId` → `{ "url": "https://…" }` |
| `GET` | `/api/AzureBlobStorage/use/file/{resourceId}` | Stream/download (private blobs) |

After upload, the frontend calls existing **`POST /api/Resources/use/add`** (or **modify**) with `name`, `url`, `studentId`, optional `sprintNumber`.

## Mentor resource review — read-only SAS (PDF / images)

`POST /api/Mentor/use/resource-review` can embed a **short-lived read-only SAS URL** (default **15 minutes**, clamped **5–60**) for **PDFs and images** instead of base64. Configure:

```json
"AzureStorage": {
  "ResourceReviewSasExpiryMinutes": 15
}
```

**Azure Portal — what you usually need**

- **Nothing extra** if the app already uses a normal **connection string that includes the account key** (default “Access key” connection string). The API calls `BlobClient.GenerateSasUri` with **read-only** permission.
- **Important:** If you disable **“Allow storage account key access”** on the storage account (Microsoft Entra–only hardening), **`GenerateSasUri` from the SDK will not work** unless you switch the code to **user delegation SAS** (OAuth) — not implemented in this repo path.
- **Firewall:** If the storage account allows only **selected networks**, the **LLM provider’s servers** (e.g. OpenAI) may be **unable to fetch** the SAS URL for vision/URL-fetch flows. For that scenario you either allow public network access for blob HTTPS (still gated by SAS token), add provider egress IPs if Microsoft publishes them, or keep using **server-side extraction** / proxy instead of passing SAS to the model.

## Allowed file types (server-enforced)

Extensions include: Office (`.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, …), PDF, common images, text/CSV/RTF, etc. Max size default **25 MB** (`AzureStorage:MaxBytes`).

## Packages

- `Azure.Storage.Blobs` (see `strAppersBackend.csproj`)

## Troubleshooting

- **503** on upload: `AzureStorage:ConnectionString` is missing or invalid.
- **400** file type: extension not in the allow list.
- **413**: increase Kestrel / IIS / App Service **request size** limits and `MaxBytes` / multipart limits to match.

## Security notes

- Treat the storage **account key** like a database password; rotate keys if leaked.
- For production, consider **Managed Identity** + **user delegation SAS** instead of the account key (larger change; current code uses connection string for simplicity).
