# HS Code RAG Data Folder

Place your Excel file in this folder.

Required default filename:
- `hs_code_rag.xlsx`

Required first-row headings (minimum):
- `HS Code`
- `description`
- other columns like `SU`, `ID`, `VA`, `LXY`, etc.

HS code normalization rule used by the API:
- Input like `0101210000` is formatted as `0101.21.00.00`
- First 4 digits, then 2 digits, then 2 digits, then 2 digits

Response rule used by the API:
- Returns only columns with values
- Empty values are omitted
- Zero-like values are omitted (for example: `0`, `0.0`, `0.00`, `0%`)
