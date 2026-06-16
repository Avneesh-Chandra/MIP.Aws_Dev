variable "project_name" {
  type    = string
  default = "mip-aws"
}

variable "environment" {
  type    = string
  default = "dev"
}

variable "aws_region" {
  type    = string
  default = "us-east-1"
}

variable "vpc_cidr" {
  type    = string
  default = "10.0.0.0/16"
}

variable "enable_nat_gateway" {
  description = "Enable NAT gateway for private subnet egress (adds cost)"
  type        = bool
  default     = false
}

variable "mip_bucket_name" {
  description = "Globally unique S3 bucket name"
  type        = string
}

variable "ses_sender_email" {
  description = "Verified SES sender email"
  type        = string
  default     = ""
}

variable "db_username" {
  type      = string
  sensitive = true
}

variable "db_password" {
  type      = string
  sensitive = true
}

variable "db_name" {
  type    = string
  default = "MIPAws"
}

variable "db_instance_class" {
  type    = string
  default = "db.t3.small"
}

variable "db_allocated_storage" {
  type    = number
  default = 20
}

variable "db_engine" {
  description = "sqlserver-ex or sqlserver-web"
  type        = string
  default     = "sqlserver-ex"
}

variable "db_backup_retention_period" {
  description = "RDS backup retention days (free tier max is 1)"
  type        = number
  default     = 1
}

variable "jwt_signing_key" {
  type      = string
  sensitive = true
}

variable "identity_default_admin_password" {
  description = "Initial SuperAdmin password for IdentitySeed (superadmin@mip.local). Required for first login on AWS."
  type        = string
  sensitive   = true
}

variable "api_cpu" {
  type    = number
  default = 512
}

variable "api_memory" {
  type    = number
  default = 2048
}

variable "worker_cpu" {
  type    = number
  default = 256
}

variable "worker_memory" {
  type    = number
  default = 512
}

variable "api_desired_count" {
  type    = number
  default = 1
}

variable "worker_desired_count" {
  type    = number
  default = 1
}

variable "cloudwatch_retention_days" {
  type    = number
  default = 14
}

variable "api_image_tag" {
  type    = string
  default = "latest"
}

variable "worker_image_tag" {
  type    = string
  default = "latest"
}

variable "status_email_recipient" {
  type    = string
  default = ""
}

variable "admin_portal_url" {
  type    = string
  default = ""
}

variable "auto_migrate_on_startup" {
  description = "Run EF migrations on API startup (dev first-deploy; RDS is private)"
  type        = bool
  default     = true
}

variable "terraform_apply" {
  description = "Safety gate for deploy workflow"
  type        = bool
  default     = false
}

variable "enable_bedrock" {
  type    = bool
  default = true
}

variable "bedrock_region" {
  type    = string
  default = "eu-north-1"
}

variable "bedrock_model_id" {
  type    = string
  default = "amazon.nova-lite-v1:0"
}

variable "aws_application_arn" {
  description = "myApplications tag value (awsApplication) to link resources in the console"
  type        = string
  default     = ""
}
