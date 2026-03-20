#!/bin/bash
# Build Test Service images for Linux (amd64 + arm64), tag with version from
# infrastructure/VERSION, and push to Harbor.
#
# Usage (from repo root or infrastructure/):
#   ./infrastructure/build-and-push-harbor.sh
#   ./infrastructure/build-and-push-harbor.sh --no-push
#   ./infrastructure/build-and-push-harbor.sh -t 1.0.3
#
# Prerequisites:
#   docker login harbor.qa2-env01.cloudad.local

set -e

# Script dir and repo root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VERSION_FILE="$SCRIPT_DIR/VERSION"

# Defaults
HARBOR_REGISTRY="${HARBOR_REGISTRY:-harbor.qa2-env01.cloudad.local/library}"
PLATFORM="${PLATFORM:-linux/amd64,linux/arm64}"
API_IMAGE_NAME="testservice-api"
WEB_IMAGE_NAME="testservice-ui"
PUSH=true

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
NC='\033[0m'

usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Build API and Web images for Linux (amd64 + arm64), tag with version from"
    echo "infrastructure/VERSION, and push to Harbor."
    echo ""
    echo "Options:"
    echo "  -t, --tag TAG     Override version (default: read from infrastructure/VERSION)"
    echo "  --no-push         Build only, do not push to registry"
    echo "  --platform SPEC   Platforms (default: linux/amd64,linux/arm64)"
    echo "  -h, --help        Show this help"
    echo ""
    echo "Environment:"
    echo "  HARBOR_REGISTRY  Registry/project (default: harbor.qa2-env01.cloudad.local/library)"
    echo "  PLATFORM         Same as --platform"
    echo ""
    echo "Example:"
    echo "  $0"
    echo "  $0 -t 1.0.3 --no-push"
}

# Parse args
while [[ $# -gt 0 ]]; do
    case $1 in
        -t|--tag)
            TAG_OVERRIDE="$2"
            shift 2
            ;;
        --no-push)
            PUSH=false
            shift
            ;;
        --platform)
            PLATFORM="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            usage
            exit 1
            ;;
    esac
done

# Resolve version
if [ -n "${TAG_OVERRIDE:-}" ]; then
    VERSION="$TAG_OVERRIDE"
else
    if [ ! -f "$VERSION_FILE" ]; then
        echo -e "${RED}VERSION file not found: $VERSION_FILE${NC}"
        exit 1
    fi
    VERSION=$(sed -n '1p' "$VERSION_FILE" | sed 's/[[:space:]]*#.*//; s/^[[:space:]]*//; s/[[:space:]]*$//')
    if [ -z "$VERSION" ]; then
        echo -e "${RED}Empty version in $VERSION_FILE${NC}"
        exit 1
    fi
fi

API_FULL="$HARBOR_REGISTRY/$API_IMAGE_NAME:$VERSION"
WEB_FULL="$HARBOR_REGISTRY/$WEB_IMAGE_NAME:$VERSION"

echo -e "${CYAN}=====================================${NC}"
echo -e "${CYAN}Test Service - Build & Push to Harbor${NC}"
echo -e "${CYAN}=====================================${NC}"
echo ""
echo -e "${YELLOW}Configuration:${NC}"
echo -e "  Registry:  ${WHITE}$HARBOR_REGISTRY${NC}"
echo -e "  Version:   ${WHITE}$VERSION${NC}"
echo -e "  API:       ${WHITE}$API_FULL${NC}"
echo -e "  Web:       ${WHITE}$WEB_FULL${NC}"
echo -e "  Platform:  ${WHITE}$PLATFORM${NC}"
echo -e "  Push:      ${WHITE}$PUSH${NC}"
echo ""

# Prereqs
echo -e "${YELLOW}Checking prerequisites...${NC}"
if ! command -v docker &> /dev/null; then
    echo -e "  ${RED}Docker not found.${NC}"
    exit 1
fi
if ! docker buildx version &> /dev/null; then
    echo -e "  ${RED}Docker Buildx not available.${NC}"
    exit 1
fi
echo -e "  ${GREEN}Docker and Buildx OK${NC}"
echo ""

# Buildx builder: use BuildKit config so the builder container trusts Harbor's TLS
# (certificate signed by unknown authority). Without this, push fails even when
# docker login succeeds on the host.
BUILDER_NAME="testservice-builder"
BUILDKIT_CONFIG="$SCRIPT_DIR/buildkit-insecure-harbor.toml"
echo -e "${YELLOW}Setting up Docker Buildx builder...${NC}"
if docker buildx ls | grep -q "$BUILDER_NAME"; then
    docker buildx rm "$BUILDER_NAME" 2>/dev/null || true
fi
docker buildx create --name "$BUILDER_NAME" --use --config "$BUILDKIT_CONFIG"
echo ""

cd "$REPO_ROOT"

# With --no-push we must use a single platform (buildx --load does not support multi-platform)
BUILD_PLATFORM="$PLATFORM"
[ "$PUSH" = false ] && BUILD_PLATFORM="linux/amd64"
EXTRA_OPTS=(); [ "$PUSH" = true ] && EXTRA_OPTS=(--push) || EXTRA_OPTS=(--load)

# Build API
echo -e "${CYAN}Building API image...${NC}"
docker buildx build \
    --platform "$BUILD_PLATFORM" \
    -t "$API_FULL" \
    -f TestService.Api/Dockerfile \
    "${EXTRA_OPTS[@]}" \
    .
echo -e "  ${GREEN}API image built${NC}"
echo ""

# Build Web (context: testservice-web)
echo -e "${CYAN}Building Web image...${NC}"
docker buildx build \
    --platform "$BUILD_PLATFORM" \
    -t "$WEB_FULL" \
    -f testservice-web/Dockerfile \
    "${EXTRA_OPTS[@]}" \
    testservice-web
echo -e "  ${GREEN}Web image built${NC}"
echo ""

echo -e "${CYAN}=====================================${NC}"
echo -e "${CYAN}Summary${NC}"
echo -e "${CYAN}=====================================${NC}"
echo -e "  ${WHITE}$API_FULL${NC}"
echo -e "  ${WHITE}$WEB_FULL${NC}"
if [ "$PUSH" = true ]; then
    echo ""
    echo -e "${GREEN}Images pushed to Harbor.${NC}"
    echo -e "${YELLOW}Update k8s manifests to image tag: $VERSION${NC}"
else
    echo ""
    echo -e "${YELLOW}Build only (--no-push). Push with: $0 -t $VERSION${NC}"
fi
echo -e "${GREEN}Done.${NC}"
