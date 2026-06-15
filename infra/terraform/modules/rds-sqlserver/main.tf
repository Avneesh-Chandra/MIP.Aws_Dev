variable "name_prefix" { type = string }
variable "vpc_id" { type = string }
variable "private_subnet_ids" { type = list(string) }
variable "allowed_sg_ids" { type = list(string) }
variable "db_username" {
  type      = string
  sensitive = true
}
variable "db_password" {
  type      = string
  sensitive = true
}
variable "db_name" { type = string }
variable "instance_class" { type = string }
variable "allocated_storage" { type = number }
variable "engine" { type = string }
variable "backup_retention_period" {
  type    = number
  default = 1
}
variable "tags" { type = map(string) }

resource "aws_security_group" "rds" {
  name        = "${var.name_prefix}-rds-sg"
  description = "RDS SQL Server access from ECS"
  vpc_id      = var.vpc_id
  ingress {
    from_port       = 1433
    to_port         = 1433
    protocol        = "tcp"
    security_groups = var.allowed_sg_ids
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
  tags = var.tags
}

resource "aws_db_subnet_group" "this" {
  name       = "${var.name_prefix}-rds-subnets"
  subnet_ids = var.private_subnet_ids
  tags       = var.tags
}

resource "aws_db_instance" "this" {
  identifier              = "${var.name_prefix}-sqlserver"
  engine                  = var.engine
  engine_version          = "15.00.4335.1.v1"
  instance_class          = var.instance_class
  allocated_storage       = var.allocated_storage
  storage_type            = "gp3"
  username                = var.db_username
  password                = var.db_password
  db_subnet_group_name    = aws_db_subnet_group.this.name
  vpc_security_group_ids  = [aws_security_group.rds.id]
  publicly_accessible     = false
  backup_retention_period = var.backup_retention_period
  deletion_protection     = false
  skip_final_snapshot     = true
  license_model           = "license-included"
  tags                    = var.tags
}

output "endpoint" { value = aws_db_instance.this.address }
output "security_group_id" { value = aws_security_group.rds.id }
