#!/bin/bash
set -e

echo "=== Starting Xvfb virtual display ==="
Xvfb :99 -screen 0 ${XVFB_RESOLUTION} -ac +extension GLX +render -noreset &
sleep 2

echo "=== Verifying display ==="
glxinfo | head -5 || echo "Warning: GLX info unavailable"

echo "=== Running EditMode tests ==="
unity-editor \
    -batchmode \
    -projectPath /project \
    -runTests \
    -testPlatform EditMode \
    -testResults /test-results/editmode-results.xml \
    -logFile /test-results/editmode.log \
    || true

echo "=== Running PlayMode tests ==="
unity-editor \
    -batchmode \
    -projectPath /project \
    -runTests \
    -testPlatform PlayMode \
    -testResults /test-results/playmode-results.xml \
    -logFile /test-results/playmode.log \
    || true

echo "=== Running Graphics System Tests ==="
unity-editor \
    -batchmode \
    -projectPath /project \
    -runTests \
    -testPlatform PlayMode \
    -testFilter "ElementalSiege.Tests.SystemTests" \
    -testResults /test-results/system-results.xml \
    -logFile /test-results/system.log \
    || true

echo "=== Collecting screenshots ==="
cp -r /project/Assets/_Project/Tests/Screenshots/* /screenshots/ 2>/dev/null || echo "No screenshots generated"

echo "=== Test Results ==="
ls -la /test-results/
echo "Done!"
