-- Creates the two database roles the app uses. Run once per PostgreSQL server, as a superuser or a role with CREATEROLE:
--   psql -v owner_password='...' -v runtime_password='...' -f deploy/sql/create-roles.sql
-- app_owner   owns the database and runs migrations (deployment pipeline, bootstrap-admin).
-- app_runtime is what the running app connects as: data access only, no DDL, insert-only on audit tables.
\set ON_ERROR_STOP on

\if :{?owner_password}
\else
  \echo 'Pass -v owner_password=... and -v runtime_password=...'
  \quit
\endif
\if :{?runtime_password}
\else
  \echo 'Pass -v runtime_password=...'
  \quit
\endif

SELECT format('CREATE ROLE app_owner LOGIN PASSWORD %L', :'owner_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_owner') \gexec

SELECT format('CREATE ROLE app_runtime LOGIN PASSWORD %L', :'runtime_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_runtime') \gexec
