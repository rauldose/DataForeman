#!/bin/bash

# Script to capture screenshots of State Machine Builder and Trend Visualizer
# using Playwright tests

set -e

echo "📸 State Machine & Trend Visualizer Screenshot Generator"
echo "========================================================="
echo ""

# Check if Playwright is installed
if ! npx playwright --version &> /dev/null; then
    echo "📦 Installing Playwright..."
    npm install
fi

# Install Chromium browser
echo "🌐 Installing Chromium browser..."
npx playwright install chromium

# Create screenshots directory
mkdir -p screenshots

echo ""
echo "🔍 Checking application status..."
echo ""

# Check if Blazor app is running on port 5000
if curl -s http://localhost:5000 > /dev/null 2>&1; then
    echo "✅ Blazor app is running on http://localhost:5000"
    BASE_URL="http://localhost:5000"
elif curl -s http://localhost:8080 > /dev/null 2>&1; then
    echo "✅ Frontend app is running on http://localhost:8080"
    BASE_URL="http://localhost:8080"
else
    echo "❌ Application not running!"
    echo ""
    echo "Please start the DataForeman.App Blazor application:"
    echo "  cd src/DataForeman.App"
    echo "  dotnet run"
    echo ""
    echo "Or start via the main startup script:"
    echo "  npm run start"
    echo ""
    exit 1
fi

echo ""
echo "🧪 Running screenshot tests..."
echo ""

# Set base URL and run only the new test file
BASE_URL=$BASE_URL npx playwright test tests/e2e/06-state-machine-and-trends.spec.js --reporter=list

echo ""
echo "✅ Screenshots captured!"
echo ""
echo "📸 Screenshots saved to: ./screenshots/"
echo "   - 10-state-machines-list.png"
echo "   - 11-state-machine-editor.png"
echo "   - 12-trends-list.png"
echo "   - 13-trend-timeline.png"
echo "   - And more..."
echo ""
echo "📊 To view HTML report: npx playwright show-report"
echo ""
