variable "name_prefix" { type = string }
variable "retention_days" { type = number }
variable "tags" { type = map(string) }

resource "aws_cloudwatch_log_group" "api" {
  name              = "/mip/aws/api"
  retention_in_days = var.retention_days
  tags              = var.tags
}

resource "aws_cloudwatch_log_group" "worker" {
  name              = "/mip/aws/worker"
  retention_in_days = var.retention_days
  tags              = var.tags
}

resource "aws_cloudwatch_log_group" "hangfire" {
  name              = "/mip/aws/hangfire"
  retention_in_days = var.retention_days
  tags              = var.tags
}

output "api_log_group_name" { value = aws_cloudwatch_log_group.api.name }
output "worker_log_group_name" { value = aws_cloudwatch_log_group.worker.name }
output "hangfire_log_group_name" { value = aws_cloudwatch_log_group.hangfire.name }
