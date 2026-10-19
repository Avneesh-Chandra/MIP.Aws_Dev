#!/usr/bin/env bash
# Point status mail + PDF download mail links at the public GFH portal.
#
# Sets PdfEditionScheduler__AdminPortalUrl on API and Worker task definitions,
# then force-redeploys both services.
#
# Run in AWS CloudShell (account that hosts ir-gfh-dev-ecs-mip-01), eu-west-1:
#   bash ~/set-admin-portal-url-ecs.sh
#   # or override:
#   ADMIN_PORTAL_URL='https://gfnnewsintelligence.gfh.com' bash ~/set-admin-portal-url-ecs.sh
#
set -euo pipefail

export AWS_REGION="${AWS_REGION:-eu-west-1}"
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)

CLUSTER="${ECS_CLUSTER:-ir-gfh-dev-ecs-mip-01}"
API_SERVICE="${ECS_API_SERVICE:-gfh-dev-mip-svc-api-01}"
WORKER_SERVICE="${ECS_WORKER_SERVICE:-gfh-dev-mip-svc-worker-01}"
ENV_KEY="PdfEditionScheduler__AdminPortalUrl"
PORTAL_URL="${ADMIN_PORTAL_URL:-https://gfnnewsintelligence.gfh.com}"
PORTAL_URL="${PORTAL_URL%/}"

echo "Account=$AWS_ACCOUNT_ID Region=$AWS_REGION"
echo "Cluster=$CLUSTER"
echo "API service=$API_SERVICE"
echo "Worker service=$WORKER_SERVICE"
echo "AdminPortalUrl=$PORTAL_URL"
echo

case "$PORTAL_URL" in
  https://*) ;;
  *)
    echo "ERROR: ADMIN_PORTAL_URL must start with https://"
    exit 1
    ;;
esac

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

  jq --arg envKey "$ENV_KEY" --arg portalUrl "$PORTAL_URL" '
    .containerDefinitions[0].environment =
      ((.containerDefinitions[0].environment // [])
        | map(select(.name != $envKey))
        + [ {name:$envKey, value:$portalUrl} ])
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
echo "DONE. Wait 2–5 minutes for new tasks."
echo "Next status / PDF download mails will use:"
echo "  View/Download/Details -> ${PORTAL_URL}/..."
echo
echo "Verify on a running task:"
echo "  aws ecs list-tasks --cluster $CLUSTER --service-name $API_SERVICE --query 'taskArns[0]' --output text"
echo "  # then describe-tasks -> taskDefinition -> look for $ENV_KEY"
