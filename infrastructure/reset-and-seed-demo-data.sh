#!/usr/bin/env sh
set -eu

BASE_URL="http://localhost:5000"
ADMIN_USERNAME="admin"
ADMIN_PASSWORD="Admin@123"
DO_CLEANUP=1
DO_SEED=1
ASSUME_YES=0

usage() {
  cat <<EOF
Usage: $(basename "$0") [options]

Reset instance data and seed demo data:
- cleanup: users (except admin), mock expectations/logs, schemas, non-default environments
- seed: users with different permissions, environments, schemas, entities, mock expectations + traffic

Options:
  --base-url URL        API base URL (default: $BASE_URL)
  --username USER       Admin username (default: $ADMIN_USERNAME)
  --password PASS       Admin password (default: $ADMIN_PASSWORD)
  --cleanup-only        Only cleanup existing data
  --seed-only           Only seed demo data
  --yes                 Skip confirmation prompt
  -h, --help            Show this help
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --base-url)
      BASE_URL="$2"
      shift 2
      ;;
    --username)
      ADMIN_USERNAME="$2"
      shift 2
      ;;
    --password)
      ADMIN_PASSWORD="$2"
      shift 2
      ;;
    --cleanup-only)
      DO_CLEANUP=1
      DO_SEED=0
      shift
      ;;
    --seed-only)
      DO_CLEANUP=0
      DO_SEED=1
      shift
      ;;
    --yes)
      ASSUME_YES=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      usage
      exit 1
      ;;
  esac
done

if ! command -v curl >/dev/null 2>&1; then
  echo "Missing dependency: curl"
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "Missing dependency: jq"
  exit 1
fi

if [ "$ASSUME_YES" -ne 1 ]; then
  echo "This will modify data on $BASE_URL."
  echo "Cleanup=$DO_CLEANUP Seed=$DO_SEED"
  printf "Continue? [y/N]: "
  read -r CONFIRM
  case "$CONFIRM" in
    y|Y|yes|YES) ;;
    *) echo "Aborted."; exit 0 ;;
  esac
fi

api() {
  METHOD="$1"
  PATHNAME="$2"
  BODY="${3:-}"
  if [ -n "$BODY" ]; then
    curl -sS -X "$METHOD" "$BASE_URL$PATHNAME" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "$BODY"
  else
    curl -sS -X "$METHOD" "$BASE_URL$PATHNAME" \
      -H "Authorization: Bearer $TOKEN"
  fi
}

api_status() {
  METHOD="$1"
  PATHNAME="$2"
  BODY="${3:-}"
  if [ -n "$BODY" ]; then
    curl -sS -o /dev/null -w "%{http_code}" -X "$METHOD" "$BASE_URL$PATHNAME" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "$BODY"
  else
    curl -sS -o /dev/null -w "%{http_code}" -X "$METHOD" "$BASE_URL$PATHNAME" \
      -H "Authorization: Bearer $TOKEN"
  fi
}

echo "Authenticating as $ADMIN_USERNAME..."
LOGIN_PAYLOAD=$(printf '{"username":"%s","password":"%s"}' "$ADMIN_USERNAME" "$ADMIN_PASSWORD")
LOGIN_RESPONSE=$(curl -sS -X POST "$BASE_URL/api/auth/login" -H "Content-Type: application/json" -d "$LOGIN_PAYLOAD")
TOKEN=$(printf "%s" "$LOGIN_RESPONSE" | jq -r '.token // empty')
if [ -z "$TOKEN" ]; then
  echo "Failed to authenticate. Response:"
  echo "$LOGIN_RESPONSE"
  exit 1
fi

cleanup_users() {
  echo "Cleaning users..."
  USERS_JSON=$(api GET "/api/users")
  echo "$USERS_JSON" | jq -r '.[] | select(.username != "admin") | .id' | while IFS= read -r USER_ID; do
    [ -z "$USER_ID" ] && continue
    STATUS=$(api_status DELETE "/api/users/$USER_ID")
    echo "  delete user $USER_ID -> $STATUS"
  done
}

cleanup_mocks() {
  echo "Cleaning mocks (expectations + logs)..."
  EXPS_JSON=$(api GET "/api/mocks/expectations?includeDisabled=true")
  echo "$EXPS_JSON" | jq -r '.[]?.id' | while IFS= read -r EXP_ID; do
    [ -z "$EXP_ID" ] && continue
    STATUS=$(api_status DELETE "/api/mocks/expectations/$EXP_ID")
    echo "  delete expectation $EXP_ID -> $STATUS"
  done
  STATUS=$(api_status DELETE "/api/mocks/requests")
  echo "  delete all logs -> $STATUS"
}

