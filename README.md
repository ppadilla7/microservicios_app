# Project Login – Docker Setup

This repo contains a Vue 3 client (Vite) and a .NET 8 API with EF Core and RBAC/MFA. This README explains how to run the full stack with Docker.

## Services
- `server` – ASP.NET Core API (`/server`), exposed at `http://localhost:5096`.
- `client` – Vue 3 built app served by Nginx (`/client`), exposed at `http://localhost:8080`.
- `db` – SQL Server 2022 container, exposed at `localhost:1433`.

## Prerequisites
- Docker and Docker Compose installed.
- Ports `5096`, `8080`, and `1433` available on your machine.

## First Run
1. Build and start all services:
   - `docker compose up -d --build`
2. Open the client:
   - `http://localhost:8080`
3. API Swagger (if enabled):
   - `http://localhost:5096/swagger`

The API automatically applies EF Core migrations and seeds RBAC resources on startup.

## Email (Mailtrap)
- The `notifications` service sends emails via Mailtrap SMTP.
- Set `MAILTRAP_USER` and `MAILTRAP_PASS` before starting Compose to avoid blank credentials:
  - Create a `.env` file in the repo root based on `.env.example`:
    - `MAILTRAP_USER=your_mailtrap_username`
    - `MAILTRAP_PASS=your_mailtrap_password`
  - Or export them in PowerShell for the current session:
    - ``$env:MAILTRAP_USER="<username>" ; $env:MAILTRAP_PASS="<password>"``
  - Then restart notifications:
    - ``docker compose up -d notifications``

### Debugging delivery
- RabbitMQ UI: `http://localhost:15672` (guest/guest). Check exchange `university.events` and queue `notifications.enrollment` bound to `enrollment.created`.
- Trigger an event by creating an enrollment:
  - ``Invoke-RestMethod -Method Post -Uri http://localhost:5281/api/enrollments -ContentType 'application/json' -Body '{"studentId":"<guid>","courseId":"<guid>"}'``
- Service logs:
  - ``docker compose logs --tail=200 notifications``
  - You should see a line like: `Evento recibido: enrollment.created ...` and then email send status.

## Configuration
### Database Connection
The API reads `ConnectionStrings:Default` from configuration. In Compose it's overridden via env:

```
ConnectionStrings__Default=Server=db;Database=securitydb;User Id=sa;Password=Your_password123;TrustServerCertificate=True
```

You can connect to the DB locally using `localhost:1433` and `sa/Your_password123`.

### CORS
The API currently allows origins `http://127.0.0.1:5174/5175/5176` (dev). For Docker client (`http://localhost:8080`), add these origins in `Program.cs` if you plan to use the containerized client:
- `http://localhost:8080`
- `http://127.0.0.1:8080`

### OAuth Callback URLs
If you use Google/GitHub OAuth with the containerized client, update `appsettings.json`:
- `OAuth:Google:CallbackUrl`: `http://localhost:8080/auth/callback`
- `OAuth:GitHub:CallbackUrl`: `http://localhost:8080/auth/callback`

Otherwise, if you keep using the dev client (`vite`), leave them pointing to the dev port.

## MFA Notes
- Client calls API at `http://localhost:5096`.
- MFA setup endpoints:
  - `POST /auth/mfa/setup` (with `Authorization: Bearer <token>`)
  - `POST /auth/mfa/setup/pending` (with `pendingToken`)
- For QR generation in the client, avoid external QR services in production. Prefer local generation (e.g., npm `qrcode`).

## Dockerfiles
- `server/Dockerfile`: multi-stage build for .NET 8, exposes `5096`.
- `client/Dockerfile`: builds Vue app with Node 20 and serves with Nginx.
- `client/nginx.conf`: SPA fallback config.

## Common Commands
- Start: `docker compose up -d`
- Stop: `docker compose down`
- Rebuild: `docker compose up -d --build`
- Logs: `docker compose logs -f server`

## Troubleshooting
- DB not reachable: ensure the `db` service is healthy (`docker compose ps`) and the API env `ConnectionStrings__Default` points to `Server=db;...`.
- CORS errors: add `http://localhost:8080` to allowed origins in `Program.cs`.
- OAuth redirects fail: update callback URLs in `appsettings.json` to the client port you use (Docker `8080` or dev `5174`).

## Dev vs Docker
- Dev client runs on Vite (`npm run dev`) ports `5174/5175/5176`.
- Docker client runs on Nginx `http://localhost:8080`.
- API stays on `http://localhost:5096` in both cases.