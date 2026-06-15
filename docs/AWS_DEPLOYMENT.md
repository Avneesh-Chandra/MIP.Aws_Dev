# AWS deployment — MIP.Aws

This guide describes deploying MIP.Aws to Amazon Web Services. The application is container-ready and uses AWS SDK integrations for storage, email, AI recovery, and secrets.

## Architecture

```
                    ┌─────────────────┐
                    │   Route 53 /    │
                    │   ALB (HTTPS)   │
                    └────────┬────────┘
                             │
              ┌──────────────┴──────────────┐
              │     ECS Fargate Service      │
              │  MIP.Aws.Api (port 8080)     │
              │  Blazor + API + Hangfire     │
              │  Playwright (downloads)      │
              └──────────────┬──────────────┘
                             │
     ┌───────────────────────┼───────────────────────┐
     │                       │                       │
┌────▼────┐           ┌──────▼──────┐         ┌──────▼──────┐
│   RDS   │           │     S3      │         │    SES      │
│ SQL Srv │           │ PDF storage │         │ status mail │
└─────────┘           └─────────────┘         └─────────────┘
                             │
                    ┌────────▼────────┐
                    │ Secrets Manager │
                    │ JWT, DB, portal │
                    └─────────────────┘
                             │
                    ┌────────▼────────┐
                    │   CloudWatch    │
                    │  Logs / metrics │
                    └─────────────────┘
```

## Prerequisites

