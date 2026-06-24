# Non-secret AWS dev sizing — used by GitHub Actions (-var-file) and local deploys.
# Secrets (db_password, jwt_signing_key, etc.) still come from terraform.tfvars or GitHub secrets.

aws_region   = "eu-north-1"
project_name = "mip-aws"
environment  = "dev"

db_name                    = "MIPAws"
db_instance_class          = "db.t3.micro"
db_allocated_storage       = 20
db_engine                  = "sqlserver-ex"
db_backup_retention_period = 1

ses_sender_email       = "avneesh.c@almoayyedcomputers.com"
status_email_recipient = "ops@your-domain.example"

enable_nat_gateway        = false
enable_cloudfront         = true
api_desired_count         = 1
worker_desired_count      = 1
api_cpu                   = 512
api_memory                = 2048
worker_cpu                = 1024
worker_memory             = 2048
cloudwatch_retention_days = 7

enable_bedrock   = true
bedrock_region   = "eu-north-1"
bedrock_model_id = "amazon.nova-lite-v1:0"

aws_application_arn = "arn:aws:resource-groups:eu-north-1:640533249094:group/MIP_Aws_Test/0cp7d3a9r0n4a7f512w9eup5jy"

api_image_tag    = "latest"
worker_image_tag = "latest"
