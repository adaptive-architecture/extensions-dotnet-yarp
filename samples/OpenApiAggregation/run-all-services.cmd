@echo off
REM Batch script to run all three services concurrently for testing
REM Usage: run-all-services.cmd

echo ====================================
echo Starting YARP OpenAPI Aggregation Demo
echo ====================================
echo.

echo [1/3] Starting UserService on http://localhost:5001...
start "UserService" /D "%~dp0UserService" dotnet run

timeout /t 2 /nobreak >nul

echo [2/3] Starting ProductService on http://localhost:5002...
start "ProductService" /D "%~dp0ProductService" dotnet run

timeout /t 2 /nobreak >nul

echo [3/3] Starting Gateway on http://localhost:5000...
start "Gateway" /D "%~dp0Gateway" dotnet run

timeout /t 3 /nobreak >nul

echo.
echo ====================================
echo All services are starting up...
echo ====================================
echo.
echo Services:
echo   * UserService:    http://localhost:5001/swagger
echo   * ProductService: http://localhost:5002/swagger
echo   * Gateway:        http://localhost:5000
echo.
echo OpenAPI Aggregation Endpoints:
echo   * List services:        http://localhost:5000/api-docs
echo   * User Management API:  http://localhost:5000/api-docs/user-management
echo   * Product Catalog API:  http://localhost:5000/api-docs/product-catalog
echo.
echo Proxied API Requests:
echo   * GET  http://localhost:5000/api/users
echo   * GET  http://localhost:5000/api/products
echo.
echo.
echo Each service is running in its own window.
echo Close the individual windows to stop each service.
echo.
pause
