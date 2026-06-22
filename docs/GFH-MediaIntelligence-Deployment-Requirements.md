GFH Media Intelligence Platform (MIP.Aws)

Deployment Requirements — Infrastructure, Hardware & Licensing

Prepared for client review. This document summarises what is required to deploy and operate the GFH Media Intelligence **AWS edition (MIP.Aws)** in a production-grade configuration, including Amazon Web Services resources, sizing guidance, AWS service subscriptions, and third-party publisher licences.

| | |
|---|---|
| **Document date** | 2026-06-15 |
| **Classification** | Confidential — for authorised GFH and client stakeholders |
| **Platform version** | .NET 10 / containerised API (Blazor + Hangfire + Playwright) |
| **Reference environment** | Amazon ECS Fargate, **eu-north-1** (Stockholm) or client-preferred AWS region |
| **Repository** | MIP.Aws (`D:\MIPaws` / GitHub: MIP.Aws_Dev) |

## Document Control

| Version | Date | Author | Notes |
|---------|------|--------|-------|
| 1.0 | 2026-06-15 | GFH Platform Engineering | Initial AWS client deployment brief derived from Terraform (`infra/terraform/`) and AWS_DEPLOYMENT.md. |

## Purpose

This brief is intended to inform the client of hardware (compute and storage), cloud services, software licences, and organisational prerequisites before go-live of **MIP.Aws**. It does not replace a formal statement of work or commercial quotation.

MIP.Aws is the AWS-ready edition of GFH Media Intelligence. It focuses on **newspaper PDF acquisition**, **licensed PressReader portals**, **operator download monitoring**, **status e-mail (Amazon SES)**, and **AI-assisted source recovery (Amazon Bedrock)**. It is a standalone codebase and does not require the original Azure-hosted MIP application.

### Out of scope

- Detailed application user procedures (see the separate User Manual where published for MIP.Aws).

- Full executive reporting, OCR pipeline, and Azure OpenAI article enrichment as implemented in the original Azure MIP (not part of the current MIP.Aws scope).

- Source code, CI/CD pipeline implementation, or day-two runbooks (available under internal engineering documentation in `docs/`).

## Table of Contents

*When opening the Word version: right-click the table of contents and choose **Update Field** to populate page numbers.*

1. Executive Summary  
2. Solution Overview  
3. Deployment Models  
4. AWS Infrastructure Requirements  
5. Hardware & Capacity Sizing  
6. AWS & Software Licences  
7. Third-Party & Publisher Licences (Client-Supplied)  
8. Network, Security & Identity  
9. Operational Prerequisites  
10. Roles & Responsibilities  
11. Suggested Implementation Phases  
12. Compliance & Legal Notice  
Appendix A — Reference URLs (GFH dev environment)  
Appendix B — Key configuration sections  
Appendix C — Document references  

---

## 1. Executive Summary

GFH Media Intelligence (MIP.Aws) is an enterprise platform that ingests licensed and public newspaper PDF editions, provides operator supervision of daily acquisition, sends operational status e-mail, and uses AI to assist recovery when downloads fail.

The recommended deployment model is **Amazon Web Services**: a containerised **ECS Fargate** API service (Blazor UI, REST API, Hangfire job server, Playwright browser automation), **Amazon RDS for SQL Server** (application and Hangfire job store), **Amazon S3** for PDF and artefact storage, **AWS Secrets Manager** for secrets, **Amazon SES** for outbound mail, **Amazon Bedrock** for AI-assisted recovery, and **Amazon CloudWatch** for operations.

**Key client actions:** Provision an AWS account (or dedicated account segment), confirm publisher agreements and PressReader subscriber accounts, verify SES sender identity (and request production access if required), assign operations mailboxes for status e-mail, and nominate administrators for application access.

---

## 2. Solution Overview

### 2.1 Logical architecture

Users access a Blazor administration UI and REST API hosted on the **API ECS service** behind an **Application Load Balancer**. Long-running work (newspaper downloads via Playwright, batch scheduling, status e-mail) runs on the **same API container** via Hangfire background jobs. A separate **Worker ECS service** is provided as a scaffold for future scale-out but is typically scaled to zero in dev. All persistent relational data resides in **RDS SQL Server**; PDF binaries are stored in **S3**.

