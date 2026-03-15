#!/bin/bash
# Run tests locally with Docker
echo "Building test container..."
docker-compose -f Docker/docker-compose.test.yml build

echo "Running tests..."
docker-compose -f Docker/docker-compose.test.yml up --abort-on-container-exit

echo "Results in Docker/test-results/"
echo "Screenshots in Docker/screenshots/"
