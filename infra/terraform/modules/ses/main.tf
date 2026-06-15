variable "sender_email" { type = string }
variable "tags" { type = map(string) }

# SES identity verification is typically completed manually in the AWS console.
# This module documents the intended sender; create identity only when email is provided.

resource "aws_sesv2_email_identity" "sender" {
  count          = var.sender_email != "" ? 1 : 0
  email_identity = var.sender_email
  tags           = var.tags
}

output "sender_identity_arn" {
  value = length(aws_sesv2_email_identity.sender) > 0 ? aws_sesv2_email_identity.sender[0].arn : null
}
