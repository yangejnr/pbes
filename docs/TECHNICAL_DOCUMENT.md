# NCS HS Code Intelligence Tool – Technical Document

## 1. Overview
The NCS HS Code Intelligence Tool is a web-based classification system for Nigeria Customs Service workflows. It combines AI-assisted HS code suggestions with Excel-based RAG validation and charge enrichment.

The system supports:
- Description-only scans
- Description + image scans
- Integration API requests from other systems
- Canonical HS code normalization and levy/charge extraction from a local Excel source

## 2. Objectives
- Provide fast HS code recommendations.
- Ensure returned HS codes are validated against internal tariff data.
- Return only meaningful charge fields (non-empty, non-zero).
- Provide clear operational endpoints for internal integrations.

## 3. Architecture
### 3.1 Components
- **Frontend (React + TypeScript + Tailwind)**
  - Scan modal, results table, details modal
  - Charges display with levy abbreviation meanings
- **Backend (.NET 8 Web API)**
  - Async scan job orchestration
  - Input validation
  - Ollama integration
  - RAG lookup, enrichment, and response shaping
- **RAG Data Source (Excel)**
  - File: `data/hs-rag/hs_code_rag.xlsx`
  - Loaded via ClosedXML
- **Ollama (Local Llama)**
  - Text model: `llama3:8b`
  - Vision model: `llama3.2-vision`
- **Gateway / Runtime**
  - Nginx gateway
  - Docker Compose services
  - Optional ngrok exposure

### 3.2 Data Flow
1. User submits scan request.
2. Backend validates request and starts async job.
3. Backend calls Ollama for candidate matches.
4. Each match is enriched against RAG:
   - Normalize HS code to canonical format.
   - Attempt lookup by code.
   - If code not found, fallback to best RAG description match (anchored to user query).
5. Response returns canonical HS code/description + filtered charge columns.

## 4. HS Code Normalization
Canonical format is now:
- **4 digits + dot + 2 digits + dot + 2 digits + dot + 2 digits**
- Example: `0101210000` -> `0101.21.00.00`

This format is used consistently in RAG output and enriched scan responses.

## 5. RAG Rules
- Required Excel columns include:
  - `HS Code`
  - `Description`
  - Charges/levies columns (e.g. `SU`, `ID`, `VAT`, `WFL`, `ETLS`, etc.)
- Output rules:
  - Exclude empty values
  - Exclude numeric zero values (`0`, `0.0`, `0.00`, `0%`)
- Enrichment fields per match:
  - `ragValidated` (boolean)
  - `ragColumns` (key/value map of filtered columns)

## 6. API Summary
Implemented endpoint groups:
- Auth APIs: `/api/auth/*`
- Core scan APIs: `/api/hscode/*`
- Integration scan APIs: `/api/integrations/hscode/*`
- RAG APIs:
  - `/api/hscode/rag/*`
  - `/api/integrations/hscode/rag/*` (alias for integration consumers)

Full endpoint details are documented in `docs/API_DOCUMENTATION.md`.

## 7. Tech Stack
- Frontend: React, TypeScript, Tailwind CSS, Vite
- Backend: ASP.NET Core (.NET 8)
- Data: PostgreSQL (officer auth data), Excel file (RAG tariff data)
- AI: Ollama local models
- Packaging: Docker / Docker Compose
- Image registry: AWS ECR

## 8. Deployment Notes
- Backend requires mounted RAG folder:
  - Compose mount: `./data:/data:ro`
- Config key:
  - `HsCodeRag:FilePath` (default `../data/hs-rag/hs_code_rag.xlsx` in container context)
- Frontend and backend updates must be rebuilt/redeployed together when response structure/UI changes.

## 9. Security & Operations
- JWT authentication for officer endpoints.
- Password hashing via ASP.NET Identity password hasher.
- Configurable secrets and DB connection via appsettings/environment variables.
- Logs available through container logs and `/tmp` runtime logs for ngrok/ollama scripts.

## 10. Known Constraints
- AI model may produce semantically noisy candidates; RAG fallback mitigates but does not replace officer review.
- Long-running scans depend on model/image complexity.

## 11. Future Enhancements
- Persist full scan/audit history to database.
- Confidence blending model between AI score and RAG match score.
- Admin tooling for RAG file versioning and validation report.