| Component | Role |
|-----------|------|
| ECS Fargate — API | Blazor UI, REST API, Hangfire dashboard (`/hangfire`), health endpoints (`/health`, `/health/live`). |
| ECS Fargate — Worker (optional) | Scaffold for dedicated Hangfire processing; downloads currently run on API. |
| Amazon RDS (SQL Server) | Users, news sources, download jobs, mail settings, Hangfire tables. |
| Amazon S3 | Newspaper PDFs and download artefacts. |
| AWS Secrets Manager | JWT signing key, optional connection/portal secrets (`mip/` prefix). |
| Amazon Bedrock | AI-assisted download recovery, selector suggestions, status summaries. |
| Amazon SES | Download monitor status e-mail and admin test messages. |
| Application Load Balancer | Public HTTP entry (HTTPS recommended for production). |
| Amazon ECR | API and Worker Docker images. |
| Amazon CloudWatch Logs | Application, Hangfire, and container logs. |

### 2.2 Functional capabilities requiring infrastructure

- **Public PDF discovery** — automated discovery of publisher-exposed PDF links (headless Chromium / Playwright).

- **Licensed PressReader portals** — subscriber login and edition download (Playwright automation).

- **Download Monitor** — operator dashboard for daily acquisition status and staggered batch runs.

- **Status e-mail** — daily batch completion notification via Amazon SES.

- **Auto AI recovery** — Amazon Bedrock analysis when downloads fail (selector/config suggestions).

- **Compliance** — role-based access control, licensed-content gates (`IsDownloadAllowed`), credential protection.

*Not in current MIP.Aws scope:* Azure AI Document Intelligence OCR pipeline, full article enrichment workflow, and scheduled executive report distribution as in the original Azure MIP.

---

## 3. Deployment Models

### 3.1 Recommended: AWS PaaS (primary)

Production deployments use **ECS Fargate** tasks running Docker images from **Amazon ECR**. Infrastructure is provisioned via **Terraform** (`infra/terraform/`) and updated through **GitHub Actions** (`aws-deploy.yml`) or local scripts (`scripts/`). This model minimises operating overhead and aligns with GFH's current dev environment (`mip-aws-dev`).

### 3.2 Alternative: Client-managed EC2 or on-premises VMs

The same container images can run on **Amazon EC2** or client-managed VMs if ECS Fargate is not preferred. In that case the client must provide:

- One or two hosts (API and optional Worker) with equivalent CPU/RAM to the Fargate sizing below.

- Docker runtime, outbound HTTPS to AWS services (S3, SES, Bedrock, Secrets Manager) and publisher sites.

- Load balancer / reverse proxy with TLS termination for the API host.

- SQL Server or RDS reachable from the API host.

**EC2/VM path:** Increases client responsibility for patching, scaling, and high availability. **ECS Fargate is strongly preferred.**

---

## 4. AWS Infrastructure Requirements

The platform requires an AWS account with permission to create resources in a dedicated environment (resource tags: `Project=mip-aws`). Two sizing profiles are defined below.

### 4.1 Sandbox / pilot (current mip-aws-dev reference)

Suitable for functional testing and limited concurrent users. Not recommended for full production newspaper volume.

| AWS resource | SKU / size | Purpose |
|--------------|------------|---------|
| ECS Fargate — API | 0.25 vCPU, 1 GB RAM, 1 task | UI, API, Hangfire, Playwright (tight; scale up for heavy downloads). |
| ECS Fargate — Worker | 0.25 vCPU, 512 MB (optional) | Scaffold; set `worker_desired_count = 0` in dev. |
| Amazon RDS | SQL Server **Express**, `db.t3.small`, 20 GB gp3 | Application + Hangfire (shared DB on Express). |
| Amazon ECR | Standard | API and Worker Docker images (~1.2 GB API image with Playwright). |
| Amazon S3 | Standard, versioning, SSE-S3 | PDF storage under `mip/` prefix. |
| AWS Secrets Manager | Standard | JWT signing key; optional DB/PressReader secrets. |
| Application Load Balancer | Application | Public HTTP on port 80. |
| Amazon SES | Sandbox or verified domain | Status e-mail; sandbox limits recipients. |
| Amazon Bedrock | Pay-per-use (e.g. Nova Lite) | AI recovery; model access enabled per region. |
| Amazon CloudWatch Logs | 14-day retention (default) | API / Hangfire logs. |
| NAT Gateway | **Disabled** (dev default) | ECS uses public subnets + public IP to reduce cost. |
| Amazon VPC | 10.0.0.0/16 | Public + private subnets; RDS in private subnets. |

**Indicative monthly AWS infrastructure cost for sandbox:** approximately **USD 260–320** excluding Bedrock token usage and publisher fees (varies by region and PDF volume). At the CBB peg (**1 USD = 0.376 BHD**), this is approximately **BHD 98–120 per month**.

### 4.2 Production (recommended minimum)

