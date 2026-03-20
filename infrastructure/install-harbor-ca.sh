#!/bin/bash
# Install Harbor's CA certificate so Docker (and buildx) trusts TLS when pushing/pulling.
# Use when you see: x509: certificate signed by unknown authority
#
# Run once per machine. From repo root or infrastructure/:
#   ./infrastructure/install-harbor-ca.sh
# On Linux: requires sudo. On macOS: installs into your login keychain (no sudo).

set -e

HARBOR_HOST="${HARBOR_HOST:-harbor.qa2-env01.cloudad.local}"

echo "Fetching certificate from $HARBOR_HOST:443 ..."
TMP_CERT=$(mktemp)
trap 'rm -f "$TMP_CERT"' EXIT
echo | openssl s_client -servername "$HARBOR_HOST" -connect "${HARBOR_HOST}:443" 2>/dev/null | openssl x509 > "$TMP_CERT"
if ! [[ -s "$TMP_CERT" ]]; then
    echo "Failed to retrieve certificate. Check connectivity to $HARBOR_HOST:443"
    exit 1
fi

case "$(uname)" in
    Linux)
        CERT_DIR="/etc/docker/certs.d/$HARBOR_HOST"
        SYSTEM_CA_DIR="/usr/local/share/ca-certificates"
        CERT_NAME="harbor.crt"
        echo "Installing CA for Docker and system ..."
        sudo mkdir -p "$CERT_DIR"
        sudo cp "$TMP_CERT" "$CERT_DIR/ca.crt"
        sudo cp "$TMP_CERT" "$SYSTEM_CA_DIR/$CERT_NAME"
        sudo update-ca-certificates
        echo "Restarting Docker ..."
        sudo systemctl restart docker
        echo "Done. Docker and buildx will now trust $HARBOR_HOST. Run: docker login $HARBOR_HOST"
        ;;
    Darwin)
        # Use standard login keychain path; security default-keychain can return name-only or odd formatting
        KEYCHAIN="${HOME}/Library/Keychains/login.keychain-db"
        if [[ ! -f "$KEYCHAIN" ]]; then
            KEYCHAIN="${HOME}/Library/Keychains/login.keychain"  # older macOS
        fi
        if [[ ! -f "$KEYCHAIN" ]]; then
            echo "Login keychain not found at $KEYCHAIN. Install the cert manually in Keychain Access."
            exit 1
        fi
        echo "Adding CA to keychain: $KEYCHAIN"
        security add-certificate -k "$KEYCHAIN" "$TMP_CERT"
        echo "Done. Harbor CA added to your keychain."
        echo ""
        echo "Set it to \"Always Trust\" for SSL: open Keychain Access, find the"
        echo "\"$HARBOR_HOST\" certificate, double-click → Trust → \"When using this certificate\" = Always Trust."
        echo ""
        echo "Note: buildx runs inside a Linux container and does not use the Mac keychain."
        echo "Use ./infrastructure/build-and-push-harbor.sh to push (it uses an insecure-registry"
        echo "config for the builder). If push still fails, add Harbor to Docker Desktop:"
        echo "  Settings → Docker Engine → \"insecure-registries\": [\"$HARBOR_HOST\"]"
        echo ""
        echo "Restart Docker Desktop, then: docker login $HARBOR_HOST"
        ;;
    *)
        echo "Unsupported OS: $(uname). Use Linux or macOS."
        exit 1
        ;;
esac
