variable "name_prefix" { type = string }
variable "vpc_id" { type = string }
variable "subnet_ids" { type = list(string) }
variable "assign_public_ip" { type = bool }
variable "alb_target_group_arn" { type = string }
variable "api_security_group_id" { type = string }
variable "api_cpu" { type = number }
variable "api_memory" { type = number }
variable "worker_cpu" { type = number }
variable "worker_memory" { type = number }
variable "api_desired_count" { type = number }
variable "worker_desired_count" { type = number }
variable "api_log_group_name" { type = string }
variable "worker_log_group_name" { type = string }
variable "hangfire_log_group_name" { type = string }
variable "execution_role_arn" { type = string }
variable "task_role_arn" { type = string }
variable "api_repository_url" { type = string }
variable "worker_repository_url" { type = string }
variable "api_image_tag" { type = string }
variable "worker_image_tag" { type = string }
variable "aws_region" { type = string }
variable "s3_bucket_name" { type = string }
variable "ses_sender_email" { type = string }
variable "db_endpoint" { type = string }
variable "db_username" {
  type      = string
  sensitive = true
}
variable "db_password" {
  type      = string
  sensitive = true
}
variable "db_name" { type = string }
variable "jwt_secret_arn" { type = string }
variable "connection_secret_arn" { type = string }
variable "status_email_recipient" { type = string }
variable "admin_portal_url" { type = string }
variable "auto_migrate_on_startup" {
  type    = bool
  default = true
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
variable "identity_default_admin_password" {
  type      = string
  sensitive = true
}
variable "tags" { type = map(string) }

locals {
  default_conn = "Server=${var.db_endpoint};Database=${var.db_name};User Id=${var.db_username};Password=${var.db_password};TrustServerCertificate=True;MultipleActiveResultSets=true"
  # RDS SQL Server Express allows only one user database per instance — share MIPAws with Hangfire.
  hangfire_conn = local.default_conn
}

resource "aws_ecs_cluster" "this" {
  name = "${var.name_prefix}-cluster"
  tags = var.tags
}

resource "aws_ecs_task_definition" "api" {
  family                   = "${var.name_prefix}-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.api_cpu
  memory                   = var.api_memory
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.task_role_arn

  container_definitions = jsonencode([
    {
      name         = "api"
      image        = "${var.api_repository_url}:${var.api_image_tag}"
      essential    = true
      portMappings = [{ containerPort = 8080, protocol = "tcp" }]
      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Development" },
        { name = "ASPNETCORE_URLS", value = "http://+:8080" },
        { name = "Aws__Region", value = var.aws_region },
        { name = "Aws__S3__Enabled", value = "true" },
        { name = "Aws__S3__BucketName", value = var.s3_bucket_name },
        { name = "Aws__S3__Prefix", value = "mip/" },
        { name = "Aws__Ses__Enabled", value = "true" },
        { name = "Aws__Ses__SenderEmail", value = var.ses_sender_email },
        { name = "Aws__SecretsManager__Enabled", value = "true" },
        { name = "Aws__SecretsManager__Prefix", value = "mip/" },
        { name = "Storage__Provider", value = "S3" },
        { name = "Email__Provider", value = "AwsSes" },
        { name = "Email__FromEmail", value = var.ses_sender_email },
        { name = "MailAutomation__Enabled", value = "true" },
        { name = "ConnectionStrings__DefaultConnection", value = local.default_conn },
        { name = "ConnectionStrings__Hangfire", value = local.hangfire_conn },
        { name = "Database__AutoMigrateOnStartup", value = tostring(var.auto_migrate_on_startup) },
        { name = "PdfEditionScheduler__StatusEmailRecipient", value = var.status_email_recipient },
        { name = "PdfEditionScheduler__AdminPortalUrl", value = var.admin_portal_url },
        { name = "Ai__Provider", value = "AwsBedrock" },
        { name = "Ai__Enabled", value = "true" },
        { name = "Ai__MockMode", value = "false" },
        { name = "Aws__Bedrock__Enabled", value = tostring(var.enable_bedrock) },
        { name = "Aws__Bedrock__Region", value = var.bedrock_region },
        { name = "Aws__Bedrock__ModelId", value = var.bedrock_model_id },
        { name = "Aws__Bedrock__MaxTokens", value = "1200" },
        { name = "Aws__Bedrock__Temperature", value = "0.2" },
        { name = "Aws__Bedrock__TopP", value = "0.9" },
        { name = "Aws__Bedrock__TimeoutSeconds", value = "60" },
        { name = "IdentitySeed__DefaultAdminEmail", value = "superadmin@mip.local" },
        { name = "IdentitySeed__DefaultAdminPassword", value = var.identity_default_admin_password },
        { name = "Auth__UseHttpsCookies", value = "false" }
      ]
      secrets = [
        { name = "Jwt__SigningKey", valueFrom = var.jwt_secret_arn }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.api_log_group_name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "api"
        }
      }
      healthCheck = {
        command     = ["CMD-SHELL", "wget --spider -q http://localhost:8080/health/live || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
    }
  ])
  tags = var.tags
}

resource "aws_ecs_task_definition" "worker" {
  family                   = "${var.name_prefix}-worker"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.worker_cpu
  memory                   = var.worker_memory
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.task_role_arn

  container_definitions = jsonencode([
    {
      name      = "worker"
      image     = "${var.worker_repository_url}:${var.worker_image_tag}"
      essential = true
      environment = [
        { name = "DOTNET_ENVIRONMENT", value = "Production" },
        { name = "Aws__Region", value = var.aws_region }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.worker_log_group_name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "worker"
        }
      }
    }
  ])
  tags = var.tags
}

resource "aws_ecs_service" "api" {
  name                              = "${var.name_prefix}-api"
  cluster                           = aws_ecs_cluster.this.id
  task_definition                   = aws_ecs_task_definition.api.arn
  desired_count                     = var.api_desired_count
  launch_type                       = "FARGATE"
  health_check_grace_period_seconds = 300

  network_configuration {
    subnets          = var.subnet_ids
    security_groups  = [var.api_security_group_id]
    assign_public_ip = var.assign_public_ip
  }

  load_balancer {
    target_group_arn = var.alb_target_group_arn
    container_name   = "api"
    container_port   = 8080
  }

  tags = var.tags
}

resource "aws_ecs_service" "worker" {
  name            = "${var.name_prefix}-worker"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.worker.arn
  desired_count   = var.worker_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.subnet_ids
    security_groups  = [var.api_security_group_id]
    assign_public_ip = var.assign_public_ip
  }

  tags = var.tags
}

output "cluster_name" { value = aws_ecs_cluster.this.name }
output "api_service_name" { value = aws_ecs_service.api.name }
output "worker_service_name" { value = aws_ecs_service.worker.name }
