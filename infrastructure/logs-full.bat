@echo off
REM ============================================================================
REM Test Service - Logs Viewer for Full Stack (Windows)
REM ============================================================================

echo.
echo ??????????????????????????????????????????????????????????????????
echo ?         Test Service - Full Stack Logs                        ?
echo ??????????????????????????????????????????????????????????????????
echo.

echo Select service to view logs:
echo.
echo   1. All services
echo   2. MongoDB
echo   3. RabbitMQ
echo   4. API
echo   5. Web UI
echo   6. Exit
echo.

choice /C 123456 /M "Select option"

if errorlevel 6 goto :end
if errorlevel 5 goto :web
if errorlevel 4 goto :api
if errorlevel 3 goto :rabbitmq
if errorlevel 2 goto :mongodb
if errorlevel 1 goto :all

:all
echo.
echo Showing logs for all services (Ctrl+C to exit)...
echo.
docker-compose -f infrastructure\docker-compose.yml logs -f
goto :end

:mongodb
echo.
echo Showing MongoDB logs (Ctrl+C to exit)...
echo.
docker logs -f testservice-mongodb
goto :end

:rabbitmq
echo.
echo Showing RabbitMQ logs (Ctrl+C to exit)...
echo.
docker logs -f testservice-rabbitmq
goto :end

:api
echo.
echo Showing API logs (Ctrl+C to exit)...
echo.
docker logs -f testservice-api
goto :end

:web
echo.
echo Showing Web UI logs (Ctrl+C to exit)...
echo.
docker logs -f testservice-web
goto :end

:end