- AWS account with permissions for ECS, ECR, RDS, S3, SES, Secrets Manager, IAM, VPC
- [Terraform](https://developer.hashicorp.com/terraform/install) >= 1.5
- AWS CLI v2 configured (`aws configure` or environment variables)
- Docker Desktop (local image builds)
- GitHub repository secrets configured (see below)

## Quick start (Terraform)

```powershell
cd D:\MIPaws
copy infra\terraform\terraform.tfvars.example infra\terraform\terraform.tfvars
# Edit terraform.tfvars — NEVER commit this file

.\scripts\aws-login.ps1
.\scripts\build-images.ps1
.\scripts\terraform-plan.ps1
# Review plan, then:
.\scripts\terraform-apply.ps1
.\scripts\push-ecr.ps1
.\scripts\update-ecs.ps1
.\scripts\run-migrations.ps1
```

Infrastructure code: `infra/terraform/` with modules for VPC, ECS, RDS, S3, SES, ALB, IAM, CloudWatch, Secrets Manager, ECR.

## GitHub Secrets (deploy workflow)

| Secret | Purpose |
|--------|---------|
| `AWS_ACCESS_KEY_ID` | CI/CD AWS auth |
| `AWS_SECRET_ACCESS_KEY` | CI/CD AWS auth |
| `AWS_REGION` | e.g. `us-east-1` |
| `TF_VAR_db_username` | RDS master user |
| `TF_VAR_db_password` | RDS master password |
| `TF_VAR_mip_bucket_name` | Globally unique S3 bucket |
| `TF_VAR_ses_sender_email` | Verified SES sender |
| `TF_VAR_jwt_signing_key` | 32+ char JWT key |

Trigger deploy: **Actions → AWS Deploy → Run workflow** (set `apply_terraform=true` only after reviewing plan).

## Prerequisites (services)
- Verified SES sender domain or email address
- SQL Server-compatible RDS instance (or existing SQL Server)
- Container image built from `Dockerfile.Api` and pushed to ECR

## 1. RDS SQL Server

Create two databases on the same RDS instance (or separate instances):

| Database | Use |
|----------|-----|
| `MIPAws` | Application EF Core context |
| `MIPAws_Hangfire` | Hangfire job storage |

Connection string format:

```
Server=<rds-endpoint>;Database=MIPAws;User Id=<user>;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Store credentials in **AWS Secrets Manager** and inject at task startup.

Run EF migrations on first deploy:

```bash
dotnet ef database update --project src/MIP.Aws.Persistence --startup-project src/MIP.Aws.Api
```

## 2. S3 storage

Enable in configuration:

```json
"Aws": {
  "Region": "us-east-1",
  "S3": {
    "Enabled": true,
    "BucketName": "your-mip-bucket",
    "Prefix": "mip/"
  }
}
```

ECS task role needs `s3:PutObject`, `s3:GetObject`, `s3:ListBucket` on the bucket.

When S3 is disabled, PDFs are stored under `Storage:LocalRoot` (ephemeral container disk — not recommended for production).

## 3. SES mail

Verify domain or sender email in SES. Configure:

```json
"Aws": {
  "Ses": {
    "Enabled": true,
    "SenderEmail": "noreply@your-domain.example",
    "ConfigurationSet": "mip-status"
  }
},
"MailAutomation": {
  "Enabled": true
},
"PdfEditionScheduler": {
  "StatusEmailEnabled": true,
  "StatusEmailRecipient": "ops@your-domain.example",
  "AdminPortalUrl": "https://api.your-domain.example"
}
```

ECS task role needs `ses:SendEmail` (or `ses:SendRawEmail`).

## 4. Secrets Manager

Recommended secrets (prefix `mip/`):

| Secret key | Content |
|------------|---------|
| `mip/jwt-signing-key` | 32+ character symmetric key |
| `mip/db-default` | DefaultConnection string |
| `mip/db-hangfire` | Hangfire connection string |
| `mip/pressreader` | JSON `{ "Username": "...", "Password": "..." }` |

Enable in configuration:

```json
"Aws": {
  "SecretsManager": {
    "Enabled": true,
    "Prefix": "mip/"
  }
}
```

ECS task role needs `secretsmanager:GetSecretValue` on the secret ARNs.

## 5. ECS Fargate

### Task definition (API)

| Setting | Value |
|---------|-------|
| Image | `/<account>.dkr.ecr.<region>.amazonaws.com/mip-aws-api:latest` |
| CPU / Memory | 2 vCPU / 4 GB minimum (Playwright) |
| Port | 8080 |
| Health check | `GET /health` |

### Environment variables

```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
Aws__Region=us-east-1
Aws__S3__Enabled=true
Aws__S3__BucketName=your-bucket
Aws__Ses__Enabled=true
Aws__Ses__SenderEmail=noreply@your-domain.example
ConnectionStrings__DefaultConnection=<from Secrets Manager>
ConnectionStrings__Hangfire=<from Secrets Manager>
Jwt__SigningKey=<from Secrets Manager>
```

Use ECS secrets integration to map Secrets Manager ARNs to environment variables.

### IAM task role

- S3 read/write on PDF bucket
- SES send
- Secrets Manager read
- CloudWatch Logs write

### Networking

- Place tasks in private subnets
- ALB in public subnets with HTTPS listener
- Security group: ALB → task on 8080; task → RDS on 1433

## 6. CloudWatch

- ECS task logs → CloudWatch Logs group `/ecs/mip-aws-api`
- Optional: Container Insights for CPU/memory
- Alarms on `/health` failures and Hangfire job failure rate

## 7. Auto AI Recovery (Bedrock)

Optional — enable when deploying AI-assisted selector recovery:

```json
"Aws": {
  "Bedrock": {
    "Enabled": true,
    "ModelId": "anthropic.claude-3-5-sonnet-20241022-v2:0",
    "Region": "us-east-1"
  }
},
"AutoAiDownloadRecovery": {
  "Enabled": true
}
```

Task role needs `bedrock:InvokeModel`.

## 8. Hangfire and downloads

The API host runs Hangfire with the `downloads` queue for Playwright portal jobs. Ensure:

- Only the API service listens on `downloads` (not a separate worker without Playwright)
- `HangfireQueues:Queues` includes `downloads`, `critical`, `default`, etc.

## 9. Deployment checklist

- [ ] RDS databases created and migrated
- [ ] S3 bucket created with lifecycle policy
- [ ] SES domain verified, recipient allow-listed (if sandbox)
- [ ] Secrets Manager populated
- [ ] ECR image built and pushed (`Dockerfile.Api`)
- [ ] ECS service healthy (`/health` returns 200)
- [ ] `NewspaperCatalog:SeedOnStartup` run once, then set `false` if desired
- [ ] PressReader credentials configured via PDF management UI
- [ ] `IsDownloadAllowed` enabled per licensed source after compliance approval
- [ ] Test download monitor batch and status email

## 10. CI/CD

GitHub Actions workflow `.github/workflows/aws-build.yml` builds and tests on push. Wire your own deploy step to push to ECR and update ECS — **not included** to avoid accidental production deploys.

## Local parity

Use `appsettings.Development.json` (gitignored) for LocalDB. See [LOCAL_SETUP.md](LOCAL_SETUP.md).
