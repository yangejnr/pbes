# NCS HS Code Intelligence Tool – Technical Document

## 1. Overview
The NCS HS Code Intelligence Tool is a web-based system for HS code classification support. It allows NCS officers to submit a detailed item description and/or a clear image, then returns likely HS code matches with descriptions, confidence, comments, and subsections. The system uses a local Llama model via Ollama for classification and keeps a recent scan summary for quick reference.

## 2. Objectives
- Provide fast, AI-assisted HS code classification support.
- Support text-only and image-based submissions.
- Enforce input quality (avoid vague descriptions and unclear images).
- Maintain an audit-friendly “last 10 scans” summary.

## 3. Architecture
### 3.1 High-Level Components
- **Frontend (React + TypeScript + Tailwind)**
  - Landing page and HS code scan modal.
  - Upload and camera capture support.
  - Results table and details modal.
  - Recent scans summary.
- **Backend (.NET 8 Web API)**
  - Handles scan requests and validation.
  - Asynchronous job handling for scans.
  - Calls local Ollama service.
  - Returns structured HS code results.
- **Ollama (Local Llama)**
  - Text model: `llama3:8b`.
  - Vision model: `llama3.2-vision`.
- **Database (PostgreSQL)**
  - Officers table for authentication (JWT-based login).

### 3.2 Data Flow (Scan)
1. User submits description and/or image.
2. Backend validates input (description specificity and image clarity).
3. Backend creates an async scan job and returns `jobId`.
4. Backend calls Ollama and builds structured JSON response.
5. Frontend polls job status until complete.
6. Results displayed in the UI and recent scans updated.

## 4. Tech Stack
- **Frontend**: React, TypeScript, Tailwind CSS
- **Backend**: .NET 8 Web API (C#)
- **Database**: PostgreSQL
- **ORM**: Entity Framework Core (Npgsql)
- **Auth**: JWT
- **AI**: Ollama (local Llama models)
- **Gateway**: Nginx (reverse proxy)
- **Deployment**: Docker, Docker Compose, AWS ECR
- **Remote Access**: ngrok

## 5. APIs
### 5.1 Authentication
- **POST** `/api/auth/login`
  - Request: `{ serviceNumberOrEmail, password }`
  - Response: `{ token, officerId, role }`

- **POST** `/api/auth/forgot-password`
  - Request: `{ serviceNumberOrEmail }`
  - Response: Stub

### 5.2 HS Code Scan
- **POST** `/api/hscode/scan`
  - Content-Type: `multipart/form-data`
  - Fields:
    - `description` (optional string)
    - `file` (optional file: PDF/JPEG/PNG)
  - Response: `202 Accepted { jobId }`

- **GET** `/api/hscode/scan/{jobId}`
  - Response:
    - `{ status: "pending" }`
    - `{ status: "completed", result: { matches, note, recentHsCodes } }`
    - `{ status: "failed", error }`

- **GET** `/api/hscode/recent`
  - Response: List of last 10 HS codes and descriptions

### 5.3 Integration API (Main System)
- **POST** `/api/integrations/hscode/scan`
  - Content-Type: `application/json`
  - Request body:
    - `requestId` (string, optional)
    - `description` (string, optional)
    - `imageBase64` (string, optional)
    - `sourceSystem` (string, optional)
  - Response:
    - `{ status: "completed", matches, note }`
    - `{ status: "needs_more_detail", message }`
    - `{ status: "rejected", message }`

## 6. HS Code Output Structure
Each match includes:
- **HS Code**
- **Description**
- **Match %**
- **Comment**
- **Subsections** (HS code, title, notes)

## 7. Input Validation
- Description must be specific (length and word count check).
- Allowed files: PDF, JPEG, PNG.
- Image clarity enforcement (size threshold).
- Non-goods queries are rejected with a polite guidance message.

## 8. Security
- JWT-based authentication for officers.
- Password hashing via ASP.NET Core Identity hasher.
- Configurable secrets in appsettings.

## 9. Configuration
### Key environment variables
- `ConnectionStrings__PostgreSql`
- `Ollama__BaseUrl`
- `Ollama__Model`
- `Ollama__TextModel`
- `Ollama__TimeoutSeconds`

## 10. Deployment (Docker)
- **Backend** on port 8080
- **Frontend** on port 3000
- **Gateway** on port 8088
- **Postgres** on internal port 5432
- **ngrok** tunnels to 8088 for remote access

## 11. Known Constraints
- Long model inference times on large images.
- Async polling required for stable UX over ngrok.

## 12. Future Enhancements
- Persistent scan history in DB.
- Role-based access control for administrators.
- Multi-model fallback strategy.
- Exportable audit logs.