Aligned with `docs/AWS_DEPLOYMENT.md` production guidance. Supports daily multi-source ingestion, Playwright automation, and operational SLOs.

| AWS resource | SKU / size | Purpose |
|--------------|------------|---------|
| ECS Fargate — API | 2 vCPU, 4 GB RAM, 2+ tasks | Dedicated compute; ALB health checks; Playwright headroom. |
| ECS Fargate — Worker | 0.5–1 vCPU, 1–2 GB (if split from API) | Optional dedicated Hangfire workers. |
| Amazon RDS | SQL Server **Web**, `db.t3.medium`+, 100 GB+ | HA consideration: Multi-AZ; backup retention 7–35 days. |
| Amazon S3 | Standard + lifecycle (IA / expiry) | Edition PDFs; GRS or cross-region replication optional. |
| Amazon ECR | Standard | Image retention and CI/CD push. |
| AWS Secrets Manager | Standard + rotation policy | Secret rotation for JWT and DB where required. |
| Application Load Balancer | + ACM certificate (HTTPS) | Custom domain; redirect HTTP → HTTPS. |
| Amazon SES | Production access | Unrestricted recipient sending after AWS approval. |
| Amazon Bedrock | Nova Lite / Nova Pro or Claude (EU profile) | Raise usage limits if recovery volume is high. |
| Amazon CloudWatch | 30–90 day retention + alarms | SLO monitoring on `/health` and failed tasks. |
| NAT Gateway | Recommended if private ECS only | ~USD 35+/month per AZ. |
| Route 53 | Hosted zone (optional) | DNS for custom API domain. |

**Indicative monthly AWS infrastructure cost for production:** approximately **USD 850–2,000+** depending on scale, Multi-AZ, NAT, storage growth, and Bedrock volume (excluding publisher fees). Approximately **BHD 319–752 per month** at 0.376 BHD/USD.

### 4.3 Optional / DR additions

- RDS **Multi-AZ** or cross-region read replica for RPO/RTO targets.

- Secondary region ECS + ALB for disaster recovery (manual failover).

- AWS WAF on ALB for OWASP protection and IP allow lists.

- AWS CloudTrail for API audit across the account.

---

## 5. Hardware & Capacity Sizing

### 5.1 Compute

| Workload | Sandbox | Production guidance |
|----------|---------|---------------------|
| API (UI + REST + Hangfire + Playwright) | 0.25 vCPU, 1 GB, 1 task | 2 vCPU, 4–8 GB; 2+ tasks behind ALB. |
| Worker (optional) | 0 tasks (default) | Scale out if Hangfire queues split from API. |
| Hangfire worker threads | Default (`HangfireQueues:WorkerCount`) | Increase when download queue sustained >200 jobs. |

**Playwright / Chromium:** Newspaper portal automation and public PDF discovery launch headless Chromium inside the **API container**. Each concurrent download job consumes significant memory (512 MB–1 GB+). Size the API task accordingly.

### 5.2 Storage

| Data type | Growth driver | Planning estimate |
|-----------|---------------|-------------------|
| RDS SQL Server | Sources, jobs, users, mail settings | Start 20 GB (dev); grow 5–20 GB/month per 10 daily sources. |
| S3 — newspaper PDFs | One PDF per source per day | ~5–50 MB per edition × sources × retention days. |
| S3 — failure artefacts | Failed download captures | Small fraction of PDF volume. |
| ECR | Container images | ~1–2 GB per API image revision. |

**Example:** 20 titles × 30 MB/edition × 365 days ≈ 200+ GB/year before S3 lifecycle expiration.

### 5.3 AI throughput (Amazon Bedrock)

- **Bedrock:** budget tokens per minute for recovery calls; typical recovery analysis 1–4k tokens per incident.

- Model default for eu-north-1: `amazon.nova-lite-v1:0` (low cost, fast).

- Client should confirm expected daily source count and failure/retry rates for a consumption forecast.

*OCR / Document Intelligence:* not part of MIP.Aws; if full article OCR is required, plan separately or use the original Azure MIP stack.

---

## 6. AWS & Software Licences

The following offerings are required. Specific procurement (direct AWS billing vs. partner) is a client decision.

