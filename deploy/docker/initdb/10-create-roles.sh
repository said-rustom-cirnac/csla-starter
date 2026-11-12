#!/usr/bin/env bash
# Runs once when the local postgres container's data volume is first created.
set -euo pipefail

psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres \
  -v owner_password="$APP_OWNER_PASSWORD" -v runtime_password="$APP_RUNTIME_PASSWORD" \
  -f /deploy/sql/create-roles.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres -c "CREATE DATABASE cslastarter OWNER app_owner"
