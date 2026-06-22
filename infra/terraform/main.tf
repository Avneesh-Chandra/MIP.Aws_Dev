locals {
  name_prefix = "${var.project_name}-${var.environment}"
  application_tags = var.aws_application_arn != "" ? {
    awsApplication = var.aws_application_arn
  } : {}
  common_tags = merge({
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
    Application = "MIP_Aws_Test"
  }, local.application_tags)
  ecs_assign_public_ip = !var.enable_nat_gateway
  ecs_subnet_ids       = var.enable_nat_gateway ? module.vpc.private_subnet_ids : module.vpc.public_subnet_ids
  admin_portal_url = var.admin_portal_url != "" ? var.admin_portal_url : (
    var.enable_cloudfront ? module.cloudfront[0].https_url : "http://${module.alb.alb_dns_name}"
  )
  use_https_cookies = startswith(local.admin_portal_url, "https://")
}

module "vpc" {
  source = "./modules/vpc"

  name_prefix        = local.name_prefix
  vpc_cidr           = var.vpc_cidr
  enable_nat_gateway = var.enable_nat_gateway
  tags               = local.common_tags
}

resource "aws_security_group" "ecs_api" {
  name        = "${local.name_prefix}-ecs-api-sg"
  description = "ECS API/Worker tasks"
  vpc_id      = module.vpc.vpc_id

  ingress {
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [module.alb.security_group_id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = local.common_tags
}

module "ecr" {
  source = "./modules/ecr"

  name_prefix = local.name_prefix
  tags        = local.common_tags
}

module "s3" {
  source = "./modules/s3"

  bucket_name = var.mip_bucket_name
  tags        = local.common_tags
}

module "cloudwatch" {
  source = "./modules/cloudwatch"

  name_prefix    = local.name_prefix
  retention_days = var.cloudwatch_retention_days
  tags           = local.common_tags
}

module "secrets_manager" {
  source = "./modules/secrets-manager"

  name_prefix      = local.name_prefix
  jwt_signing_key  = var.jwt_signing_key
  ses_sender_email = var.ses_sender_email
  tags             = local.common_tags
}

module "alb" {
  source = "./modules/alb"

  name_prefix       = local.name_prefix
  vpc_id            = module.vpc.vpc_id
  public_subnet_ids = module.vpc.public_subnet_ids
  tags              = local.common_tags
}

module "cloudfront" {
  count  = var.enable_cloudfront ? 1 : 0
  source = "./modules/cloudfront"

  name_prefix  = local.name_prefix
  alb_dns_name = module.alb.alb_dns_name
  tags         = local.common_tags
}

module "rds_sqlserver" {
  source = "./modules/rds-sqlserver"

  name_prefix             = local.name_prefix
  vpc_id                  = module.vpc.vpc_id
  private_subnet_ids      = module.vpc.private_subnet_ids
  allowed_sg_ids          = [aws_security_group.ecs_api.id]
  db_username             = var.db_username
  db_password             = var.db_password
  db_name                 = var.db_name
  instance_class          = var.db_instance_class
  allocated_storage       = var.db_allocated_storage
  engine                  = var.db_engine
  backup_retention_period = var.db_backup_retention_period
  tags                    = local.common_tags
}

module "iam" {
  source = "./modules/iam"

  name_prefix      = local.name_prefix
  bucket_arn       = module.s3.bucket_arn
  bucket_name      = module.s3.bucket_name
  secret_arns      = module.secrets_manager.secret_arns
  enable_bedrock   = var.enable_bedrock
  bedrock_region   = var.bedrock_region
  bedrock_model_id = var.bedrock_model_id
  tags             = local.common_tags
}

module "ecs" {
  source = "./modules/ecs"

  name_prefix                     = local.name_prefix
  vpc_id                          = module.vpc.vpc_id
  subnet_ids                      = local.ecs_subnet_ids
  assign_public_ip                = local.ecs_assign_public_ip
  alb_target_group_arn            = module.alb.target_group_arn
  api_security_group_id           = aws_security_group.ecs_api.id
  api_cpu                         = var.api_cpu
  api_memory                      = var.api_memory
  worker_cpu                      = var.worker_cpu
  worker_memory                   = var.worker_memory
  api_desired_count               = var.api_desired_count
  worker_desired_count            = var.worker_desired_count
  api_log_group_name              = module.cloudwatch.api_log_group_name
  worker_log_group_name           = module.cloudwatch.worker_log_group_name
  hangfire_log_group_name         = module.cloudwatch.hangfire_log_group_name
  execution_role_arn              = module.iam.ecs_execution_role_arn
  task_role_arn                   = module.iam.ecs_task_role_arn
  api_repository_url              = module.ecr.api_repository_url
  worker_repository_url           = module.ecr.worker_repository_url
  api_image_tag                   = var.api_image_tag
  worker_image_tag                = var.worker_image_tag
  aws_region                      = var.aws_region
  s3_bucket_name                  = module.s3.bucket_name
  ses_sender_email                = var.ses_sender_email
  db_endpoint                     = module.rds_sqlserver.endpoint
  db_username                     = var.db_username
  db_password                     = var.db_password
  db_name                         = var.db_name
  jwt_secret_arn                  = module.secrets_manager.jwt_secret_arn
  connection_secret_arn           = module.secrets_manager.connection_secret_arn
  status_email_recipient          = var.status_email_recipient
  admin_portal_url                = local.admin_portal_url
  use_https_cookies               = local.use_https_cookies
  auto_migrate_on_startup         = var.auto_migrate_on_startup
  enable_bedrock                  = var.enable_bedrock
  bedrock_region                  = var.bedrock_region
  bedrock_model_id                = var.bedrock_model_id
  identity_default_admin_password = var.identity_default_admin_password
  tags                            = local.common_tags
}

module "ses" {
  source = "./modules/ses"

  sender_email = var.ses_sender_email
  tags         = local.common_tags
}
