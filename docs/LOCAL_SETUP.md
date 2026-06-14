# MIP AWS — Local development

Standalone stack. **No dependency** on the original MIP repo or its database.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB
- Playwright Chromium (installed on first PDF download run)

## Configuration

`appsettings.Development.json` is gitignored. Create it from the template:

```powershell
copy src\MIP.Aws.Api\appsettings.Template.json src\MIP.Aws.Api\appsettings.Development.json
```

Then set at minimum:

- `Jwt:SigningKey` — 32+ characters
- `ConnectionStrings:DefaultConnection` and `Hangfire` — LocalDB (see template)
- `IdentitySeed:DefaultAdminPassword` and `DevelopmentUsers` — for local login accounts

## Databases

| Catalog | Connection |
|---------|------------|
| App | `(localdb)\mssqllocaldb` → **MIPAws** |
| Hangfire | `(localdb)\mssqllocaldb` → **MIPAws_Hangfire** |

Created automatically on first API start in Development.

## Run the API (includes Download Monitor UI)

```powershell
cd <repo-root>
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src\MIP.Aws.Api\MIP.Aws.Api.csproj --urls "http://localhost:5196"
```

- **Login / home:** http://localhost:5196/ (redirects to login)
- **Download Monitor:** http://localhost:5196/operator/download-monitor
- **PDF management:** http://localhost:5196/admin/pdf-management (SuperAdmin / ContentAdmin — UAE PressReader editions seeded on startup)
- Swagger: http://localhost:5196/swagger
- Hangfire: http://localhost:5196/hangfire (SuperAdmin cookie session)
- Health: http://localhost:5196/health

The Blazor UI is hosted inside the API on port **5196** (not a separate Blazor process).

## Seed users (Development)

Configured in `appsettings.Development.json` under `IdentitySeed`. Default local accounts use the `@mip.local` domain — see your local Development file for emails and passwords.

## Login (API)

```powershell
$body = @{ email = '<your-dev-email>'; password = '<your-dev-password>' } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5196/api/v1/auth/login -Method POST -Body $body -ContentType 'application/json'
```

## Storage

PDFs and artifacts: configure `Storage:LocalRoot` in `appsettings.Development.json` (default `./storage` relative to the API working directory).

## Build

```powershell
dotnet build MIP.Aws.slnx
dotnet test MIP.Aws.slnx
```
