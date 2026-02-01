# NCS HS Code Intelligence Tool – Administrative Document

## 1. Purpose
The NCS HS Code Intelligence Tool provides officers with an AI-assisted platform for HS code classification. It improves consistency, reduces manual lookup effort, and supports faster decision-making.

## 2. Target Users
- Customs officers performing HS code classification.
- Supervisors overseeing classification decisions.
- ICT/Admin staff supporting system availability.

## 3. Key Capabilities
- **HS Code Scan**
  - Input a detailed description and/or image.
  - Receive top HS code matches with confidence level.
  - View detailed subsections and notes.

- **Recent Scans Summary**
  - Shows last 10 scans for quick reference.

- **Officer Login**
  - Secure access through JWT authentication.

- **Remote Access**
  - System can be exposed securely using ngrok for demonstrations or remote use.

## 4. Functional Workflow
1. Officer opens the portal.
2. Clicks “Start HS Code Scan.”
3. Enters item description or uses camera/image upload.
4. System validates input and submits to AI engine.
5. Results displayed with match percentage and action to view details.

## 5. System Value
- Reduces human error in HS code classification.
- Provides structured results with supporting details.
- Enhances transparency for supervisory review.

## 6. Administration & Maintenance
- **User Management**: Officers are stored in the database; passwords are hashed.
- **Configuration**: Models, DB, and JWT settings are configurable in environment variables.
- **Uptime**: Dockerized deployment simplifies start/stop and upgrades.
- **Monitoring**: Logs available for backend, gateway, and model service.

## 7. Limitations & Considerations
- AI output is advisory and requires officer judgment.
- Results depend on the specificity of the description and image clarity.
- Heavy image processing can increase response time.

## 8. Slide Presentation Outline
Suggested slide structure:
1. **Title** – NCS HS Code Intelligence Tool
2. **Problem Statement** – Challenges with manual HS code classification
3. **Solution Overview** – AI-assisted classification system
4. **System Architecture** – Frontend, backend, AI model, database
5. **User Workflow** – Step-by-step scan process
6. **Key Features** – Scan, details, recent summary, security
7. **Benefits** – Speed, accuracy, consistency
8. **Operational Requirements** – Local Llama, Docker, PostgreSQL
9. **Roadmap** – Planned enhancements
10. **Closing** – Next steps and adoption

## 9. Summary
The NCS HS Code Intelligence Tool is a practical, scalable solution for HS code classification across NCS operations. It provides fast access to structured HS code guidance and ensures officers can make informed decisions with AI support.
