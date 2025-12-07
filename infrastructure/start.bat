@echo off
REM ============================================================================
REM Test Service Infrastructure - Start Script (Windows)
REM ============================================================================
REM This script starts MongoDB and RabbitMQ containers for local development
REM ============================================================================

echo.
echo ??????????????????????????????????????????????????????????????????
echo ?         Test Service - Infrastructure Startup                 ?
echo ??????????????????????????????????????????????????????????????????
echo.

REM Check if Docker is running
echo [1/5] Checking Docker status...
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo ? Docker is not running!
    echo.
    echo Please start Docker Desktop first:
    echo   1. Open Docker Desktop
    echo   2. Wait for Docker to fully start
    echo   3. Run this script again
    echo.
    pause
    exit /b 1
)
echo ? Docker is running
echo.

REM Check if containers are already running
echo [2/5] Checking existing containers...
docker ps --filter "name=testservice-mongodb" --filter "name=testservice-rabbitmq" --format "{{.Names}}" | findstr "testservice" >nul 2>&1
if %errorlevel% equ 0 (
    echo ??  Containers are already running
    echo.
    choice /C YN /M "Do you want to restart them?"
    if errorlevel 2 goto :check_health
    echo.
    echo Stopping existing containers...
    docker-compose -f infrastructure\docker-compose.yml down
    echo.
)

REM Start services
echo [3/5] Starting infrastructure services...
docker-compose -f infrastructure\docker-compose.yml up -d

if %errorlevel% neq 0 (
    echo ? Failed to start services!
    echo.
    echo Troubleshooting:
    echo   - Check if ports 27017, 5672, 15672 are available
    echo   - Run: netstat -ano ^| findstr "27017"
    echo   - Check Docker Desktop logs
    echo.
    pause
    exit /b 1
)
echo ? Services started
echo.

REM Wait for services to be ready
echo [4/5] Waiting for services to be healthy...
timeout /t 5 /nobreak >nul

:wait_loop
set /a count=0
:check_loop
if %count% geq 30 goto :timeout_error

REM Check MongoDB health
docker inspect --format "{{.State.Health.Status}}" testservice-mongodb 2>nul | findstr "healthy" >nul 2>&1
set mongo_healthy=%errorlevel%

REM Check RabbitMQ health
docker inspect --format "{{.State.Health.Status}}" testservice-rabbitmq 2>nul | findstr "healthy" >nul 2>&1
set rabbitmq_healthy=%errorlevel%

if %mongo_healthy% equ 0 if %rabbitmq_healthy% equ 0 goto :services_ready

timeout /t 1 /nobreak >nul
set /a count+=1
goto :check_loop

:services_ready
echo ? All services are healthy!
echo.

:check_health
REM Display service status
echo [5/5] Service Status:
echo ????????????????????????????????????????????????????????????????
echo.

docker ps --filter "name=testservice" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo.

REM Check and display MongoDB status
docker ps | findstr "testservice-mongodb" >nul
if %errorlevel% equ 0 (
    echo ? MongoDB
    echo    ?? Connection: mongodb://admin:password123@localhost:27017/TestServiceDb
    echo    ?? Database: TestServiceDb
) else (
    echo ? MongoDB is not running
)
echo.

REM Check and display RabbitMQ status
docker ps | findstr "testservice-rabbitmq" >nul
if %errorlevel% equ 0 (
    echo ? RabbitMQ
    echo    ?? AMQP Port: localhost:5672
    echo    ?? Management UI: http://localhost:15672
    echo    ?? Credentials: guest / guest
) else (
    echo ? RabbitMQ is not running
)
echo.

echo ????????????????????????????????????????????????????????????????
echo Infrastructure is ready! ??
echo ????????????????????????????????????????????????????????????????
echo.
echo Next Steps:
echo.
echo   1. Start the API:
echo      cd TestService.Api
echo      dotnet run
echo.
echo   2. Access Swagger UI:
echo      https://localhost:5001/swagger
echo.
echo   3. Run tests:
echo      cd TestService.Tests
echo      dotnet test
echo.
echo   4. Stop infrastructure:
echo      infrastructure\stop.bat
echo.
echo   5. View logs:
echo      infrastructure\logs.bat
echo.
pause
goto :end

:timeout_error
echo ? Timeout waiting for services to be healthy!
echo.
echo Services may still be starting. Check with:
echo   docker-compose -f infrastructure\docker-compose.yml ps
echo   docker-compose -f infrastructure\docker-compose.yml logs
echo.
pause
exit /b 1

:end
