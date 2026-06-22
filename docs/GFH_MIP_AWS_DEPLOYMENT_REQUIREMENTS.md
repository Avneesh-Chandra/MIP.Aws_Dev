# GFH Media Intelligence Platform — Deployment Requirements

**Infrastructure, Hardware & Licensing (AWS)**

| Field | Value |
|-------|--------|
| **Product** | GFH Media Intelligence Platform (MIP.Aws) |
| **Repository** | `D:\MIPaws` / [MIP.Aws_Dev](https://github.com/Avneesh-Chandra/MIP.Aws_Dev) |
| **Document version** | 1.0 |
| **Target cloud** | Amazon Web Services (AWS) |
| **IaC** | Terraform (`infra/terraform/`) |
| **Classification** | Internal — GFH / Almoayyed Computers |

---

## 1. Purpose and scope

This document defines the **infrastructure, hardware (compute/storage), software, and licensing** requirements to deploy and operate **MIP.Aws** on AWS. It is intended for IT operations, cloud architects, compliance reviewers, and project stakeholders planning dev, staging, or production rollouts.

MIP.Aws is the AWS-ready edition of GFH Media Intelligence. It provides:

- Licensed and public newspaper PDF acquisition (including PressReader subscriber portals)
- Operator download monitoring and batch scheduling
- Automated AI-assisted source recovery (Amazon Bedrock)
- Status email notifications (Amazon SES)
- Blazor admin UI and REST API in a single containerized host

This document does **not** replace step-by-step runbooks. See also:

- [AWS_DEPLOYMENT.md](./AWS_DEPLOYMENT.md) — deployment procedures
- [AWS_SECURITY.md](./AWS_SECURITY.md) — security controls
- [AWS_COST_CONTROL.md](./AWS_COST_CONTROL.md) — cost defaults and optimization
- [AWS_BEDROCK_AI.md](./AWS_BEDROCK_AI.md) — AI model access and configuration

---

## 2. Architecture summary

```
                         ┌─────────────────────┐
                         │  Route 53 (optional) │
                         │  ACM certificate     │
                         └──────────┬──────────┘
                                    │ HTTPS (recommended)
                         ┌──────────▼──────────┐
                         │ Application Load     │
                         │ Balancer (ALB)         │
                         └──────────┬──────────┘
                                    │ :8080
              ┌─────────────────────▼─────────────────────┐
              │ ECS Fargate — MIP.Aws.Api                  │
              │ ASP.NET Core 10 · Blazor · Hangfire        │
              │ Playwright Chromium (portal downloads)     │
              └──────┬──────────────┬──────────────┬───────┘
                     │              │              │
            ┌────────▼────┐  ┌──────▼──────┐ ┌─────▼─────┐
            │ RDS SQL     │  │ S3 bucket   │ │ Amazon SES │
            │ Server      │  │ PDF storage │ │ status mail│
            └─────────────┘  └─────────────┘ └───────────┘
                     │
            ┌────────▼────────┐        ┌──────────────────┐
            │ Secrets Manager│        │ Amazon Bedrock   │
            │ JWT · DB · keys│        │ AI recovery      │
            └────────────────┘        └──────────────────┘
                     │
            ┌────────▼────────┐
            │ CloudWatch Logs │
            └─────────────────┘
```

**Container registry:** Amazon ECR (`mip-aws-{env}-api`, `mip-aws-{env}-worker`)

**Optional worker service:** `MIP.Aws.Worker` ECS task (scaffold; Hangfire download jobs run on the API container today).

---

## 3. AWS account requirements

### 3.1 Account and region

| Requirement | Detail |
|-------------|--------|
| AWS account | Dedicated account or OU segment recommended for production |
| Primary region | Configurable; **current dev deployment uses `eu-north-1` (Stockholm)** |
| Bedrock region | May differ from primary region (default `eu-north-1`; model access is per-region) |
| Billing alerts | AWS Budgets recommended before first Terraform apply |

### 3.2 IAM permissions (deployment principal)

The CI/CD user or role used for Terraform and GitHub Actions requires permissions to manage at minimum:

| Service | Purpose |
|---------|---------|
| ECS, ECR | Container orchestration and images |
| RDS | SQL Server database |
| S3 | PDF and artifact storage |
| SES | Email sending and identity verification |
| Secrets Manager | Runtime secrets |
| IAM | ECS task and execution roles |
| VPC, EC2 (networking) | Subnets, security groups, optional NAT |
| ELB (ALB) | Public HTTP/HTTPS entry |
| CloudWatch Logs | Application and Hangfire logs |
| Bedrock | `InvokeModel` (if AI recovery enabled) |

**Principle:** Application containers use **IAM task roles only** — no long-lived AWS access keys inside containers.

### 3.3 Service quotas

Confirm quotas for:

- ECS Fargate vCPU and memory in target region
- RDS SQL Server instances (Express/Web editions)
- SES sending limits (sandbox vs production)
- Bedrock model access (must be enabled in console per model)

---

## 4. Infrastructure components

All components below are provisioned by Terraform modules under `infra/terraform/modules/`.

| Component | AWS service | Module | Notes |
|-----------|-------------|--------|-------|
| Network | VPC | `vpc` | Default CIDR `10.0.0.0/16`; public + private subnets |
| Ingress | ALB | `alb` | HTTP listener (port 80); HTTPS via ACM is a future enhancement |
| Compute | ECS Fargate | `ecs` | API + optional Worker services |
| Images | ECR | `ecr` | API (~1.2 GB with Playwright); Worker (smaller) |
| Database | RDS SQL Server | `rds-sqlserver` | Engine 15.x; license-included model |
| Object storage | S3 | `s3` | Versioning, SSE-S3, lifecycle rules |
| Email | SES | `ses` | Sender identity verification required |
| Secrets | Secrets Manager | `secrets-manager` | JWT signing key, connection metadata |
| Identity / access | IAM | `iam` | Least-privilege ECS roles |
| Observability | CloudWatch Logs | `cloudwatch` | API, worker, Hangfire log groups |

### 4.1 Network layout

| Tier | Placement | Default (dev) |
|------|-----------|----------------|
| ALB | Public subnets | Internet-facing |
| ECS tasks | Public subnets **or** private + NAT | Public subnets when `enable_nat_gateway = false` |
| RDS | Private subnets | Not publicly accessible |

**Security groups:**

- ALB: inbound TCP 80 from allowed CIDR (default `0.0.0.0/0` — restrict for production)
- ECS: inbound TCP 8080 from ALB security group only
- RDS: inbound TCP 1433 from ECS security group only

### 4.2 DNS and TLS (production recommendation)

| Item | Dev (current) | Production target |
|------|---------------|-------------------|
| DNS | ALB DNS name | Route 53 hosted zone |
| TLS | HTTP only | ACM certificate on ALB (443) |
| Cookie auth | `Auth__UseHttpsCookies=false` workaround over HTTP | HTTPS + `Auth__UseHttpsCookies=true` |
| Admin portal URL | ALB HTTP URL | `https://api.<domain>` |

---

## 5. Hardware and compute requirements

AWS Fargate abstracts physical hardware. Requirements are expressed as **vCPU, memory, storage, and concurrency**.

### 5.1 ECS API task (primary workload)

Hosts: Blazor UI, REST API, Hangfire server, Playwright browser automation, PDF processing orchestration.

| Profile | vCPU | Memory | Tasks | Use case |
|---------|------|--------|-------|----------|
| **Dev / test (Terraform default)** | 0.25 (256 units) | 1 GB (1024 MiB) | 1 | Lowest cost; may be tight for concurrent Playwright jobs |
| **Recommended minimum (Playwright)** | 1–2 | 2–4 GB | 1 | Stable portal downloads and batch runs |
| **Production** | 2+ | 4–8 GB | 2+ (behind ALB) | HA, parallel download queues |

**Container image characteristics:**

- Base: `mcr.microsoft.com/dotnet/aspnet:10.0`
- Bundled: Playwright Chromium + system dependencies (~1 GB+ image)
- Port: `8080`
- Health check: `GET /health/live`

**Ephemeral disk:** Fargate provides limited container scratch space. **Do not** rely on local disk for PDF retention — S3 is required in production (`Storage__Provider=S3`).

### 5.2 ECS Worker task (optional)

| Profile | vCPU | Memory | Default count |
|---------|------|--------|---------------|
| Dev | 0.25 | 512 MiB | 0–1 (often **0** — downloads run on API) |
| Production | 0.5–1 | 1–2 GB | Scale when background processing is split from API |

### 5.3 RDS SQL Server

| Setting | Dev default | Production guidance |
|---------|-------------|---------------------|
| Engine | `sqlserver-ex` (Express) | `sqlserver-web` or higher for larger catalogs |
| Instance class | `db.t3.small` | `db.t3.medium` or larger under load |
| Storage | 20 GB gp3 | Scale with PDF metadata growth; monitor free space |
| Backups | 1 day retention | 7–35 days for production |
| Encryption | Review module | Enable encryption at rest |
| Multi-AZ | Not in default module | Recommended for production |

**Database layout:**

| Database | Purpose |
|----------|---------|
| `MIPAws` | Application data (sources, downloads, users, mail settings) |
| Hangfire | Job storage — **on RDS Express, shares `MIPAws` database** (single user DB limit) |

### 5.4 S3 storage

| Item | Requirement |
|------|-------------|
| Bucket | Globally unique name (`mip_bucket_name` Terraform variable) |
| Prefix | `mip/` (configurable) |
| Encryption | SSE-S3 (AES-256) |
| Versioning | Enabled |
| Lifecycle | Transition to Standard-IA at 30 days; expire at 365 days (module default) |
| Capacity planning | Estimate ~5–50 MB per newspaper PDF × retention period × number of titles |

**Example:** 20 titles × daily editions × 30 MB × 365 days ≈ **200+ GB/year** before lifecycle expiration.

### 5.5 Build and CI infrastructure

| Environment | Requirement |
|-------------|-------------|
| GitHub Actions | `ubuntu-latest` runner; Docker for image build (~20–30 min per full API image) |
| Developer workstation | .NET 10 SDK, Docker Desktop (optional local builds), AWS CLI v2, Terraform ≥ 1.5 |
| Disk (local Docker builds) | **≥ 30 GB free** on Docker data volume (Playwright layers); recommend dedicated drive |

---

## 6. Software and runtime requirements

### 6.1 Application stack

| Component | Version / technology |
|-----------|---------------------|
| Runtime | .NET 10 |
| Web host | ASP.NET Core (`MIP.Aws.Api`) |
| UI | Blazor Server (MudBlazor) |
| ORM | Entity Framework Core |
| Job scheduler | Hangfire (SQL Server storage) |
| Browser automation | Playwright (Chromium) |
| PDF / document | QuestPDF, ClosedXML (reports/exports where applicable) |

### 6.2 AWS SDK integrations

| Integration | Interface | When enabled |
|-------------|-----------|--------------|
| S3 | `AwsS3FileStorageService` | `Aws:S3:Enabled=true` |
| SES | `AwsSesEmailSender` | `Aws:Ses:Enabled=true` |
| Bedrock | `AwsBedrockAiProvider` | `Aws:Bedrock:Enabled=true` |
| Secrets Manager | Configuration provider | `Aws:SecretsManager:Enabled=true` |

### 6.3 External connectivity (egress)

ECS tasks require outbound HTTPS to:

- PressReader and other licensed publisher portals
- Public newspaper PDF hosts (per source configuration)
- AWS service endpoints (S3, SES, Secrets Manager, Bedrock, CloudWatch, ECR)
- Optional: external SMTP is **not** used when SES is enabled

---

## 7. Licensing requirements

### 7.1 GFH proprietary software

| Item | Requirement |
|------|-------------|
| MIP.Aws codebase | **Proprietary — GFH / Almoayyed Computers** |
| Deployment rights | Internal GFH use or as covered by customer agreement |
| Source repository | `MIP.Aws_Dev` (standalone; no dependency on legacy Azure MIP repo) |

### 7.2 Newspaper and portal content licensing

| License type | Owner | Platform obligation |
|--------------|-------|---------------------|
| **PressReader subscriber access** | GFH / publisher agreement | Valid subscriber username and password per portal; stored encrypted, never in git |
| **Per-source download permission** | GFH compliance | `IsDownloadAllowed` must be enabled only after compliance approval per source |
| **Public PDF editions** | Publisher terms | Respect robots.txt, rate limits, and contractual use |

**Operational controls in software:**

- Portal credentials via admin UI / Secrets Manager (`mip/pressreader`)
- AI recovery prompts explicitly avoid suggesting licensing or credential changes
- Download monitor provides operator supervision before automated batches

### 7.3 Third-party and open-source components

| Component | License model | Notes |
|-----------|---------------|-------|
| .NET / ASP.NET Core | MIT (Microsoft) | Runtime included in container base image |
| Playwright | Apache 2.0 | Chromium binaries bundled in container |
| MudBlazor | MIT | UI components |
| Hangfire | LGPL / commercial options | Job scheduling |
| Terraform AWS provider | MPL 2.0 | Infrastructure automation |

No separate commercial license is required for the above in a typical internal deployment; verify Hangfire usage against GFH legal policy for production.

### 7.4 AWS service licensing

| AWS service | Licensing / billing model |
|-------------|---------------------------|
| **RDS SQL Server Express** | `license-included` in Terraform — AWS provides SQL Server license embedded in instance price |
| **RDS SQL Server Web** | Alternative engine (`db_engine = sqlserver-web`) for higher limits — higher cost |
| **ECS Fargate** | Per vCPU-hour and GB-hour |
| **S3** | Per GB stored + requests + lifecycle transitions |
| **SES** | Per email sent; sandbox free for verified addresses only until production access |
| **Bedrock** | Per model token / request (model-specific pricing) |
| **Secrets Manager** | Per secret per month + API calls |
| **CloudWatch Logs** | Per GB ingested and stored |
| **ALB** | Per hour + LCU usage |
| **NAT Gateway** | Optional (~$32+/month per AZ if enabled) |

### 7.5 Amazon Bedrock model access

| Requirement | Detail |
|-------------|--------|
| Console activation | Model access must be requested in Bedrock console per region |
| Default model (dev) | `amazon.nova-lite-v1:0` in `eu-north-1` |
| IAM | `bedrock:InvokeModel` on ECS task role |
| Data handling | Article text sent to Bedrock for recovery/diagnosis — review against GFH data policy |

---

## 8. Configuration and secrets

### 8.1 Terraform variables (infrastructure)

Copy `infra/terraform/terraform.tfvars.example` → `terraform.tfvars` (**never commit**).

| Variable | Required | Description |
|----------|----------|-------------|
| `aws_region` | Yes | e.g. `eu-north-1` |
| `mip_bucket_name` | Yes | Globally unique S3 bucket |
| `db_username` / `db_password` | Yes | RDS master credentials |
| `jwt_signing_key` | Yes | Minimum 32 characters |
| `ses_sender_email` | Yes | Verified SES sender |
| `status_email_recipient` | Recommended | Daily status email recipient |
| `admin_portal_url` | Recommended | Link in status emails |
| `identity_default_admin_password` | Yes (first deploy) | Initial SuperAdmin password |
| `enable_bedrock` | Optional | Default `true` |
| `bedrock_model_id` | Optional | Default `amazon.nova-lite-v1:0` |
| `api_cpu` / `api_memory` | Optional | Scale compute |
| `enable_nat_gateway` | Optional | Default `false` (cost saving) |

### 8.2 GitHub Actions secrets (CI/CD)

**Repository secrets** (for `aws-deploy.yml` image-only deploy):

| Secret | Required for image deploy |
|--------|---------------------------|
| `AWS_ACCESS_KEY_ID` | Yes |
| `AWS_SECRET_ACCESS_KEY` | Yes |

**Additional secrets** (when `apply_terraform=true`):

| Secret | Purpose |
|--------|---------|
| `TF_VAR_db_username` | RDS user |
| `TF_VAR_db_password` | RDS password |
| `TF_VAR_mip_bucket_name` | S3 bucket name |
| `TF_VAR_ses_sender_email` | SES sender |
| `TF_VAR_jwt_signing_key` | JWT key |

Helper script: `scripts/set-github-aws-secrets.ps1` (uploads local `mip-dev` profile keys).

### 8.3 Secrets Manager (runtime)

| Secret path | Content |
|-------------|---------|
| `mip/jwt-signing-key` | Symmetric JWT signing key |
| `mip/db-default` | DefaultConnection (optional override) |
| `mip/db-hangfire` | Hangfire connection (optional override) |
| `mip/pressreader` | JSON credentials for licensed portals |

---

## 9. Functional deployment prerequisites

Before go-live, the following must be complete:

### 9.1 AWS platform

- [ ] VPC, ALB, ECS cluster, and RDS instance provisioned (Terraform apply)
- [ ] ECR images built from `Dockerfile.Api` (includes Playwright) and pushed
- [ ] ECS API service healthy (`/health` returns 200)
- [ ] S3 bucket accessible from task role
- [ ] SES sender email or domain **verified**
- [ ] SES production access requested (if sending to arbitrary recipients)
- [ ] Bedrock model access enabled in target region

### 9.2 Application

- [ ] EF Core migrations applied (`Database__AutoMigrateOnStartup` or `run-migrations.ps1`)
- [ ] SuperAdmin account accessible (`superadmin@mip.local` or configured email)
- [ ] Newspaper catalog seeded (`NewspaperCatalog:SeedOnStartup`)
- [ ] PressReader credentials entered in admin UI
- [ ] `IsDownloadAllowed` enabled per licensed source after compliance sign-off
- [ ] Mail settings configured (`/admin/mail-settings`)
- [ ] Test status email to operations recipient

### 9.3 Security and compliance

- [ ] No secrets in git (`terraform.tfvars`, `appsettings.Development.json` gitignored)
- [ ] JWT signing key rotated from template default
- [ ] ALB ingress restricted if required by corporate policy
- [ ] HTTPS planned or implemented for production
- [ ] CloudTrail enabled for audit (recommended)
- [ ] `IdentitySeed:SeedDevelopmentRoleUsers=false` in production

---

## 10. Deployment methods

| Method | When to use |
|--------|-------------|
| **GitHub Actions — AWS Deploy** | Recommended when local Docker is unavailable; builds on `ubuntu-latest` |
| **Local scripts** | `build-images.ps1` → `push-ecr.ps1` → `update-ecs.ps1` |
| **Terraform** | `terraform-plan.ps1` / `terraform-apply.ps1` for infrastructure changes |

**GitHub Actions inputs:**

- `apply_terraform`: `false` for image-only redeploy; `true` only after reviewing plan
- `image_tag`: typically `latest`

---

## 11. Sizing reference — dev vs production

| Resource | Dev / test | Production |
|----------|------------|------------|
| ECS API | 0.25 vCPU, 1 GB, 1 task | 2 vCPU, 4 GB, 2+ tasks |
| ECS Worker | 0 tasks | 0–2 tasks (if split from API) |
| RDS | `db.t3.small`, Express, 20 GB | `db.t3.medium`+, Web, 100+ GB |
| NAT Gateway | Off | On (if private ECS required) |
| ALB | HTTP | HTTPS + custom domain |
| SES | Sandbox | Production access |
| Bedrock | Nova Lite | Nova Pro or Claude (per policy) |
| CloudWatch retention | 14 days | 30–90 days |
| S3 lifecycle | 365-day expiry | Archive / Glacier for long retention |

See [AWS_COST_CONTROL.md](./AWS_COST_CONTROL.md) for stop/start and destroy procedures.

---

## 12. Estimated monthly cost (indicative)

Costs vary by region and usage. **Dev defaults** are designed for low spend:

| Component | Approximate dev cost driver |
|-----------|----------------------------|
| RDS SQL Server Express (`db.t3.small`) | Largest fixed cost |
| ECS Fargate (1 API task) | Moderate |
| ALB | Low–moderate |
| S3 | Low until PDF volume grows |
| SES | Minimal in sandbox |
| Bedrock | Pay per request; Nova Lite is low |
| NAT Gateway | **$0** when disabled |

Use AWS Pricing Calculator with region `eu-north-1` and above sizing for budget approval.

---

## 13. Support and operational contacts

| Area | Responsibility |
|------|----------------|
| AWS account / billing | GFH IT / cloud owner |
| PressReader licensing | GFH content / compliance |
| Application defects | MIP.Aws development team |
| Publisher download permissions | GFH compliance + operations |

---

## 14. Document history

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | June 2026 | GFH MIP.Aws project | Initial AWS deployment requirements |

---

## Appendix A — Current dev environment reference

*Reference values from the active GFH dev deployment; update when infrastructure changes.*

| Item | Value |
|------|--------|
| AWS account | `640533249094` |
| Region | `eu-north-1` |
| ECS cluster | `mip-aws-dev-cluster` |
| ALB URL | `http://mip-aws-dev-alb-839810689.eu-north-1.elb.amazonaws.com` |
| ECR API repo | `640533249094.dkr.ecr.eu-north-1.amazonaws.com/mip-aws-dev-api` |
| SES sender (verified) | `avneesh.c@almoayyedcomputers.com` |
| Bedrock model | `amazon.nova-lite-v1:0` |
| GitHub repository | `https://github.com/Avneesh-Chandra/MIP.Aws_Dev` |

## Appendix B — Related file locations

| Path | Description |
|------|-------------|
| `infra/terraform/` | Terraform root and modules |
| `Dockerfile.Api` | Production API image (Playwright) |
| `Dockerfile.Api.slim` | Mail/admin-only image (no Playwright) |
| `scripts/` | Build, push, deploy, migration scripts |
| `.github/workflows/aws-deploy.yml` | Manual AWS deployment workflow |
| `.github/workflows/aws-ci.yml` | Build, test, Terraform validate |
| `src/MIP.Aws.Api/appsettings.Template.json` | Application configuration template |
