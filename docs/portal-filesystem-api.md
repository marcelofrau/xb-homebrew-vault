---
layout: default
title: Portal Filesystem API
description: Xbox Dev Mode WDP filesystem endpoints (/api/filesystem/apps/*) — proven request formats, multipart upload gotchas, and operational notes validated against an Xbox Series X.
---

# Portal Filesystem API (WDP Xbox)

Reference for the Xbox Developer Mode Device Portal filesystem endpoints, validated against
an Xbox Series X (OsVersion `26100`) via the built-in web UI, captured HAR, and working cURLs.

Use this document when implementing filesystem browsing/transfer in another client (e.g. X-Files).
It records the exact formats that **work**, the ones that **fail**, and why.

## Base URL & Authentication

```
https://<xbox-ip>:11443
```

- **Auth**: HTTP Basic (`Authorization: Basic <base64(user:pass)>`).
- **CSRF**: state-changing calls (`POST`/`DELETE`) require:
  - Cookie `CSRF-Token=<token>` (auto-sent by the cookie jar), **and**
  - Header `X-CSRF-Token: <token>`.
- The token is obtained from `GET /api/os/info` (falls back to `GET /`). The web UI also
  sets `Set-Cookie: CSRF-Token=...` on `GET /`.

## Path conventions

`knownfolderid` values seen on this console: `DevelopmentFiles`, `LocalAppData`.

Portal `path` values are backslash-separated and always begin with `\\`:

| Level | Portal path | URL-encoded |
|-------|-------------|-------------|
| Root of a known folder | `\` | `%5C` |
| One level (`teste`) | `\\teste` | `%5C%5Cteste` |
| Two levels (`teste\sub`) | `\\teste\\sub` | `%5C%5Cteste%5C%5Csub` |

For `LocalAppData`, the package full name is a **separate query parameter**
(`packagefullname=...`) and is **not** part of `path`.

## Operations

### List known folders

```
GET /api/filesystem/apps/knownfolders
```

### List files / packages

List the root of a known folder, or a sub-path:

```
GET /api/filesystem/apps/files?knownfolderid=LocalAppData&packagefullname=XFiles.Xbox_1.2.0.1128_x64__jgz7qwhvc5jpc&path=%5C
```

List entries under a package's `LocalAppData` root — **omit** `packagefullname` to list packages instead:

```
GET /api/filesystem/apps/files?knownfolderid=LocalAppData&path=%5C
```

Response (abridged):

```json
{
  "FullPath": "Q:\\Users\\UserMgr2\\AppData\\Local\\Packages\\XFiles.Xbox_jgz7qwhvc5jpc\\\\teste",
  "Items": [
    { "Name": "nested", "CurrentDir": "\\\\teste", "SubPath": "\\\\teste", "Type": 16, "FileSize": 0, "DateCreated": 134299193516051561 },
    { "Name": "song.mp3", "CurrentDir": "\\\\teste", "SubPath": "\\\\teste", "Type": 32, "FileSize": 4194304, "DateCreated": 134299193516051561 }
  ]
}
```

- `Type` is a bitmask; **`Type & 0x10 != 0` → directory**. `Type == 32` → file.
- `FullPath` for the target folder is resolved server-side (LocalAppData → `Q:\Users\UserMgr2\AppData\Local\Packages\<pkg>\...`).
- Uploads **do not create folders** — create them first (below).

### Download a file

The **filename is a separate query parameter**; `path` is the *parent folder only*:

```
GET /api/filesystem/apps/file?knownfolderid=LocalAppData&filename=song.mp3&packagefullname=XFiles.Xbox_1.2.0.1128_x64__jgz7qwhvc5jpc&path=%5C%5Cteste
```

> **Gotcha**: putting the filename at the end of `path` returns `404`. `path` = folder, `filename` = file.

### Create a folder

```
POST /api/filesystem/apps/folder?knownfolderid=LocalAppData&newfoldername=criando%20nova%20pasta&packagefullname=XFiles.Xbox_1.2.0.1128_x64__jgz7qwhvc5jpc&path=%5C%5Cteste
```

`newfoldername` is the folder to create **inside** `path`.

### Rename an entry

```
POST /api/filesystem/apps/rename?knownfolderid=LocalAppData&filename=criando%20nova%20pasta&newfilename=criando%20nova%20pasta%20renomeando&packagefullname=XFiles.Xbox_1.2.0.1128_x64__jgz7qwhvc5jpc&path=%5C%5Cteste%5Ccriando%20nova%20pasta
```

`filename` = old name, `newfilename` = new name, `path` = parent folder.

> **Critical path semantics:** unlike delete/create/upload, `path` here is the
> **full path of the entry being renamed, INCLUDING its own name**
> (`\\teste\criando nova pasta`), not the parent. Verified against browser HAR:
> `path=%5C%5Cteste%5Ccriando%20nova%20pasta&filename=criando%20nova%20pasta&newfilename=...`.
> Sending the parent path renames the wrong folder (or the package root if
> `path=\`).

> **Known Xbox bug (tested):** rename/upload into a **just-created or just-renamed
> folder** can fail. Uploads to the package root work. Observed:
> - Rename `teste`→`teste2` 6 s after folder creation → `500 {"Code": -2147467259,
>   "Reason": "Renaming the file on the system failed. Check you have the right
>   permissions."}` (`E_FAIL`).
> - Upload into a folder renamed ~10 s earlier → `500 {"Code": -2147024893,
>   "Reason": "File move failed."}` (`ERROR_PATH_NOT_FOUND`).
> - Same folder, listing + delete afterwards → work fine.
> Workaround: let the device settle (a few seconds) before targeting a
> just-created/renamed folder, or upload to the package root first.

### Delete an entry

```
DELETE /api/filesystem/apps/file?knownfolderid=LocalAppData&filename=song.mp3&packagefullname=XFiles.Xbox_1.2.0.1128_x64__jgz7qwhvc5jpc&path=%5C%5Cteste
```

`filename` = entry to delete, `path` = parent folder.

### Upload a file

```
POST /api/filesystem/apps/file?knownfolderid=LocalAppData&packagefullname=XFiles.Xbox_1.2.0.1128_x64__jgz7qwhvc5jpc&path=%5C%5Cteste&extract=false
```

Body: `multipart/form-data` — **the part format is critical**, see below.

### Upload ZIP with `extract=true`

`POST .../file?...&extract=true` with a ZIP body is **broken on Xbox** — returns
`500 {"Reason": "D:\\DevelopmentFiles\\WdpTempWebFolder\\UPDxxxx.tmp"}`.
**Workaround**: extract the ZIP locally and upload the resulting tree file-by-file
(create folders via the folder endpoint, then upload each file).

## Multipart upload format (critical)

The WDP Xbox accepts the browser's multipart format **only**. .NET's default
`MultipartFormDataContent` output is rejected with `500`.

### Format that works (Firefox web UI)

```
------geckoformboundary2f42b1f2b1fa2acb5f3b3f7c5d45460b\r\n
Content-Disposition: form-data; name="file"; filename="song.mp3"\r\n
Content-Type: application/octet-stream\r\n
\r\n
<file bytes>\r\n
------geckoformboundary2f42b1f2b1fa2acb5f3b3f7c5d45460b--\r\n
```

Rules:
1. `Content-Disposition` comes **first** (before `Content-Type`).
2. `name="file"` — **quoted**.
3. `filename="song.mp3"` — quoted, plain, **no `filename*` parameter**.
4. Part `Content-Type: application/octet-stream`.
5. Content-Length set (never chunked).

### Format that fails (.NET default)

.NET generates (via `content.Add(fileContent, "file", fileName)`):

```
--2daad1ff-f2ce-48d8-ae89-d14c98646b48\r\n
Content-Type: application/octet-stream\r\n
Content-Disposition: form-data; name=file; filename="song.mp3"; filename*=utf-8''song.mp3\r\n
...
```

Failing differences:

| # | .NET default | WDP web UI | Why it matters |
|---|--------------|------------|----------------|
| 1 | `filename="..."; filename*=utf-8''...` | `filename="..."` only | Extra RFC 5987 param corrupts filename parsing |
| 2 | `name=file` (unquoted) | `name="file"` (quoted) | Parser looking for quoted `name="file"` misses it |
| 3 | `Content-Type` before `Content-Disposition` | `Content-Disposition` first | Some parsers require disposition first |

### Fix (keep streaming, correct format)

```csharp
using var fileStream = File.OpenRead(localFilePath);
using var fileContent = new StreamContent(fileStream);
fileContent.Headers.ContentDisposition = ContentDispositionHeaderValue.Parse(
    "form-data; name=\"file\"; filename=\"" + fileName.Replace("\"", "\\\"") + "\"");
fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
using var content = new MultipartFormDataContent();
content.Add(fileContent);   // no name overload — disposition is set manually
```

## Error: `500 {"Reason": "...WdpTempWebFolder..."}`

The WDP writes the request body to `D:\DevelopmentFiles\WdpTempWebFolder\UPDxxxx.tmp`,
then moves it to the destination. A `500` with this body means the move/processing failed.

Diagnosis order:

1. **Request format** — multipart must match the section above. A wrong multipart makes
   *every* upload fail, to any folder, for any file size.
2. **Is the web UI also failing?** If the browser upload fails too, it is console state:
   - Reboot developer mode (restarts the WDP service).
   - Check the package storage quota (`Q:`) — a full quota makes the move fail.
   - Clean stale `UPD*.tmp` files left in `D:\DevelopmentFiles\WdpTempWebFolder`.
3. **Not the cause**: CSRF expiry (a successful `CreateFolder` proves the token is valid;
   the token is only re-fetched when empty). File size alone (small files fail too).
   Target path depth (uploads to the known-folder root fail the same way).

## Operational notes

- `UserFiles:\DevelopmentFiles` is surfaced as the console's `D:\DevelopmentFiles`
  (note the WDP temp folder lives under it).
- `LocalAppData` resolves to the package's isolated store on `Q:`.
- The `UPDxxxx.tmp` names are freshly generated per attempt — a new name per retry is normal
  and does **not** indicate progress.
- Keep one `HttpClient` per console with a shared `CookieContainer`; re-acquire CSRF when a
  `403` occurs.
