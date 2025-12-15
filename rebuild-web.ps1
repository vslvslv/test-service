#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick rebuild script for Test Service Web UI
.DESCRIPTION
    Rebuilds only the web container with clean build (no cache) and recreates it
.PARAMETER NoCache
    Force rebuild without using Docker cache
.EXAMPLE
    .\rebuild-web.ps1
    # Rebuild with cache
.EXAMPLE
    .\rebuild-web.ps1 -NoCache
    # Clean rebuild without cache
#>

param(
    [switch]$NoCache = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Rebuilding Web Container" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
try {
    docker ps | Out-Null
} catch {
    Write-Host "? Docker is not running! Please start Docker Desktop." -ForegroundColor Red
    exit 1
}

Write-Host "?? Building web image..." -ForegroundColor Yellow
Write-Host ""

# Build command
$buildArgs = @(
    "build",
    "-t", "testservice-web:latest",
    "-f", "testservice-web/Dockerfile"
)

if ($NoCache) {
    Write-Host "?? Building with --no-cache flag (clean build)" -ForegroundColor Yellow
    $buildArgs += "--no-cache"
}

$buildArgs += "testservice-web"

& docker $buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "? Build successful!" -ForegroundColor Green
Write-Host ""

# Check if container is running
Write-Host "?? Checking if web container is running..." -ForegroundColor Yellow
$runningContainer = docker ps --filter "name=testservice-web" --format "{{.Names}}"

if ($runningContainer) {
    Write-Host "   Found running container: $runningContainer" -ForegroundColor White
    Write-Host ""
    Write-Host "?? Recreating web container..." -ForegroundColor Yellow
    
    docker compose -f infrastructure/docker-compose.yml up -d --force-recreate web
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? Failed to recreate container!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "? Waiting for container to be healthy..." -ForegroundColor Yellow
    Start-Sleep -Seconds 3
    
    # Check container status
    $containerStatus = docker inspect --format='{{.State.Health.Status}}' testservice-web 2>$null
    
    if ($containerStatus) {
        Write-Host "   Health status: $containerStatus" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "? Container recreated successfully!" -ForegroundColor Green
} else {
    Write-Host "   No running container found." -ForegroundColor Gray
    Write-Host ""
    Write-Host "?? To start the container, run:" -ForegroundColor Yellow
    Write-Host "   docker compose -f infrastructure/docker-compose.yml up -d" -ForegroundColor White
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "? Web image rebuilt: testservice-web:latest" -ForegroundColor Green

if ($runningContainer) {
    Write-Host "? Container recreated and running" -ForegroundColor Green
}

Write-Host ""
Write-Host "?? Access the web UI:" -ForegroundColor Yellow
Write-Host "   http://localhost:3000" -ForegroundColor White
Write-Host ""
Write-Host "?? View logs:" -ForegroundColor Yellow
Write-Host "   docker logs -f testservice-web" -ForegroundColor White
Write-Host ""
Write-Host "? Done!" -ForegroundColor Green
