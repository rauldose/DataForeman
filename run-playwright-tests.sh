#!/bin/bash

# Playwright Test Runner Script for DataForeman
# This script sets up and runs Playwright tests

set -e

echo "🎭 DataForeman Playwright Test Runner"
echo "======================================"
echo ""

# Check if Playwright browsers are installed
echo "📦 Checking Playwright installation..."
if ! npx playwright --version &> /dev/null; then
    echo "❌ Playwright not found. Installing..."
    npm install
fi

# Install browsers if needed
echo "🌐 Installing Playwright browsers (if needed)..."
npx playwright install chromium

# Create screenshots directory
echo "📁 Creating screenshots directory..."
mkdir -p screenshots

# Check if application is running
echo "🔍 Checking if application is running on http://localhost:8080..."
if curl -s http://localhost:8080 > /dev/null; then
    echo "✅ Application is running"
else
    echo "⚠️  Application not detected on localhost:8080"
    echo "   Please start the application first with:"
    echo "   npm run start"
    echo ""
    read -p "Continue anyway? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "❌ Tests cancelled"
        exit 1
    fi
fi

# Run Playwright tests
echo ""
echo "🧪 Running Playwright tests..."
echo ""

npx playwright test "$@"

# Show results
echo ""
echo "✅ Tests complete!"
echo ""
echo "📸 Screenshots saved to: ./screenshots/"
echo "📊 HTML report: npx playwright show-report"
echo ""