| Item | Required? | Notes for client |
|------|-----------|------------------|
| AWS account | Yes | IAM user or role for Terraform/CI/CD; billing owner assigned. |
| Amazon RDS SQL Server (license-included) | Yes | Express (dev) or Web/Standard (production); no separate SQL licence purchase on RDS LI model. |
| Amazon ECS / Fargate | Yes | Pay per vCPU-hour and memory-hour. |
| Amazon S3, ECR, ALB, CloudWatch | Yes | Standard AWS service billing. |
| Amazon SES | Yes | Verify sender; sandbox until production access approved. |
| Amazon Bedrock | Yes (if AI recovery enabled) | Enable model access in console per region. |
| AWS Secrets Manager | Recommended | JWT and optional portal secrets. |
| .NET runtime | No separate licence | Open-source; included in container. |
| Playwright / Chromium | No separate licence | Open-source automation bundled in API image. |
| Hangfire | OSS (verify LGPL policy) | Job scheduler; commercial licence if required by legal. |
| GFH MIP.Aws application | Proprietary | GFH / Almoayyed Computers — internal or per customer agreement. |

### 6.1 IAM and deployment principals

- **ECS task role:** S3 read/write, SES send, Secrets Manager read, Bedrock `InvokeModel`, CloudWatch Logs write.

- **ECS execution role:** ECR pull, CloudWatch Logs, Secrets Manager read (JWT at startup).

- **CI/CD principal (GitHub Actions):** `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` repository secrets; ECR push and ECS `UpdateService`.

- **No AWS access keys inside containers** — task roles only at runtime.

### 6.2 Authentication

- API authentication uses **JWT** and **ASP.NET Identity** cookies (`Jwt:SigningKey` in Secrets Manager or environment).

- Microsoft Entra ID / OAuth: not configured in default MIP.Aws Terraform; can be added per client requirement.

---

## 7. Third-Party & Publisher Licences (Client-Supplied)

**Critical:** GFH Media Intelligence does not include newspaper or PressReader subscriptions. The client must hold valid agreements and provide credentials through the admin UI. Automated access must comply with robots.txt, terms of use, and publisher contracts.

| Provider / content | Licence type | Client responsibility |
|--------------------|--------------|----------------------|
| PressReader (e.g. Dar Al Khaleej editions) | Subscriber portal licence | Active PressReader subscription; username/password in News Sources admin; `IsDownloadAllowed` only after compliance approval. |
| Akhbar Al Khaleej, Asharq Al-Awsat, Al Qabas, Al Ayam, etc. | Publisher terms / public web policy | Legal review for automated PDF discovery; public links only where permitted. |
| RSS / public HTML sources | Publisher feed terms | Configure only approved feeds. |

### 7.1 Credentials the client must provision

- PressReader subscriber e-mail and password (per GFH policy — not stored in source control).

- Any licensed portal credentials required by configured news sources.

- **SES verified sender** e-mail or domain (e.g. operations mailbox).

- **Status e-mail recipient** for download monitor notifications.

---

## 8. Network, Security & Identity

### 8.1 Network

- **Outbound HTTPS** from ECS tasks to: RDS, S3, Secrets Manager, SES, Bedrock, CloudWatch, ECR, publisher websites, PressReader.

- **Inbound HTTP/HTTPS** to ALB for users; Worker has no public admin surface.

- **Custom domain and TLS certificate** (ACM on ALB) recommended for production.

- **Security groups:** ALB → ECS on 8080; ECS → RDS on 1433; RDS not publicly accessible.

### 8.2 Security controls (enabled in production)

- HTTPS-only (recommended), security headers, login throttling.

- Secrets in Secrets Manager; IAM task roles (no embedded AWS keys).

- S3 block public access; presigned URLs for downloads where applicable.

- Role-based access (SuperAdmin, ContentAdmin, and roles seeded per Identity configuration).

### 8.3 IAM (AWS)

| Role | Permissions |
|------|-------------|
| ECS task | S3, SES, Secrets Manager read, Bedrock invoke, CloudWatch Logs |
| ECS execution | ECR pull, logs, JWT secret read |
| Deploy principal | ECR push, ECS update, Terraform apply (if used) |

---

## 9. Operational Prerequisites

