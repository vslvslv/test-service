#!/bin/bash
# Build and publish Test Service containers (Bash version)

set -e

# Default values
REGISTRY=""
TAG="latest"
PUSH=false
NO_BUILD=false
PLATFORM="linux/amd64,linux/arm64"

# Configuration
API_IMAGE_NAME="testservice-api"
WEB_IMAGE_NAME="testservice-web"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -r|--registry)
            REGISTRY="$2"
            shift 2
            ;;
        -t|--tag)
            TAG="$2"
            shift 2
            ;;
        -p|--push)
            PUSH=true
            shift
            ;;
        --no-build)
            NO_BUILD=true
            shift
            ;;
        --platform)
            PLATFORM="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: ./build-and-publish.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -r, --registry REGISTRY   Container registry URL"
            echo "  -t, --tag TAG            Image tag (default: latest)"
            echo "  -p, --push               Push images to registry"
            echo "  --no-build               Skip building, only push"
            echo "  --platform PLATFORM      Target platform (default: linux/amd64,linux/arm64)"
            echo "  -h, --help               Show this help message"
            echo ""
            echo "Examples:"
            echo "  ./build-and-publish.sh"
            echo "  ./build-and-publish.sh -r docker.io/myusername -t v1.0.0 -p"
            echo "  ./build-and-publish.sh -r ghcr.io/myorg -p"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Determine image names
if [ -n "$REGISTRY" ]; then
    API_FULL_NAME="$REGISTRY/$API_IMAGE_NAME:$TAG"
    WEB_FULL_NAME="$REGISTRY/$WEB_IMAGE_NAME:$TAG"
else
    API_FULL_NAME="$API_IMAGE_NAME:$TAG"
    WEB_FULL_NAME="$WEB_IMAGE_NAME:$TAG"
fi

echo -e "${CYAN}=====================================${NC}"
echo -e "${CYAN}Test Service - Build & Publish${NC}"
echo -e "${CYAN}=====================================${NC}"
echo ""
echo -e "${YELLOW}Configuration:${NC}"
echo -e "  API Image:  ${WHITE}$API_FULL_NAME${NC}"
echo -e "  Web Image:  ${WHITE}$WEB_FULL_NAME${NC}"
echo -e "  Platform:   ${WHITE}$PLATFORM${NC}"
echo -e "  Push:       ${WHITE}$PUSH${NC}"
echo ""

# Check prerequisites
echo -e "${YELLOW}Checking prerequisites...${NC}"

if ! command -v docker &> /dev/null; then
    echo -e "  ${RED}? Docker not found! Please install Docker.${NC}"
    exit 1
fi
DOCKER_VERSION=$(docker --version)
echo -e "  ${GREEN}? Docker: $DOCKER_VERSION${NC}"

if ! docker buildx version &> /dev/null; then
    echo -e "  ${RED}? Docker Buildx not available!${NC}"
    exit 1
fi
echo -e "  ${GREEN}? Docker Buildx: Available${NC}"
echo ""