cleanup_schemas() {
  echo "Cleaning schemas..."
  SCHEMAS_JSON=$(api GET "/api/schemas")
  echo "$SCHEMAS_JSON" | jq -r '.[].entityName' | while IFS= read -r SCHEMA_NAME; do
    [ -z "$SCHEMA_NAME" ] && continue
    api_status DELETE "/api/schemas/$SCHEMA_NAME/entities" >/dev/null || true
    STATUS=$(api_status DELETE "/api/schemas/$SCHEMA_NAME")
    echo "  delete schema $SCHEMA_NAME -> $STATUS"
  done
}

cleanup_environments() {
  echo "Cleaning non-default environments..."
  ENVS_JSON=$(api GET "/api/environments?includeInactive=true")
  echo "$ENVS_JSON" | jq -r '.[] | select(.name != "dev" and .name != "staging" and .name != "production") | .id' | while IFS= read -r ENV_ID; do
    [ -z "$ENV_ID" ] && continue
    STATUS=$(api_status DELETE "/api/environments/$ENV_ID")
    echo "  delete environment $ENV_ID -> $STATUS"
  done
}

seed_users() {
  echo "Seeding users..."
  api POST "/api/users" '{
    "username":"demo_admin",
    "email":"demo_admin@example.local",
    "password":"DemoAdmin@123",
    "firstName":"Demo",
    "lastName":"Admin",
    "role":1
  }' >/dev/null || true

  api POST "/api/users" '{
    "username":"demo_qa",
    "email":"demo_qa@example.local",
    "password":"DemoQa@123",
    "firstName":"Demo",
    "lastName":"QA",
    "role":0,
    "customPermissions":["mocks.read","mocks.verify","activity.read","schemas.read"]
  }' >/dev/null || true

  api POST "/api/users" '{
    "username":"demo_mocker",
    "email":"demo_mocker@example.local",
    "password":"DemoMock@123",
    "firstName":"Demo",
    "lastName":"Mocker",
    "role":0,
    "customPermissions":["mocks.read","mocks.write","mocks.logs.read","mocks.logs.delete","mocks.verify"]
  }' >/dev/null || true

  api POST "/api/users" '{
    "username":"demo_viewer",
    "email":"demo_viewer@example.local",
    "password":"DemoView@123",
    "firstName":"Demo",
    "lastName":"Viewer",
    "role":0,
    "customPermissions":["dashboard.read","entities.read","activity.read"]
  }' >/dev/null || true
}

seed_environments() {
  echo "Seeding environments..."
  api POST "/api/environments" '{
    "name":"qa",
    "displayName":"QA",
    "description":"Quality Assurance environment"
  }' >/dev/null || true

  api POST "/api/environments" '{
    "name":"uat",
    "displayName":"UAT",
    "description":"User Acceptance Testing environment"
  }' >/dev/null || true
}

seed_schemas() {
  echo "Seeding schemas..."
  api POST "/api/schemas" '{
    "entityName":"DemoCustomer",
    "fields":[
      {"name":"customerId","type":"string","required":true,"isUnique":true},
      {"name":"name","type":"string","required":true},
      {"name":"email","type":"string","required":true,"isUnique":true},
      {"name":"tier","type":"string","required":false},
      {"name":"active","type":"boolean","required":false}
    ],
    "filterableFields":["customerId","email","tier","active"],
    "excludeOnFetch":false
  }' >/dev/null || true

  api POST "/api/schemas" '{
    "entityName":"DemoOrder",
    "fields":[
      {"name":"orderId","type":"string","required":true,"isUnique":true},
      {"name":"customerId","type":"string","required":true},
      {"name":"status","type":"string","required":true},
      {"name":"amount","type":"number","required":true},
      {"name":"currency","type":"string","required":true}
    ],
    "filterableFields":["orderId","customerId","status"],
    "excludeOnFetch":false
  }' >/dev/null || true

  api POST "/api/schemas" '{
    "entityName":"DemoApiCredential",
    "fields":[
      {"name":"service","type":"string","required":true},
      {"name":"clientId","type":"string","required":true},
      {"name":"secret","type":"string","required":true},
      {"name":"region","type":"string","required":false}
    ],
    "filterableFields":["service","region"],
    "excludeOnFetch":false
  }' >/dev/null || true
}

