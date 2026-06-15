# AWS cost control — MIP.Aws

Defaults in Terraform target **low-cost dev/test** deployments.

## Built-in cost controls

| Resource | Default | Notes |
|----------|---------|-------|
| ECS API | 1 task, 0.25 vCPU, 1 GB | Increase for Playwright-heavy loads |
| ECS Worker | 1 task, 0.25 vCPU, 0.5 GB | Scaffold worker — scale to 0 if unused |
| RDS | SQL Server Express, `db.t3.small`, 20 GB | Smallest practical for SQL Server |
| NAT Gateway | **Disabled** | ECS uses public subnets + public IP when NAT off |
| CloudWatch logs | 14-day retention | Set `cloudwatch_retention_days` variable |
| S3 | Lifecycle to IA after 30 days, expire after 365 | Adjust in `modules/s3` |
| SES | Sandbox until domain verified | No sending charges until production access |

## Stop spend when not testing

```bash
# Scale ECS to zero
aws ecs update-service --cluster mip-aws-dev-cluster --service mip-aws-dev-api --desired-count 0
aws ecs update-service --cluster mip-aws-dev-cluster --service mip-aws-dev-worker --desired-count 0

# Stop RDS (if supported for your engine/edition)
aws rds stop-db-instance --db-instance-identifier mip-aws-dev-sqlserver
```

## Destroy entire environment

```powershell
cd infra/terraform
terraform destroy
```

Review plan carefully — S3 bucket has `force_delete` only on ECR, not S3 (bucket must be emptied first).

## Cost optimization tips

1. **No NAT Gateway** unless private-only ECS is required — saves ~$32/month.
2. **Single AZ RDS** for dev (modify module if needed).
3. **Scale API to 0** nights/weekends.
4. **Use SES sandbox** for development (verify recipient emails only).
5. **S3 lifecycle** — enable intelligent tiering for large PDF archives.
6. **Fargate Spot** — not configured by default; add capacity provider for non-prod.
7. **Bedrock AI recovery** — keep disabled until needed (`Aws:Bedrock:Enabled=false`).

## Production scaling

Increase via Terraform variables:

- `api_desired_count`, `api_cpu`, `api_memory`
- `enable_nat_gateway = true` for private subnets
- `db_instance_class`, `db_allocated_storage`
- `cloudwatch_retention_days`
