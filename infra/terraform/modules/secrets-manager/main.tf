variable "name_prefix" { type = string }
variable "jwt_signing_key" {
  type      = string
  sensitive = true
}
variable "ses_sender_email" { type = string }
variable "tags" { type = map(string) }

resource "aws_secretsmanager_secret" "jwt" {
  name = "${var.name_prefix}/jwt-signing-key"
  tags = var.tags
}

resource "aws_secretsmanager_secret_version" "jwt" {
  secret_id     = aws_secretsmanager_secret.jwt.id
  secret_string = var.jwt_signing_key
}

resource "aws_secretsmanager_secret" "connection" {
  name = "${var.name_prefix}/connection-strings"
  tags = var.tags
}

resource "aws_secretsmanager_secret" "app_settings" {
  name = "${var.name_prefix}/app-settings"
  tags = var.tags
}

resource "aws_secretsmanager_secret_version" "app_settings" {
  secret_id = aws_secretsmanager_secret.app_settings.id
  secret_string = jsonencode({
    SesSenderEmail = var.ses_sender_email
  })
}

output "jwt_secret_arn" { value = aws_secretsmanager_secret.jwt.arn }
output "connection_secret_arn" { value = aws_secretsmanager_secret.connection.arn }
output "app_settings_secret_arn" { value = aws_secretsmanager_secret.app_settings.arn }
output "secret_arns" {
  value = [
    aws_secretsmanager_secret.jwt.arn,
    aws_secretsmanager_secret.connection.arn,
    aws_secretsmanager_secret.app_settings.arn
  ]
}
