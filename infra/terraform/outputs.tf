output "vpc_id" {
  value = module.vpc.vpc_id
}

output "alb_dns_name" {
  value = module.alb.alb_dns_name
}

output "cloudfront_domain_name" {
  value = var.enable_cloudfront ? module.cloudfront[0].domain_name : null
}

output "cloudfront_https_url" {
  description = "Public HTTPS URL for the app (use this instead of the raw ALB HTTP URL when CloudFront is enabled)."
  value       = var.enable_cloudfront ? module.cloudfront[0].https_url : null
}

output "admin_portal_url" {
  description = "Effective portal URL passed to ECS (CloudFront HTTPS when enabled, unless admin_portal_url is overridden)."
  value       = local.admin_portal_url
}

output "api_ecr_repository_url" {
  value = module.ecr.api_repository_url
}

output "worker_ecr_repository_url" {
  value = module.ecr.worker_repository_url
}

output "s3_bucket_name" {
  value = module.s3.bucket_name
}

output "rds_endpoint" {
  value     = module.rds_sqlserver.endpoint
  sensitive = true
}

output "ecs_cluster_name" {
  value = module.ecs.cluster_name
}

output "api_service_name" {
  value = module.ecs.api_service_name
}

output "worker_service_name" {
  value = module.ecs.worker_service_name
}

output "ses_sender_email" {
  value = var.ses_sender_email
}

output "bedrock_enabled" { value = module.iam.bedrock_enabled }
output "bedrock_model_id" { value = module.iam.bedrock_model_id }
output "bedrock_region" { value = module.iam.bedrock_region }
