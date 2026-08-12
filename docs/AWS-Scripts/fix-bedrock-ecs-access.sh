#!/usr/bin/env bash
# =============================================================================
# Fix Bedrock invoke for GFH MIP DEV ECS (AI settings "Test Bedrock")
# =============================================================================
# eu-west-1 does NOT support on-demand InvokeModel for amazon.nova-lite-v1:0.
# You must invoke the EU inference profile: eu.amazon.nova-lite-v1:0
#
# This script:
#   1) Finds the ECS task ROLE used by the API service
#   2) Attaches IAM allowing InvokeModel on the inference profile + EU Nova models
#   3) Smoke-tests InvokeModel using eu.amazon.nova-lite-v1:0
#   4) Optionally sets ECS env Aws__Bedrock__ModelId=eu.amazon.nova-lite-v1:0
#      (needed until the app mapping deploy is live)
#
# Run in AWS CloudShell as the normal user (NOT sudo su):
#   sed -i 's/\r$//' ./fix-bedrock-ecs-access.sh
#   bash ./fix-bedrock-ecs-access.sh
#
# To also update the ECS task env + force a new deployment:
#   UPDATE_ECS_ENV=true bash ./fix-bedrock-ecs-access.sh
# =============================================================================
set -euo pipefail

AWS_REGION="${AWS_REGION:-eu-west-1}"
BEDROCK_REGION="${BEDROCK_REGION:-$AWS_REGION}"
ECS_CLUSTER="${ECS_CLUSTER:-ir-gfh-dev-ecs-mip-01}"
ECS_API_SERVICE="${ECS_API_SERVICE:-gfh-dev-mip-svc-api-01}"
FOUNDATION_MODEL_ID="${FOUNDATION_MODEL_ID:-amazon.nova-lite-v1:0}"
INFERENCE_PROFILE_ID="${INFERENCE_PROFILE_ID:-eu.amazon.nova-lite-v1:0}"
POLICY_NAME="${POLICY_NAME:-mip-bedrock-invoke-nova-lite}"
SMOKE_TEST="${SMOKE_TEST:-true}"
UPDATE_ECS_ENV="${UPDATE_ECS_ENV:-false}"

echo "==> Region:              $AWS_REGION"
echo "==> Bedrock region:      $BEDROCK_REGION"
echo "==> Cluster/service:     $ECS_CLUSTER / $ECS_API_SERVICE"
echo "==> Foundation model:    $FOUNDATION_MODEL_ID"
echo "==> Inference profile:   $INFERENCE_PROFILE_ID"
echo

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
echo "==> Account:             $ACCOUNT_ID"
echo

TASK_DEF_ARN="$(aws ecs describe-services \
  --cluster "$ECS_CLUSTER" \
  --services "$ECS_API_SERVICE" \
  --region "$AWS_REGION" \
  --query 'services[0].taskDefinition' \
  --output text)"

if [[ -z "$TASK_DEF_ARN" || "$TASK_DEF_ARN" == "None" ]]; then
  echo "ERROR: Could not resolve task definition for $ECS_API_SERVICE in $ECS_CLUSTER"
  exit 1
fi

echo "==> Task definition:     $TASK_DEF_ARN"

TASK_ROLE_ARN="$(aws ecs describe-task-definition \
  --task-definition "$TASK_DEF_ARN" \
  --region "$AWS_REGION" \
  --query 'taskDefinition.taskRoleArn' \
  --output text)"

if [[ -z "$TASK_ROLE_ARN" || "$TASK_ROLE_ARN" == "None" ]]; then
  echo "ERROR: taskRoleArn is empty on the task definition."
  exit 1
fi

TASK_ROLE_NAME="${TASK_ROLE_ARN##*/}"
echo "==> Task role ARN:       $TASK_ROLE_ARN"
echo "==> Task role name:      $TASK_ROLE_NAME"
echo

echo "==> Current Bedrock-related env on task definition:"
aws ecs describe-task-definition \
  --task-definition "$TASK_DEF_ARN" \
  --region "$AWS_REGION" \
  --query "taskDefinition.containerDefinitions[].environment[?starts_with(name, 'Ai__') || starts_with(name, 'Aws__Bedrock__')].[name,value]" \
  --output table || true
echo

# Cross-region inference needs InvokeModel on:
# - the inference profile in the source region
# - the underlying foundation model in destination EU regions
POLICY_DOC="$(cat <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "InvokeNovaLiteInferenceProfile",
      "Effect": "Allow",
      "Action": [
        "bedrock:InvokeModel",
        "bedrock:InvokeModelWithResponseStream"
      ],
      "Resource": [
        "arn:aws:bedrock:${BEDROCK_REGION}:${ACCOUNT_ID}:inference-profile/${INFERENCE_PROFILE_ID}",
        "arn:aws:bedrock:${BEDROCK_REGION}:${ACCOUNT_ID}:inference-profile/*",
        "arn:aws:bedrock:eu-west-1::foundation-model/${FOUNDATION_MODEL_ID}",
        "arn:aws:bedrock:eu-west-3::foundation-model/${FOUNDATION_MODEL_ID}",
        "arn:aws:bedrock:eu-central-1::foundation-model/${FOUNDATION_MODEL_ID}",
        "arn:aws:bedrock:eu-north-1::foundation-model/${FOUNDATION_MODEL_ID}"
      ]
    },
    {
      "Sid": "ListAndGetModelInfo",
      "Effect": "Allow",
      "Action": [
        "bedrock:GetFoundationModel",
        "bedrock:ListFoundationModels",
        "bedrock:GetInferenceProfile",
        "bedrock:ListInferenceProfiles"
      ],
      "Resource": "*"
    }
  ]
}
EOF
)"

