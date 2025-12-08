#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and publish Test Service containers
.DESCRIPTION
    Builds Docker images for the API and Web UI, and optionally pushes them to a container registry
.PARAMETER Registry
    Container registry URL (e.g., docker.io/username, ghcr.io/username, azurecr.io)
.PARAMETER Tag
    Image tag (default: latest)
.PARAMETER Push
    Push images to registry after building
.PARAMETER NoBuild
    Skip building, only push existing images
.PARAMETER Platform
    Target platform (default: linux/amd64,linux/arm64)
.EXAMPLE
    ./build-and-publish.ps1
    # Build images locally
.EXAMPLE
    ./build-and-publish.ps1 -Registry "docker.io/myusername" -Tag "v1.0.0" -Push
    # Build and push to Docker Hub
.EXAMPLE
    ./build-and-publish.ps1 -Registry "ghcr.io/myorg" -Tag "latest" -Push
    # Build and push to GitHub Container Registry
#>

param(
    [string]$Registry = "",
    [string]$Tag = "latest",
    [switch]$Push = $false,
    [switch]$NoBuild = $false,
    [string]$Platform = "linux/amd64"
)

$ErrorActionPreference = "Stop"

# Configuration
$ApiImageName = "testservice-api"
$WebImageName = "testservice-web"
$ComposeFile = "infrastructure/docker-compose.yml"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Test Service - Build & Publish" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Determine image names
if ($Registry) {
    $ApiFullName = "$Registry/$ApiImageName`:$Tag"
    $WebFullName = "$Registry/$WebImageName`:$Tag"
} else {
    $ApiFullName = "$ApiImageName`:$Tag"
    $WebFullName = "$WebImageName`:$Tag"
}

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  API Image:  $ApiFullName" -ForegroundColor White
Write-Host "  Web Image:  $WebFullName" -ForegroundColor White
Write-Host "  Platform:   $Platform" -ForegroundColor White
Write-Host "  Push:       $Push" -ForegroundColor White
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check Docker
try {
    $dockerVersion = docker --version
    Write-Host "  ? Docker: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "  ? Docker not found! Please install Docker." -ForegroundColor Red
    exit 1
}

# Check Docker Buildx (for multi-platform builds)
try {
    docker buildx version | Out-Null
    Write-Host "  ? Docker Buildx: Available" -ForegroundColor Green
} catch {
    Write-Host "  ? Docker Buildx not available!" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Build images
if (-not $NoBuild) {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "Building Images" -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host ""

    # Create buildx builder if it doesn't exist
    Write-Host "Setting up Docker Buildx builder..." -ForegroundColor Yellow
    $builderName = "testservice-builder"
    $existingBuilder = docker buildx ls | Select-String $builderName
    
    if (-not $existingBuilder) {
        Write-Host "  Creating new builder: $builderName" -ForegroundColor White
        docker buildx create --name $builderName --use
    } else {
        Write-Host "  Using existing builder: $builderName" -ForegroundColor White
        docker buildx use $builderName
    }
    Write-Host ""

    # Build API image
    Write-Host "Building API image..." -ForegroundColor Yellow
    Write-Host "  Source: TestService.Api" -ForegroundColor White
    Write-Host "  Target: $ApiFullName" -ForegroundColor White
    
    $buildArgs = @(
        "buildx", "build",
        "--platform", $Platform,
        "-t", $ApiFullName,
        "-f", "TestService.Api/Dockerfile"
    )
    
    if ($Push) {
        $buildArgs += "--push"
    } else {
        $buildArgs += "--load"
    }
    
    $buildArgs += "."
    
    & docker $buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ? API build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ? API image built successfully" -ForegroundColor Green
    Write-Host ""

    # Build Web image
    Write-Host "Building Web image..." -ForegroundColor Yellow
    Write-Host "  Source: testservice-web" -ForegroundColor White
    Write-Host "  Target: $WebFullName" -ForegroundColor White
    
    $buildArgs = @(
        "buildx", "build",
        "--platform", $Platform,
        "-t", $WebFullName,
        "-f", "testservice-web/Dockerfile"
    )
    
    if ($Push) {
        $buildArgs += "--push"
    } else {
        $buildArgs += "--load"
    }
    
    $buildArgs += "testservice-web"
    
    & docker $buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ? Web build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ? Web image built successfully" -ForegroundColor Green
    Write-Host ""
}

# Push images
if ($Push -and $NoBuild) {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "Pushing Images" -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not $Registry) {
        Write-Host "  ? Registry not specified! Use -Registry parameter" -ForegroundColor Red
        exit 1
    }

    Write-Host "Pushing API image..." -ForegroundColor Yellow
    docker push $ApiFullName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ? API push failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ? API image pushed successfully" -ForegroundColor Green
    Write-Host ""

    Write-Host "Pushing Web image..." -ForegroundColor Yellow
    docker push $WebFullName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ? Web push failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ? Web image pushed successfully" -ForegroundColor Green
    Write-Host ""
}

# Summary
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

if (-not $NoBuild) {
    Write-Host "Built images:" -ForegroundColor Yellow
    Write-Host "  • $ApiFullName" -ForegroundColor White
    Write-Host "  • $WebFullName" -ForegroundColor White
    Write-Host ""
}

if ($Push) {
    Write-Host "Images pushed to: $Registry" -ForegroundColor Green
    Write-Host ""
}

Write-Host "Next steps:" -ForegroundColor Yellow
if (-not $Push) {
    Write-Host "  1. Test locally: docker compose up" -ForegroundColor White
    Write-Host "  2. Push to registry: ./build-and-publish.ps1 -Registry 'your-registry' -Push" -ForegroundColor White
} else {
    Write-Host "  1. Pull and run on any server:" -ForegroundColor White
    Write-Host "     docker compose -f infrastructure/docker-compose.yml pull" -ForegroundColor White
    Write-Host "     docker compose -f infrastructure/docker-compose.yml up -d" -ForegroundColor White
    Write-Host ""
    Write-Host "  2. Or update your docker-compose.yml to use published images:" -ForegroundColor White
    Write-Host "     image: $ApiFullName" -ForegroundColor White
    Write-Host "     image: $WebFullName" -ForegroundColor White
}

Write-Host ""
Write-Host "? Done!" -ForegroundColor Green