| Prerequisite | Owner | Detail |
|--------------|-------|--------|
| EF Core database migrations | GFH Engineering / Client DBA | `Database__AutoMigrateOnStartup` or `scripts/run-migrations.ps1`. |
| Container images in ECR | CI/CD | GitHub Actions **AWS Deploy** or `scripts/push-ecr.ps1`. |
| Secrets Manager / env vars | Deployment runbook | JWT (≥32 chars), RDS connection, SES sender, Bedrock region/model. |
| GitHub repository secrets | Client IT | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` for CI/CD. |
| Initial admin user | Platform | `superadmin@mip.local` seeded on first deploy; change password post-handover. |
| Newspaper catalog seed | Platform | `NewspaperCatalog:SeedOnStartup` enables idempotent Gulf source templates. |
| SES identity verification | Client IT | Verify sender e-mail/domain; request production access if needed. |
| Bedrock model access | Client IT | Enable Nova Lite (or chosen model) in target region. |
| Monitoring | Operations | CloudWatch alarms on ECS task failures, ALB 5xx, `/health` checks. |
| Backup policy | Client IT | RDS automated backups; S3 lifecycle per retention policy. |

### 9.1 Post-deploy verification

- `GET /health/live` and `GET /health` return **200**.

- Hangfire dashboard shows recurring jobs (PDF edition scheduler, download monitor).

- Test PDF discovery and licensed portal login from **PDF Management** (`/admin/pdf-management`).

- Test mail from **Mail Settings** (`/admin/mail-settings`).

---

## 10. Roles & Responsibilities

| Role | Typical owner | Responsibilities |
|------|---------------|------------------|
| Client IT / Cloud | Client | AWS account, networking, DNS, ACM certificates, backup policy, SES production access. |
| Client Compliance / Legal | Client | Publisher agreements, PressReader entitlement, download approval gates. |
| GFH Platform Engineering | GFH | Terraform deploy, ECR push, ECS redeploy, smoke tests, handover. |
| Content Administrator | Client/GFH | News sources, PDF settings, mail settings, user provisioning. |
| SuperAdmin | GFH | Hangfire dashboard, portal test login/download, system configuration. |

---

## 11. Suggested Implementation Phases

1. **Phase 1 — Infrastructure:** Provision VPC, RDS, S3, SES, ECS, ALB via Terraform; configure Secrets Manager; run database migrations.

2. **Phase 2 — Platform smoke test:** Verify health endpoints, seed catalog, create admin users, configure SES sender and status recipient.

3. **Phase 3 — Source onboarding:** Add public PDF sources and licensed PressReader editions; compliance sign-off on `IsDownloadAllowed`.

4. **Phase 4 — Pilot ingestion:** Run Download Monitor batch; validate Playwright downloads on sample editions.

5. **Phase 5 — Production cutover:** Enable production SES, HTTPS on ALB, monitoring alarms, and operational runbook.

---

## 12. Compliance & Legal Notice

The platform includes technical controls (`IPublisherComplianceGate`, licensed-portal disclaimers, robots.txt respect for public modes) to support GFH compliance posture. These controls do not replace legal review of each publisher relationship.

- Automated steps use only publisher-exposed controls (e.g. explicit download buttons on licensed portals).

- The system does not bypass paywalls, CAPTCHA, MFA, or rate limits.

- Downloaded PDFs may contain licensed third-party content — storage and distribution must follow GFH confidentiality policies.

---

## Appendix A — Reference URLs (GFH AWS dev environment)

| Item | URL |
|------|-----|
| Admin UI / API | http://mip-aws-dev-alb-839810689.eu-north-1.elb.amazonaws.com |
| Hangfire dashboard | http://mip-aws-dev-alb-839810689.eu-north-1.elb.amazonaws.com/hangfire |
| Health — live | http://mip-aws-dev-alb-839810689.eu-north-1.elb.amazonaws.com/health/live |
| PDF Management | http://mip-aws-dev-alb-839810689.eu-north-1.elb.amazonaws.com/admin/pdf-management |
| Mail Settings | http://mip-aws-dev-alb-839810689.eu-north-1.elb.amazonaws.com/admin/mail-settings |

*Replace with HTTPS custom domain in production.*

---

## Appendix B — Key configuration sections

- `ConnectionStrings:DefaultConnection`, `ConnectionStrings:Hangfire`

- `Aws:Region`, `Aws:S3:*`, `Aws:Ses:*`, `Aws:Bedrock:*`, `Aws:SecretsManager:*`

- `Storage:Provider` (S3 in AWS)

- `Email:Provider` (AwsSes)

- `Jwt:*`, `Auth:UseHttpsCookies`

- `PdfEditionScheduler:*`, `MailAutomation:*`

- `HangfireQueues`, `NewspaperCatalog`, `IdentitySeed`

---

## Appendix C — Document references

| Document | Location |
|----------|----------|
| AWS deployment guide | `docs/AWS_DEPLOYMENT.md` |
| AWS security | `docs/AWS_SECURITY.md` |
| AWS cost control | `docs/AWS_COST_CONTROL.md` |
| BHD cost estimate | `docs/GFH_MIP_AWS_COST_ESTIMATE_BHD.docx` |
| Bedrock AI | `docs/AWS_BEDROCK_AI.md` |
| Terraform | `infra/terraform/` |
| GitHub Actions deploy | `.github/workflows/aws-deploy.yml` |

---

*GFH Media Intelligence Platform — MIP.Aws — Confidential*
