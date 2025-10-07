#!/bin/bash

# データベースの初期化スクリプト
set -e

cd "$(dirname "$0")/.."

echo "Creating migrations..."
dotnet ef migrations add InitialCreate --project src/LocalLlmAssistant --context AppDbContext

echo "Applying migrations..."
dotnet ef database update --project src/LocalLlmAssistant --context AppDbContext

echo "Setting up FTS5..."
sqlite3 src/LocalLlmAssistant/App_Data/app.db < scripts/create_fts.sql

echo "Database initialized successfully!"