# Build images
if [ "$NO_BUILD" = false ]; then
    echo -e "${CYAN}=====================================${NC}"
    echo -e "${CYAN}Building Images${NC}"
    echo -e "${CYAN}=====================================${NC}"
    echo ""

    # Create buildx builder if it doesn't exist
    echo -e "${YELLOW}Setting up Docker Buildx builder...${NC}"
    BUILDER_NAME="testservice-builder"
    
    if ! docker buildx ls | grep -q "$BUILDER_NAME"; then
        echo -e "  ${WHITE}Creating new builder: $BUILDER_NAME${NC}"
        docker buildx create --name "$BUILDER_NAME" --use
    else
        echo -e "  ${WHITE}Using existing builder: $BUILDER_NAME${NC}"
        docker buildx use "$BUILDER_NAME"
    fi
    echo ""

    # Build API image
    echo -e "${YELLOW}Building API image...${NC}"
    echo -e "  ${WHITE}Source: TestService.Api${NC}"
    echo -e "  ${WHITE}Target: $API_FULL_NAME${NC}"
    
    BUILD_ARGS="buildx build --platform $PLATFORM -t $API_FULL_NAME -f TestService.Api/Dockerfile"
    
    if [ "$PUSH" = true ]; then
        BUILD_ARGS="$BUILD_ARGS --push"
    else
        BUILD_ARGS="$BUILD_ARGS --load"
    fi
    
    BUILD_ARGS="$BUILD_ARGS ."
    
    docker $BUILD_ARGS
    
    echo -e "  ${GREEN}? API image built successfully${NC}"
    echo ""

    # Build Web image
    echo -e "${YELLOW}Building Web image...${NC}"
    echo -e "  ${WHITE}Source: testservice-web${NC}"
    echo -e "  ${WHITE}Target: $WEB_FULL_NAME${NC}"
    
    BUILD_ARGS="buildx build --platform $PLATFORM -t $WEB_FULL_NAME -f testservice-web/Dockerfile"
    
    if [ "$PUSH" = true ]; then
        BUILD_ARGS="$BUILD_ARGS --push"
    else
        BUILD_ARGS="$BUILD_ARGS --load"
    fi
    
    BUILD_ARGS="$BUILD_ARGS testservice-web"
    
    docker $BUILD_ARGS
    
    echo -e "  ${GREEN}? Web image built successfully${NC}"
    echo ""
fi

# Push images
if [ "$PUSH" = true ] && [ "$NO_BUILD" = true ]; then
    echo -e "${CYAN}=====================================${NC}"
    echo -e "${CYAN}Pushing Images${NC}"
    echo -e "${CYAN}=====================================${NC}"
    echo ""

    if [ -z "$REGISTRY" ]; then
        echo -e "  ${RED}? Registry not specified! Use -r parameter${NC}"
        exit 1
    fi

    echo -e "${YELLOW}Pushing API image...${NC}"
    docker push "$API_FULL_NAME"
    echo -e "  ${GREEN}? API image pushed successfully${NC}"
    echo ""

    echo -e "${YELLOW}Pushing Web image...${NC}"
    docker push "$WEB_FULL_NAME"
    echo -e "  ${GREEN}? Web image pushed successfully${NC}"
    echo ""
fi

# Summary
echo -e "${CYAN}=====================================${NC}"
echo -e "${CYAN}Summary${NC}"
echo -e "${CYAN}=====================================${NC}"
echo ""

if [ "$NO_BUILD" = false ]; then
    echo -e "${YELLOW}Built images:${NC}"
    echo -e "  ${WHITE}• $API_FULL_NAME${NC}"
    echo -e "  ${WHITE}• $WEB_FULL_NAME${NC}"
    echo ""
fi

if [ "$PUSH" = true ]; then
    echo -e "${GREEN}Images pushed to: $REGISTRY${NC}"
    echo ""
fi

echo -e "${YELLOW}Next steps:${NC}"
if [ "$PUSH" = false ]; then
    echo -e "  ${WHITE}1. Test locally: docker compose up${NC}"
    echo -e "  ${WHITE}2. Push to registry: ./build-and-publish.sh -r your-registry -p${NC}"
else
    echo -e "  ${WHITE}1. Pull and run on any server:${NC}"
    echo -e "     ${WHITE}docker compose -f infrastructure/docker-compose.yml pull${NC}"
    echo -e "     ${WHITE}docker compose -f infrastructure/docker-compose.yml up -d${NC}"
    echo ""
    echo -e "  ${WHITE}2. Or update your docker-compose.yml to use published images:${NC}"
    echo -e "     ${WHITE}image: $API_FULL_NAME${NC}"
    echo -e "     ${WHITE}image: $WEB_FULL_NAME${NC}"
fi

echo ""
echo -e "${GREEN}? Done!${NC}"
