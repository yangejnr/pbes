# NCS HS Code Intelligence Tool – Administrative Document

## 1. Purpose
The NCS HS Code Intelligence Tool provides officers with AI-assisted HS code classification, now reinforced with internal tariff validation (Excel-based RAG). This improves consistency, transparency, and readiness for operational review.

## 2. Business Value
- Faster classification support for officers.
- Better alignment with internal tariff records.
- Reduced ambiguity through canonical HS code formatting.
- Clear charge breakdowns in result details.

## 3. Key Capabilities
- **HS Code Scan**
  - Accepts item description and optional image.
  - Returns ranked matches with details.
- **RAG Validation & Enrichment**
  - Matches are validated against internal Excel tariff data.
  - Returns charge/levy fields only when they have meaningful values.
- **Charges Visibility in View Details**
  - Levy abbreviations are expanded for readability.
  - Examples: `WFL`, `ETLS`, `NAN`, `TXT`, `RES`.
- **Integration Support**
  - External systems can submit scans and poll status.
  - Dedicated RAG endpoints available under integration route alias.

## 4. HS Code Standard Used in the System
The operational format is:
- **4.2.2.2** (e.g., `0101.21.00.00`)

This format is used for display and API outputs where RAG validation succeeds.

## 5. Operational Workflow
1. Officer opens the portal and starts scan.
2. Officer provides item description and optional image.
3. System runs AI classification.
4. System post-validates and enriches using internal Excel tariff data.
5. Officer reviews matched HS code and charges in “View Details”.

## 6. Governance Notes
- AI output remains advisory and should be reviewed by trained officers.
- RAG-backed outputs improve consistency but do not replace policy judgment.
- Excel tariff source should be controlled and updated through approved internal process.

## 7. Administration & Maintenance
- Runtime stack: Dockerized backend/frontend/gateway/postgres.
- RAG data file location: `data/hs-rag/hs_code_rag.xlsx`.
- Service startup script: `./run-all.sh`.
- API and integration details for developers: `docs/API_DOCUMENTATION.md`.

## 8. Stakeholder Communication Summary
When sharing status with management and partner teams, communicate:
- System now returns RAG-validated HS codes.
- Charges shown are filtered to non-empty/non-zero values only.
- API routes are available for both core and integration consumption.

## 9. Conclusion
The current release provides a stronger operational baseline by combining AI speed with tariff-grounded validation and clearer result interpretation for officers and integrated systems.
