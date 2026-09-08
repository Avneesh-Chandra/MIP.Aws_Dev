#!/usr/bin/env bash
# Store the Al Ayam RPA ingest API key and inject it into GFH DEV ECS (API).
# Run after the MIP image that contains /upload-today-pdf is deployed.
#
# CloudShell, account 522213210072, eu-west-1 (NOT sudo):
#   export ALAYAM_RPA_INGEST_API_KEY='generate-a-long-random-string'
#   bash ~/set-alayam-rpa-ingest-ecs.sh
#
# Optional: restrict ingest to Bahrain - Al Ayam:
#   export ALAYAM_RPA_ALLOWED_SOURCE_ID='<guid-from-operator-news-sources>'
#
# Do NOT set UseExternalRpa=true until PAD has uploaded a real PDF successfully.
# Playwright stays as fallback until then.
#
set -euo pipefail

export AWS_REGION="${AWS_REGION:-eu-west-1}"
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)

CLUSTER="${ECS_CLUSTER:-ir-gfh-dev-ecs-mip-01}"
API_SERVICE="${ECS_API_SERVICE:-gfh-dev-mip-svc-api-01}"
WORKER_SERVICE="${ECS_WORKER_SERVICE:-gfh-dev-mip-svc-worker-01}"
SECRET_NAME="${RPA_INGEST_SECRET_NAME:-mip/dev/alayam-rpa-ingest-api-key}"
ENV_KEY="PublisherOutbound__AlAyam__IngestApiKey"
ENV_ALLOWED="PublisherOutbound__AlAyam__IngestAllowedSourceId"

API_KEY="${ALAYAM_RPA_INGEST_API_KEY:-${1:-}}"
ALLOWED_SOURCE_ID="${ALAYAM_RPA_ALLOWED_SOURCE_ID:-}"

echo "Account=$AWS_ACCOUNT_ID Region=$AWS_REGION"
echo "Cluster=$CLUSTER"
echo "API service=$API_SERVICE"

if [ -z "$API_KEY" ]; then
  cat <<'USAGE'
ERROR: ALAYAM_RPA_INGEST_API_KEY is required.

  export ALAYAM_RPA_INGEST_API_KEY='a-long-random-secret'
  bash ~/set-alayam-rpa-ingest-ecs.sh
USAGE
  exit 1
fi

if aws secretsmanager describe-secret --region "$AWS_REGION" --secret-id "$SECRET_NAME" >/dev/null 2>&1; then
  aws secretsmanager put-secret-value --region "$AWS_REGION" \
    --secret-id "$SECRET_NAME" \
    --secret-string "$API_KEY" >/dev/null
  echo "Updated secret: $SECRET_NAME"
else
  aws secretsmanager create-secret --region "$AWS_REGION" \
    --name "$SECRET_NAME" \
    --description "MIP DEV Al Ayam RPA ingest API key (X-Rpa-Api-Key)" \
    --secret-string "$API_KEY" >/dev/null
  echo "Created secret: $SECRET_NAME"
fi

SECRET_ARN=$(aws secretsmanager describe-secret --region "$AWS_REGION" \
  --secret-id "$SECRET_NAME" --query ARN --output text)
SECRET_KMS=$(aws secretsmanager describe-secret --region "$AWS_REGION" \
  --secret-id "$SECRET_NAME" --query KmsKeyId --output text)
echo "Secret ARN: $SECRET_ARN"

resolve_key_arn() {
  local kid="$1"
  if [ -z "$kid" ] || [ "$kid" = "None" ] || [ "$kid" = "null" ]; then
    aws kms describe-key --region "$AWS_REGION" \
      --key-id "alias/aws/secretsmanager" \
      --query KeyMetadata.Arn --output text
    return
  fi
  aws kms describe-key --region "$AWS_REGION" --key-id "$kid" \
    --query KeyMetadata.Arn --output text
}

KMS_ARN=$(resolve_key_arn "$SECRET_KMS")

patch_service() {
  local service="$1"
  local status
  status=$(aws ecs describe-services --region "$AWS_REGION" \
    --cluster "$CLUSTER" --services "$service" \
    --query 'services[0].status' --output text 2>/dev/null || echo "MISSING")

  if [ "$status" != "ACTIVE" ] && [ "$status" != "DRAINING" ]; then
    echo "SKIP: service $service is not active (status=$status)."
    return 0
  fi

  local family
  family=$(aws ecs describe-services --region "$AWS_REGION" \
    --cluster "$CLUSTER" --services "$service" \
    --query 'services[0].taskDefinition' --output text)
  family="${family##*/}"
  family="${family%%:*}"
  echo "Patching service $service (family=$family)..."

  aws ecs describe-task-definition --region "$AWS_REGION" \
    --task-definition "$family" \
    --query 'taskDefinition' > "/tmp/td-${family}.json"

  local exec_role_arn exec_role_name
  exec_role_arn=$(jq -r '.executionRoleArn' "/tmp/td-${family}.json")
  exec_role_name="${exec_role_arn##*/}"

  local exec_policy
  exec_policy=$(cat <<POL
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["secretsmanager:GetSecretValue","secretsmanager:DescribeSecret"],
      "Resource": ["${SECRET_ARN}"]
    },
    {
      "Effect": "Allow",
      "Action": ["kms:Decrypt","kms:DescribeKey"],
      "Resource": ["${KMS_ARN}"]
    }
  ]
}
POL
)
  aws iam put-role-policy --role-name "$exec_role_name" \
    --policy-name mip-dev-read-alayam-rpa-ingest-secret \
    --policy-document "$exec_policy"
  echo "  Granted GetSecretValue to $exec_role_name"

  jq --arg envKey "$ENV_KEY" --arg secretArn "$SECRET_ARN" \
     --arg envAllowed "$ENV_ALLOWED" --arg allowedId "$ALLOWED_SOURCE_ID" '
    .containerDefinitions[0].environment |=
      (map(select(.name != $envKey and .name != $envAllowed))
        + if ($allowedId | length) > 0 then [{name:$envAllowed, value:$allowedId}] else [] end)
    | .containerDefinitions[0].secrets =
        ((.containerDefinitions[0].secrets // [])
          | map(select(.name != $envKey))
          + [ {name:$envKey, valueFrom:$secretArn} ])
    | del(.taskDefinitionArn,.revision,.status,.requiresAttributes,.compatibilities,.registeredAt,.registeredBy)
  ' "/tmp/td-${family}.json" > "/tmp/td-${family}-new.json"

  local new_td
  new_td=$(aws ecs register-task-definition --region "$AWS_REGION" \
    --cli-input-json "file:///tmp/td-${family}-new.json" \
    --query 'taskDefinition.taskDefinitionArn' --output text)
  echo "  Registered: $new_td"

  aws ecs update-service --region "$AWS_REGION" \
    --cluster "$CLUSTER" --service "$service" \
    --task-definition "$new_td" --force-new-deployment \
    --query 'service.taskDefinition' --output text >/dev/null
  echo "  Force-redeployed $service"
}

patch_service "$API_SERVICE"
patch_service "$WORKER_SERVICE"

echo
echo "DONE. Wait 2–5 minutes, then from GFHNews PAD/PowerShell POST a PDF to:"
echo "  POST https://<MIP-API-HOST>/api/v1/news-sources/<alayam-guid>/upload-today-pdf"
echo "  Header: X-Rpa-Api-Key: (the key you just stored)"
echo "Leave PublisherOutbound__AlAyam__UseExternalRpa unset/false until that upload succeeds."
