# MIP.Aws

AWS-ready edition of GFH Media Intelligence — newspaper PDF acquisition, licensed PressReader portals, operator supervision, and automated recovery. **Standalone repository** with no dependency on the original GFH.MediaIntelligence project.

## Modules

| Module | Description |
|--------|-------------|
| **PDF Download Engine** | Public PDF discovery and licensed PressReader portal downloads (Playwright) |
| **Auto AI Recovery** | Automatic selector/config recovery when downloads fail |
| **Download Monitor** | Operator dashboard for daily acquisition status and batch runs |
| **Email Scheduler** | Daily status email after staggered batch completion (AWS SES) |

## Solution

| Project | Role |
|---------|------|
| `MIP.Aws.Api` | ASP.NET Core host — REST API, Blazor UI, Hangfire server |
| `MIP.Aws.Blazor` | Razor Class Library — Download Monitor, PDF management UI |
| `MIP.Aws.Worker` | Background worker scaffold (extend for dedicated Hangfire processing) |
| `MIP.Aws.Application` | MediatR features, configuration, abstractions |
| `MIP.Aws.Domain` | Entities, enums, security policies |
| `MIP.Aws.Infrastructure` | AWS (S3, SES, Bedrock, Secrets Manager), Playwright, jobs |
| `MIP.Aws.Persistence` | EF Core, Identity seed, newspaper catalog seed |
| `MIP.Aws.Shared` | Shared DTOs and responses |
| `MIP.Aws.Tests` | Unit tests |

Open `MIP.Aws.slnx` in Visual Studio 2022 or build with the .NET 10 SDK.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB for development)
- Playwright Chromium (installed on first PDF download)

## Local setup

1. Clone the repository.
2. Copy configuration template:
   ```powershell
   copy src\MIP.Aws.Api\appsettings.Template.json src\MIP.Aws.Api\appsettings.Development.json
   ```
3. Edit `appsettings.Development.json` — set `Jwt:SigningKey` (32+ chars), connection strings, and optional dev seed users. This file is **gitignored**.
4. Build and run:
   ```powershell
   dotnet build MIP.Aws.slnx
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet run --project src\MIP.Aws.Api\MIP.Aws.Api.csproj --urls "http://localhost:5196"
   ```

See [docs/LOCAL_SETUP.md](docs/LOCAL_SETUP.md) for URLs, seed users, and storage paths.

## Database setup

| Database | Purpose |
|----------|---------|
| `MIPAws` | Application data (sources, downloads, PDF editions, users) |
| `MIPAws_Hangfire` | Hangfire job storage |

Databases are created automatically on first API start in Development (LocalDB). For production, use **Amazon RDS for SQL Server** — see [docs/AWS_DEPLOYMENT.md](docs/AWS_DEPLOYMENT.md).

## Docker setup

```powershell
docker compose build
docker compose up api
```

- API: http://localhost:8080
- Requires external SQL Server or RDS connection strings via environment variables.

See `Dockerfile.Api`, `Dockerfile.Worker`, and `docker-compose.yml`.

## AWS deployment overview

Target architecture (Terraform in `infra/terraform/`):

- **ECS Fargate** — API container (Blazor + Hangfire + Playwright)
- **RDS SQL Server** — app + Hangfire databases
- **S3** — PDF and artifact storage (`AwsS3FileStorageService`)
- **SES** — download monitor status email (`AwsSesEmailSender`)
- **Secrets Manager** — JWT signing key, DB credentials, PressReader credentials
- **CloudWatch** — logs and metrics

| Guide | Purpose |
|-------|---------|
| [docs/GFH-MediaIntelligence-Deployment-Requirements.docx](docs/GFH-MediaIntelligence-Deployment-Requirements.docx) | Client deployment brief — infrastructure, hardware & licensing (AWS) |
| [docs/GFH_MIP_AWS_DEPLOYMENT_REQUIREMENTS.md](docs/GFH_MIP_AWS_DEPLOYMENT_REQUIREMENTS.md) | Technical deployment requirements (AWS) |
| [docs/AWS_DEPLOYMENT.md](docs/AWS_DEPLOYMENT.md) | Step-by-step deployment |
| [docs/AWS_DATABASE_MIGRATIONS.md](docs/AWS_DATABASE_MIGRATIONS.md) | EF migrations on RDS |
| [docs/AWS_COST_CONTROL.md](docs/AWS_COST_CONTROL.md) | Low-cost defaults |
| [docs/AWS_SECURITY.md](docs/AWS_SECURITY.md) | IAM, secrets, network |

### GitHub Actions

- `aws-ci.yml` — build, test, Terraform validate
- `aws-deploy.yml` — manual ECR push + Terraform (workflow_dispatch)

### Required GitHub Secrets

`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`, `TF_VAR_db_username`, `TF_VAR_db_password`, `TF_VAR_mip_bucket_name`, `TF_VAR_ses_sender_email`, `TF_VAR_jwt_signing_key`

### Deploy scripts

`scripts/aws-login.ps1`, `build-images.ps1`, `push-ecr.ps1`, `terraform-plan.ps1`, `terraform-apply.ps1`, `update-ecs.ps1`, `run-migrations.ps1`

## Configuration

| File | Committed | Purpose |
|------|-----------|---------|
| `appsettings.json` | Yes | Safe defaults and structure (no secrets) |
| `appsettings.Template.json` | Yes | Deployment template with placeholders |
| `appsettings.Development.json` | **No** (gitignored) | Local secrets and dev users |

Never commit AWS credentials, JWT signing keys, database passwords, or PressReader subscriber credentials.

## CI

GitHub Actions workflow `.github/workflows/aws-build.yml` builds the solution, runs tests, and publishes artifacts. **No automatic deployment.**

## License

Proprietary — GFH / Almoayyed Computers.
