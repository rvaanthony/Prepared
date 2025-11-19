#!/bin/bash
# Bash script to run tests with code coverage and generate HTML report
# Usage: ./scripts/test-coverage.sh
# Note: This script should be run from the solution root directory

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SOLUTION_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$SOLUTION_ROOT"

echo "Running tests with code coverage..."

# Clean previous coverage results
rm -rf ./TestResults
rm -rf ./coverage

# Run tests with coverage
echo ""
echo "Executing tests..."
dotnet test Prepared.slnx \
    --collect:"XPlat Code Coverage" \
    --settings:.runsettings \
    --results-directory:"./TestResults" \
    --logger:"trx;LogFileName=test-results.trx" \
    --logger:"console;verbosity=normal"

# Generate HTML report
echo ""
echo "Generating HTML coverage report..."

COVERAGE_FILE=$(find ./TestResults -name "coverage.cobertura.xml" | head -n 1)

if [ -n "$COVERAGE_FILE" ]; then
    reportgenerator \
        -reports:"$COVERAGE_FILE" \
        -targetdir:"coverage" \
        -reporttypes:"Html;Badges;TextSummary" \
        -classfilters:"-*Tests*" \
        -filefilters:"-*Tests*;-*Test*"
    
    echo ""
    echo "Coverage report generated successfully!"
    echo "Open 'coverage/index.html' in your browser to view the report."
    
    # Display summary
    if [ -f "coverage/Summary.txt" ]; then
        echo ""
        echo "Coverage Summary:"
        cat coverage/Summary.txt
    fi
else
    echo ""
    echo "No coverage files found. Make sure tests ran successfully."
fi