echo "==> Attaching inline policy '$POLICY_NAME' to role '$TASK_ROLE_NAME'..."
aws iam put-role-policy \
  --role-name "$TASK_ROLE_NAME" \
  --policy-name "$POLICY_NAME" \
  --policy-document "$POLICY_DOC"
echo "    Done."
echo

echo "==> Checking foundation model in $BEDROCK_REGION..."
if aws bedrock get-foundation-model \
  --model-identifier "$FOUNDATION_MODEL_ID" \
  --region "$BEDROCK_REGION" \
  --query 'modelDetails.modelId' \
  --output text >/tmp/mip-bedrock-model-id.txt 2>/tmp/mip-bedrock-model-err.txt; then
  echo "    Model visible: $(cat /tmp/mip-bedrock-model-id.txt)"
else
  echo "    WARNING: get-foundation-model failed:"
  cat /tmp/mip-bedrock-model-err.txt
  echo "    Enable Amazon Nova Lite in:"
  echo "      https://${BEDROCK_REGION}.console.aws.amazon.com/bedrock/home?region=${BEDROCK_REGION}#/modelaccess"
fi
echo

if [[ "$SMOKE_TEST" == "true" ]]; then
  echo "==> Smoke-testing InvokeModel with inference profile $INFERENCE_PROFILE_ID ..."
  BODY_FILE="$(mktemp)"
  cat >"$BODY_FILE" <<'EOF'
{
  "messages": [
    {
      "role": "user",
      "content": [{ "text": "Reply with the single word: ok" }]
    }
  ],
  "inferenceConfig": {
    "maxTokens": 16,
    "temperature": 0
  }
}
EOF

  if aws bedrock-runtime invoke-model \
    --model-id "$INFERENCE_PROFILE_ID" \
    --region "$BEDROCK_REGION" \
    --content-type "application/json" \
    --accept "application/json" \
    --body "fileb://${BODY_FILE}" \
    /tmp/mip-bedrock-invoke-out.json >/tmp/mip-bedrock-invoke-meta.json 2>/tmp/mip-bedrock-invoke-err.txt; then
    echo "    InvokeModel OK (CLI identity)."
    head -c 500 /tmp/mip-bedrock-invoke-out.json; echo
  else
    echo "    InvokeModel FAILED for CLI identity:"
    cat /tmp/mip-bedrock-invoke-err.txt
    echo
    echo "    If AccessDenied: enable Nova Lite model access in ${BEDROCK_REGION}."
    echo "    If still ValidationException: confirm inference profile ID is ${INFERENCE_PROFILE_ID}."
  fi
  rm -f "$BODY_FILE"
  echo
fi

if [[ "$UPDATE_ECS_ENV" == "true" ]]; then
  echo "==> Registering new task definition with Aws__Bedrock__ModelId=$INFERENCE_PROFILE_ID ..."
  TMP_TD="$(mktemp)"
  aws ecs describe-task-definition \
    --task-definition "$TASK_DEF_ARN" \
    --region "$AWS_REGION" \
    --query 'taskDefinition' \
    --output json >"$TMP_TD"

  python3 - "$TMP_TD" "$INFERENCE_PROFILE_ID" "$BEDROCK_REGION" <<'PY'
import json, sys
path, profile_id, bedrock_region = sys.argv[1], sys.argv[2], sys.argv[3]
td = json.load(open(path))
for key in [
    "taskDefinitionArn", "revision", "status", "requiresAttributes",
    "compatibilities", "registeredAt", "registeredBy", "deregisteredAt"
]:
    td.pop(key, None)
env_keys = {
    "Aws__Bedrock__Enabled": "true",
    "Aws__Bedrock__ModelId": profile_id,
    "Aws__Bedrock__Region": bedrock_region,
    "Ai__Provider": "AwsBedrock",
    "Ai__Enabled": "true",
    "Ai__MockMode": "false",
}
for c in td.get("containerDefinitions", []):
    env = c.setdefault("environment", [])
    by_name = {e["name"]: e for e in env}
    for k, v in env_keys.items():
        if k in by_name:
            by_name[k]["value"] = v
        else:
            env.append({"name": k, "value": v})
json.dump(td, open(path, "w"))
PY

  NEW_TD_ARN="$(aws ecs register-task-definition \
    --cli-input-json "file://$TMP_TD" \
    --region "$AWS_REGION" \
    --query 'taskDefinition.taskDefinitionArn' \
    --output text)"
  echo "    New task definition: $NEW_TD_ARN"

  aws ecs update-service \
    --cluster "$ECS_CLUSTER" \
    --service "$ECS_API_SERVICE" \
    --task-definition "$NEW_TD_ARN" \
    --force-new-deployment \
    --region "$AWS_REGION" >/dev/null
  echo "    Forced ECS redeploy of $ECS_API_SERVICE"
  rm -f "$TMP_TD"
  echo
fi

echo "==> Next steps"
echo "  1) Wait 10–30 seconds for IAM (and ECS rollout if UPDATE_ECS_ENV=true)."
echo "  2) Open Admin → AI settings and click Test Bedrock."
echo "  3) Current running app still uses amazon.nova-lite-v1:0 unless you:"
echo "       UPDATE_ECS_ENV=true bash ./fix-bedrock-ecs-access.sh"
echo "     or deploy the code that auto-maps to ${INFERENCE_PROFILE_ID}."
echo
echo "Done."
