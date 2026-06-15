# AWS security — MIP.Aws

## Principles

- **Least privilege IAM** for ECS task and execution roles
- **No secrets in git** — Secrets Manager + GitHub Secrets
- **Private data** — S3 block public access, presigned URLs only
- **Database isolation** — RDS in private subnets, SG allows ECS only
- **TLS ready** — ALB HTTP today; add ACM certificate for HTTPS

## IAM

| Role | Permissions |
|------|-------------|
| ECS execution | Pull images, write logs, read Secrets Manager (JWT) |
| ECS task | S3 read/write on MIP bucket, SES send, Secrets Manager read, CloudWatch logs |

No AWS access keys are embedded in containers — task roles only.

## Secrets

| Secret | Storage |
|--------|---------|
| JWT signing key | Secrets Manager + GitHub Secret `TF_VAR_jwt_signing_key` |
| DB password | GitHub Secret `TF_VAR_db_password` → Terraform → RDS |
| PressReader credentials | Secrets Manager / admin UI (encrypted at rest) |

Never commit `terraform.tfvars`, `appsettings.Development.json`, or `.env` files.

## Network

```
Internet → ALB (public subnets) → ECS API (public or private subnets)
ECS → RDS (private subnets, port 1433)
ECS → S3 / SES / Secrets Manager (AWS APIs)
```

Security groups:
- ALB: inbound 80 from 0.0.0.0/0
- ECS: inbound 8080 from ALB SG only
- RDS: inbound 1433 from ECS SG only

## S3

- Block all public access
- SSE-S3 (AES256) default encryption
- Application uses object keys in DB; downloads via presigned URLs

## SES

- Verify sender domain/email before production sending
- Sandbox mode: only verified recipients receive mail
- Application surfaces clear errors when SES rejects unverified addresses

## Application

- Cookie + JWT authentication
- Role-based authorization policies
- Portal credentials encrypted via `INewsCredentialProtector`
- Health endpoints `/health` and `/health/live` are anonymous (no sensitive data)

## HTTPS (future)

1. Request ACM certificate for your domain
2. Add HTTPS listener on ALB (port 443)
3. Redirect HTTP → HTTPS
4. Set `PdfEditionScheduler:AdminPortalUrl` to `https://...`

## Compliance checklist

- [ ] Rotate JWT signing key periodically
- [ ] Enable RDS encryption at rest (upgrade module if required)
- [ ] Restrict ALB ingress to corporate IP range if needed
- [ ] Enable AWS CloudTrail for API audit
- [ ] Review SES production access request
- [ ] Set `IdentitySeed:SeedDevelopmentRoleUsers=false` in production
