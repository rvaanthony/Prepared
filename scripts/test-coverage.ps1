# PowerShell script to run tests with code coverage and generate HTML report
# Usage: .\scripts\test-coverage.ps1
# Note: This script should be run from the solution root directory

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Split-Path -Parent $scriptPath

# Change to solution root to ensure paths work correctly
Push-Location $solutionRoot

try {
    Write-Host "Running tests with code coverage..." -ForegroundColor Green

    # Clean previous coverage results
    if (Test-Path ".\TestResults") {
        Remove-Item -Recurse -Force ".\TestResults"
    }
    if (Test-Path ".\coverage") {
        Remove-Item -Recurse -Force ".\coverage"
    }

    # Run tests with coverage using coverlet.collector
    Write-Host "`nExecuting tests with coverage..." -ForegroundColor Yellow
    dotnet test Prepared.slnx `
        --collect:"XPlat Code Coverage" `
        --settings:.runsettings `
        --results-directory:"./TestResults" `
        --logger:"trx;LogFileName=test-results.trx" `
        --logger:"console;verbosity=normal" `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=json,cobertura

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nTests failed. Coverage report will still be generated from successful tests." -ForegroundColor Yellow
    }

    # Generate HTML report
    Write-Host "`nGenerating HTML coverage report..." -ForegroundColor Yellow

    # Find coverage files (could be in test project subdirectories or root TestResults)
    $coverageFiles = @()
    $coverageFiles += Get-ChildItem -Path ".\TestResults" -Recurse -Include "coverage.cobertura.xml" -ErrorAction SilentlyContinue
    $coverageFiles += Get-ChildItem -Path ".\*Tests\TestResults" -Recurse -Include "coverage.cobertura.xml" -ErrorAction SilentlyContinue
    $coverageFile = $coverageFiles | Select-Object -First 1

    if ($coverageFile) {
        Write-Host "Found coverage file: $($coverageFile.FullName)" -ForegroundColor Cyan
        
        # If multiple coverage files found, merge them
        if ($coverageFiles.Count -gt 1) {
            Write-Host "Found $($coverageFiles.Count) coverage files, merging..." -ForegroundColor Yellow
            $coveragePaths = ($coverageFiles | ForEach-Object { $_.FullName }) -join ';'
            reportgenerator `
                -reports:"$coveragePaths" `
                -targetdir:"coverage" `
                -reporttypes:"Html;Badges;TextSummary" `
                -classfilters:"-*Tests*;-*ServiceDefaults*" `
                -filefilters:"-*Tests*;-*Test*;-*ServiceDefaults*;-*Views*;-*Generated*"
        } else {
            reportgenerator `
                -reports:"$($coverageFile.FullName)" `
                -targetdir:"coverage" `
                -reporttypes:"Html;Badges;TextSummary" `
                -classfilters:"-*Tests*;-*ServiceDefaults*" `
                -filefilters:"-*Tests*;-*Test*;-*ServiceDefaults*;-*Views*;-*Generated*"
        }
        
        Write-Host "`nCoverage report generated successfully!" -ForegroundColor Green
        Write-Host "Open 'coverage\index.html' in your browser to view the report." -ForegroundColor Cyan
        
        # Display summary
        $summaryFile = "coverage\Summary.txt"
        if (Test-Path $summaryFile) {
            Write-Host "`nCoverage Summary:" -ForegroundColor Yellow
            Get-Content $summaryFile
        }
    } else {
        Write-Host "`nNo coverage files found. Make sure tests ran successfully." -ForegroundColor Red
    }
}
finally {
    Pop-Location
}