seed_entities() {
  echo "Seeding entities..."
  for ENV in dev qa uat; do
    api POST "/api/entities/DemoCustomer" "$(printf '{
      "environment":"%s",
      "fields":{
        "customerId":"%s-cust-001",
        "name":"%s Customer One",
        "email":"cust1-%s@example.local",
        "tier":"gold",
        "active":true
      }
    }' "$ENV" "$ENV" "$ENV" "$ENV")" >/dev/null || true

    api POST "/api/entities/DemoCustomer" "$(printf '{
      "environment":"%s",
      "fields":{
        "customerId":"%s-cust-002",
        "name":"%s Customer Two",
        "email":"cust2-%s@example.local",
        "tier":"silver",
        "active":true
      }
    }' "$ENV" "$ENV" "$ENV" "$ENV")" >/dev/null || true

    api POST "/api/entities/DemoOrder" "$(printf '{
      "environment":"%s",
      "fields":{
        "orderId":"%s-ord-1001",
        "customerId":"%s-cust-001",
        "status":"created",
        "amount":125.50,
        "currency":"USD"
      }
    }' "$ENV" "$ENV" "$ENV")" >/dev/null || true

    api POST "/api/entities/DemoApiCredential" "$(printf '{
      "environment":"%s",
      "fields":{
        "service":"payments",
        "clientId":"%s-payments-client",
        "secret":"%s-secret-001",
        "region":"us-east-1"
      }
    }' "$ENV" "$ENV" "$ENV")" >/dev/null || true
  done
}

seed_mocks() {
  echo "Seeding mock expectations..."
  api POST "/api/mocks/expectations" '{
    "environment":"dev",
    "name":"demo-web-payment-json",
    "priority":30,
    "enabled":true,
    "requestMatcher":{
      "method":"POST",
      "path":"/payments",
      "pathMatchType":0,
      "headers":{"X-Application-Type":"web","Content-Type":"application/json"},
      "bodyMatchType":0
    },
    "responseTemplate":{
      "status":200,
      "headers":{"Content-Type":"application/json"},
      "body":"{\"source\":\"web\",\"ok\":true}"
    },
    "times":{"unlimited":true,"remaining":0}
  }' >/dev/null || true

  api POST "/api/mocks/expectations" '{
    "environment":"dev",
    "name":"demo-mobile-payment-xml",
    "priority":25,
    "enabled":true,
    "requestMatcher":{
      "method":"POST",
      "path":"/payments",
      "pathMatchType":0,
      "headers":{"X-Application-Type":"mobile","Content-Type":"application/xml"},
      "bodyMatchType":0
    },
    "responseTemplate":{
      "status":201,
      "headers":{"Content-Type":"application/json"},
      "body":"{\"source\":\"mobile\",\"ok\":true}"
    },
    "times":{"unlimited":true,"remaining":0}
  }' >/dev/null || true

  api POST "/api/mocks/expectations" '{
    "environment":"qa",
    "name":"demo-admin-users",
    "priority":20,
    "enabled":true,
    "requestMatcher":{
      "method":"GET",
      "path":"/users",
      "pathMatchType":0,
      "headers":{"X-Application-Type":"admin"},
      "bodyMatchType":0
    },
    "responseTemplate":{
      "status":200,
      "headers":{"Content-Type":"application/json"},
      "body":"{\"users\":5,\"source\":\"qa-admin\"}"
    },
    "times":{"unlimited":true,"remaining":0}
  }' >/dev/null || true
}

generate_mock_traffic() {
  echo "Generating mock traffic for logs/graph..."
  i=1
  while [ "$i" -le 6 ]; do
    curl -sS -o /dev/null -X POST "$BASE_URL/mock/dev/payments" \
      -H "Content-Type: application/json" \
      -H "X-Application-Type: web" \
      -d "{\"paymentId\":$i}" || true

    curl -sS -o /dev/null -X POST "$BASE_URL/mock/dev/payments" \
      -H "Content-Type: application/xml" \
      -H "X-Application-Type: mobile" \
      -d "<payment id=\"$i\"/>" || true

    curl -sS -o /dev/null "$BASE_URL/mock/qa/users" \
      -H "X-Application-Type: admin" || true

    if [ "$i" -le 2 ]; then
      curl -sS -o /dev/null "$BASE_URL/mock/dev/unknown-$i" || true
    fi

    sleep 1
    i=$((i + 1))
  done
}

if [ "$DO_CLEANUP" -eq 1 ]; then
  cleanup_users
  cleanup_mocks
  cleanup_schemas
  cleanup_environments
fi

if [ "$DO_SEED" -eq 1 ]; then
  seed_users
  seed_environments
  seed_schemas
  seed_entities
  seed_mocks
  generate_mock_traffic
fi

echo "Done."
echo "You can verify in UI:"
echo "  Users: demo_admin, demo_qa, demo_mocker, demo_viewer"
echo "  Schemas: DemoCustomer, DemoOrder, DemoApiCredential"
echo "  Mocks envs: dev, qa"
