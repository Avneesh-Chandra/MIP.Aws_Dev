# Database migrations — MIP.Aws on AWS

## Overview

MIP.Aws uses EF Core migrations in `src/MIP.Aws.Persistence/Migrations/`.

| Database | Purpose |
|----------|---------|
| `MIPAws` | Application data **and** Hangfire job tables on AWS (RDS SQL Server Express allows only **one** user database per instance) |
| `MIPAws_Hangfire` | Local development only — separate Hangfire catalog |

In **Development**, migrations run automatically via `DatabaseBootstrap.ApplyDevelopmentDatabaseAsync`.

In **Production**, migrations are **not** applied on API startup — run them explicitly before or during deployment.

## Option 1 — GitHub Actions (recommended)

Add a manual workflow step or run locally before deploy:

```powershell
$env:ConnectionStrings__DefaultConnection = "<RDS connection string>"
.\scripts\run-migrations.ps1
```

Wire into `aws-deploy.yml` as an optional pre-apply step when RDS is reachable from GitHub Actions (requires public RDS bastion or self-hosted runner in VPC).

## Option 2 — Local machine via VPN/bastion

1. Connect to AWS (VPN, bastion, or SSM port forward to RDS).
2. Set connection string:
   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Server=<rds-endpoint>;Database=MIPAws;User Id=<user>;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=true"
   ```
3. Run:
   ```powershell
   dotnet ef database update --project src/MIP.Aws.Persistence --startup-project src/MIP.Aws.Api
   ```

## Option 3 — One-off ECS migration task

1. Build a migration image from `Dockerfile.Api` (includes EF tools context).
2. Run as ECS Fargate task in the same VPC/security group as RDS.
3. Override command:
   ```bash
   dotnet ef database update --project src/MIP.Aws.Persistence --startup-project src/MIP.Aws.Api
   ```
4. Stop the task after success.

## Hangfire database

On **RDS SQL Server Express**, `ConnectionStrings__Hangfire` must point to the **same** catalog as `DefaultConnection` (`MIPAws`). Hangfire creates its own tables on first start (`PrepareSchemaIfNecessary = true`).

Do **not** create a separate `MIPAws_Hangfire` database on Express — the engine allows only one user database and the API will crash with error 4060.

## Rollback

EF migrations are forward-only in production. To roll back:

1. Restore RDS snapshot.
2. Or deploy a previous application version and run `dotnet ef database update <PreviousMigration>` if compatible.

## Verification

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
```

API health: `GET /health/live` should return 200 after migrations and config are correct.
