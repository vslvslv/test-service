@echo off
REM ============================================================================
REM Test Service - Full Stack Startup (Windows)
REM ============================================================================
REM Starts all services: MongoDB, RabbitMQ, API, and Web UI
REM ============================================================================

echo.
echo ??????????????????????????????????????????????????????????????????
echo ?         Test Service - Full Stack Startup                     ?
echo ??????????????????????????????????????????????????????????????????
echo.

REM Check if Docker is running
echo [1/5] Checking Docker status...
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo ? Docker is not running!
    echo.
    echo Please start Docker Desktop first.
    echo.
    pause
    exit /b 1
)
echo ? Docker is running
echo.

REM Check if docker-compose file exists
if not exist "infrastructure\docker-compose.yml" (
    echo ? docker-compose.yml not found!
    echo.
    echo Please run from the solution root directory.
    echo.
    pause
    exit /b 1
)

REM Build images
echo [2/5] Building Docker images...
echo This may take a few minutes on first run...
docker-compose -f infrastructure\docker-compose.yml build --no-cache

if %errorlevel% neq 0 (
    echo ? Failed to build images!
    echo.
    pause
    exit /b 1
)
echo ? Images built successfully
echo.

REM Start all services
echo [3/5] Starting all services...
docker-compose -f infrastructure\docker-compose.yml up -d

if %errorlevel% neq 0 (
    echo ? Failed to start services!
    echo.
    pause
    exit /b 1
)
echo ? Services started
echo.

REM Wait for services to be healthy
echo [4/5] Waiting for services to be healthy...
echo This may take 30-60 seconds...
timeout /t 10 /nobreak >nul

:wait_loop
set /a count=0
:check_loop
if %count% geq 60 goto :timeout_error

REM Check all service health
docker inspect --format "{{.State.Health.Status}}" testservice-mongodb 2>nul | findstr "healthy" >nul 2>&1
set mongo_healthy=%errorlevel%

docker inspect --format "{{.State.Health.Status}}" testservice-rabbitmq 2>nul | findstr "healthy" >nul 2>&1
set rabbitmq_healthy=%errorlevel%

docker inspect --format "{{.State.Health.Status}}" testservice-api 2>nul | findstr "healthy" >nul 2>&1
set api_healthy=%errorlevel%

docker inspect --format "{{.State.Health.Status}}" testservice-web 2>nul | findstr "healthy" >nul 2>&1
set web_healthy=%errorlevel%

if %mongo_healthy% equ 0 if %rabbitmq_healthy% equ 0 if %api_healthy% equ 0 if %web_healthy% equ 0 goto :services_ready

timeout /t 1 /nobreak >nul
set /a count+=1
goto :check_loop

:services_ready
echo ? All services are healthy!
echo.

REM Display service status
echo [5/5] Service Status:
echo ????????????????????????????????????????????????????????????????
echo.

docker ps --filter "name=testservice" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo.

echo ????????????????????????????????????????????????????????????????
echo Full Stack is running! ??
echo ????????????????????????????????????????????????????????????????
echo.
echo ?? Services:
echo.
echo   ? MongoDB
echo      ?? mongodb://admin:password123@localhost:27017/TestServiceDb
echo.
echo   ? RabbitMQ
echo      ?? AMQP: localhost:5672
echo      ?? Management: http://localhost:15672 (guest/guest)
echo.
echo   ? API Service
echo      ?? API: http://localhost:5000
echo      ?? Swagger: http://localhost:5000/swagger
echo      ?? Health: http://localhost:5000/health
echo.
echo   ? Web UI
echo      ?? Application: http://localhost:3000
echo      ?? Health: http://localhost:3000/health
echo.
echo ????????????????????????????????????????????????????????????????
echo.
echo ?? Management Commands:
echo.
echo   • View logs:        infrastructure\logs-full.bat
echo   • Check status:     infrastructure\status-full.bat
echo   • Stop all:         infrastructure\stop-full.bat
echo   • Restart all:      infrastructure\restart-full.bat
echo.
pause
goto :end

:timeout_error
echo ??  Timeout waiting for services to be healthy!
echo.
echo Services may still be starting. Check with:
echo   docker-compose -f infrastructure\docker-compose.yml ps
echo   docker-compose -f infrastructure\docker-compose.yml logs
echo.
pause
exit /b 1

:end
