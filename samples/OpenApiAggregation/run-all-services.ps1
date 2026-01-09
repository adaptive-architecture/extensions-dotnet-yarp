# PowerShell script to run all three services concurrently for testing
# Usage: .\run-all-services.ps1

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Starting YARP OpenAPI Aggregation Demo" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Store process objects
$processes = @()

try {
    # Start UserService
    Write-Host "[1/3] Starting UserService on http://localhost:5001..." -ForegroundColor Yellow
    $userServiceProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run" `
        -WorkingDirectory "$PSScriptRoot\UserService" `
        -PassThru `
        -WindowStyle Hidden
    $processes += $userServiceProcess
    
    # Wait a bit before starting next service
    Start-Sleep -Seconds 2
    
    # Start ProductService
    Write-Host "[2/3] Starting ProductService on http://localhost:5002..." -ForegroundColor Yellow
    $productServiceProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run" `
        -WorkingDirectory "$PSScriptRoot\ProductService" `
        -PassThru `
        -WindowStyle Hidden
    $processes += $productServiceProcess
    
    # Wait a bit before starting gateway
    Start-Sleep -Seconds 2
    
    # Start Gateway
    Write-Host "[3/3] Starting Gateway on http://localhost:5000..." -ForegroundColor Yellow
    $gatewayProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run" `
        -WorkingDirectory "$PSScriptRoot\Gateway" `
        -PassThru `
        -WindowStyle Hidden
    $processes += $gatewayProcess
    
    # Wait for services to start up
    Write-Host ""
    Write-Host "Waiting for services to start up..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    Write-Host ""
    Write-Host "====================================" -ForegroundColor Green
    Write-Host "All services are running!" -ForegroundColor Green
    Write-Host "====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Services:" -ForegroundColor Cyan
    Write-Host "  • UserService:    http://localhost:5001/swagger" -ForegroundColor White
    Write-Host "  • ProductService: http://localhost:5002/swagger" -ForegroundColor White
    Write-Host "  • Gateway:        http://localhost:5000" -ForegroundColor White
    Write-Host ""
    Write-Host "OpenAPI Aggregation Endpoints:" -ForegroundColor Cyan
    Write-Host "  • List services:        http://localhost:5000/api-docs" -ForegroundColor White
    Write-Host "  • User Management API:  http://localhost:5000/api-docs/user-management" -ForegroundColor White
    Write-Host "  • Product Catalog API:  http://localhost:5000/api-docs/product-catalog" -ForegroundColor White
    Write-Host ""
    Write-Host "Proxied API Requests:" -ForegroundColor Cyan
    Write-Host "  • GET  http://localhost:5000/api/users" -ForegroundColor White
    Write-Host "  • GET  http://localhost:5000/api/products" -ForegroundColor White
    Write-Host ""
    Write-Host "Press Ctrl+C to stop all services..." -ForegroundColor Yellow
    Write-Host ""
    
    # Monitor processes
    while ($true) {
        # Check if all processes are still running
        $runningCount = 0
        foreach ($proc in $processes) {
            if (-not $proc.HasExited) {
                $runningCount++
            }
        }
        
        if ($runningCount -lt 3) {
            Write-Host ""
            Write-Host "One or more services stopped unexpectedly!" -ForegroundColor Red
            Write-Host "Check the service logs for errors." -ForegroundColor Red
            break
        }
        
        Start-Sleep -Milliseconds 500
    }
}
finally {
    Write-Host ""
    Write-Host "Stopping all services..." -ForegroundColor Yellow
    
    # Stop all processes
    foreach ($proc in $processes) {
        if ($null -ne $proc -and -not $proc.HasExited) {
            try {
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            }
            catch {
                # Ignore errors during cleanup
            }
        }
    }
    
    Write-Host "All services stopped." -ForegroundColor Green
}
