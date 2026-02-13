# NCS HS Code Intelligence Tool – API Documentation

## 1. Base URLs
Typical local endpoints:
- Backend direct: `http://localhost:8080`
- Gateway: `http://localhost:8088`

When using ngrok, replace with your public ngrok URL.

## 2. Conventions
- JSON uses camelCase.
- HS code canonical format in enriched outputs: `4.2.2.2` (e.g. `0101.21.00.00`).
- RAG-enriched fields may appear in scan matches:
  - `ragValidated: boolean`
  - `ragColumns: Record<string, string>` (non-empty/non-zero columns only)

## 3. Authentication APIs
### 3.1 Login
`POST /api/auth/login`

Request:
```json
{
  "serviceNumberOrEmail": "NCS12345",
  "password": "your-password"
}
```

Response:
```json
{
  "token": "<jwt>",
  "officerId": "<id>",
  "role": "Officer"
}
```

### 3.2 Forgot Password (stub)
`POST /api/auth/forgot-password`

Request:
```json
{ "serviceNumberOrEmail": "NCS12345" }
```

## 4. Core HS Scan APIs
### 4.1 Start Scan
`POST /api/hscode/scan`

Content-Type: `multipart/form-data`
- `description` (optional string)
- `file` (optional file: PDF/JPEG/PNG)

Response:
```json
{ "jobId": "<guid>" }
```

### 4.2 Poll Scan Status
`GET /api/hscode/scan/{jobId}`

Pending:
```json
{ "status": "pending" }
```

Completed:
```json
{
  "status": "completed",
  "result": {
    "matches": [
      {
        "hsCode": "0501.00.00.00",
        "description": "Human hair, unworked...",
        "matchPercent": 80,
        "comment": "",
        "subsections": [],
        "ragValidated": true,
        "ragColumns": {
          "HS code": "0501.00.00.00",
          "Description": "Human hair, unworked...",
          "ID": "5",
          "VAT": "7.5"
        }
      }
    ],
    "note": null,
    "recentHsCodes": [
      { "hsCode": "0501.00.00.00", "description": "Human hair, unworked..." }
    ]
  }
}
```

Failed:
```json
{ "status": "failed", "error": "HS code scan failed." }
```

### 4.3 Recent Scan Summary
`GET /api/hscode/recent`

Response:
```json
[
  { "hsCode": "0501.00.00.00", "description": "Human hair, unworked..." }
]
```

## 5. Integration HS Scan APIs
### 5.1 Start Integration Scan
`POST /api/integrations/hscode/scan`

Request:
```json
{
  "requestId": "ext-12345",
  "description": "Honda civic",
  "imageBase64": null,
  "sourceSystem": "main-platform"
}
```

Responses:
- Accepted:
```json
{ "requestId": "ext-12345", "status": "accepted", "jobId": "<guid>" }
```
- Needs more detail / rejected:
```json
{ "requestId": "ext-12345", "status": "needs_more_detail", "message": "..." }
```
```json
{ "requestId": "ext-12345", "status": "rejected", "message": "..." }
```

### 5.2 Poll Integration Scan Status
`GET /api/integrations/hscode/scan/{jobId}`

Responses:
```json
{ "requestId": "ext-12345", "status": "pending", "jobId": "<guid>", "matches": null, "note": null, "message": null }
```
```json
{ "requestId": "ext-12345", "status": "completed", "jobId": "<guid>", "matches": [ ... ], "note": "", "message": null }
```
```json
{ "requestId": "ext-12345", "status": "failed", "jobId": "<guid>", "matches": null, "note": null, "message": "HS code scan failed." }
```

## 6. RAG APIs
The same controller is available via two route families:
- `/api/hscode/rag/*`
- `/api/integrations/hscode/rag/*`

### 6.1 Reload RAG Workbook
`POST /api/hscode/rag/reload`
(or `/api/integrations/hscode/rag/reload`)

Success:
```json
{ "status": "ok", "message": "Loaded", "rows": 6513 }
```

Error (file missing/invalid):
```json
{ "status": "error", "message": "Excel file not found ...", "rows": 0 }
```

### 6.2 Lookup by HS Code
`GET /api/hscode/rag/lookup/{hsCode}`
(or `/api/integrations/hscode/rag/lookup/{hsCode}`)

Success:
```json
{
  "columns": {
    "HS code": "0101.21.00.00",
    "Description": "Live Purebred breeding horses",
    "SU": "U",
    "ID": "5",
    "VAT": "7.5"
  }
}
```

Not found:
```json
{ "status": "not_found", "message": "No row found for the provided HS code." }
```

### 6.3 Search by Query
`POST /api/hscode/rag/search`
(or `/api/integrations/hscode/rag/search`)

Request:
```json
{ "query": "Human hair", "topK": 10 }
```

Response:
```json
{
  "total": 10,
  "rows": [
    {
      "columns": {
        "HS code": "0501.00.00.00",
        "Description": "Human hair, unworked...",
        "ID": "5",
        "VAT": "7.5"
      }
    }
  ],
  "note": null
}
```

## 7. Levy/Charge Interpretation (UI Mapping)
The frontend expands common levy codes to readable labels (e.g., `WFL`, `ETLS`, `NAN`, `TXT`, `RES`) in View Details.

Integrators can consume raw keys from `ragColumns` and map them similarly in their own UI.

## 8. Error Handling Guidance
- Handle async polling timeout client-side.
- Handle `status = failed` and message payloads.
- Treat `ragValidated = false` as non-canonical AI output requiring manual review.

## 9. Developer Integration Checklist
1. Use async flow: start scan -> poll status endpoint.
2. Display canonical `hsCode` from completed response.
3. Display `ragColumns` for charges only when present.
4. Use RAG lookup/search endpoints for direct tariff queries.
5. Reload RAG after Excel updates.
