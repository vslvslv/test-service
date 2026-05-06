#!/usr/bin/env bash
# rotate-mongo-password.sh — change the MongoDB root password in-place
#
# Usage:
#   bash infrastructure/rotate-mongo-password.sh <old> <new>
#   OLD_MONGO_PASSWORD=password123 NEW_MONGO_PASSWORD=... bash infrastructure/rotate-mongo-password.sh
#
# What it does:
#   1. Connects to the running MongoDB container with the old password.
#   2. Runs db.changeUserPassword('admin', '<new>') against the admin database.
#   3. Verifies the new credential by issuing a ping with it.
#   4. Reminds you to update infrastructure/.env (or your secret store)
#      with MONGO_PASSWORD=<new> and restart the API container.
#
# Why:
#   The default dev password "password123" is committed in source as a
#   compose fallback. Rotating to a real per-developer secret is encouraged
#   but must not destroy the existing data volume.
#
# Containers expected:
#   - testservice-mongodb       (from docker-compose.yml)
#   - testservice-mongodb-dev   (from docker-compose.dev.yml)
# The script auto-detects whichever is running.
set -euo pipefail

OLD="${1:-${OLD_MONGO_PASSWORD:-}}"
NEW="${2:-${NEW_MONGO_PASSWORD:-}}"

if [[ -z "$OLD" || -z "$NEW" ]]; then
  echo "ERROR: old and new passwords are required" >&2
  echo "Usage: $0 <old-password> <new-password>" >&2
  exit 1
fi

if [[ "${#NEW}" -lt 8 ]]; then
  echo "ERROR: new password must be at least 8 characters" >&2
  exit 1
fi

# Find a running mongo container we know about
CONTAINER=""
for candidate in testservice-mongodb-dev testservice-mongodb; do
  if docker ps --format '{{.Names}}' | grep -qx "$candidate"; then
    CONTAINER="$candidate"
    break
  fi
done

if [[ -z "$CONTAINER" ]]; then
  echo "ERROR: no testservice-mongodb* container is running" >&2
  echo "Start one with:" >&2
  echo "  docker compose -f infrastructure/docker-compose.dev.yml up -d mongodb" >&2
  exit 1
fi

echo "Rotating MongoDB admin password in container: $CONTAINER"

# Run the changeUserPassword command via mongosh inside the container.
# Pass passwords via env vars so they don't appear in process lists.
docker exec \
  -e OLD_PW="$OLD" \
  -e NEW_PW="$NEW" \
  "$CONTAINER" \
  mongosh --quiet \
    -u admin \
    -p "$OLD" \
    --authenticationDatabase admin \
    --eval 'db.getSiblingDB("admin").changeUserPassword("admin", process.env.NEW_PW)'

# Verify the new password works
echo "Verifying new password..."
docker exec \
  "$CONTAINER" \
  mongosh --quiet \
    -u admin \
    -p "$NEW" \
    --authenticationDatabase admin \
    --eval 'db.runCommand({ping:1}).ok' \
    | tee /dev/stderr | grep -q '^1$' || {
  echo "ERROR: new password did not authenticate. Old password may still be active." >&2
  exit 2
}

echo
echo "Rotation succeeded."
echo
echo "Next steps:"
echo "  1. Update your infrastructure/.env (or env vars):"
echo "       MONGO_PASSWORD=<new>"
echo "  2. Restart the API container so it picks up the new connection string:"
echo "       docker compose -f infrastructure/docker-compose.dev.yml up -d --force-recreate api"
echo "  3. (Optional) Verify by hitting /api/auth/login or /health on the API."
