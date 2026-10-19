-- Run as a PostgreSQL superuser through database/setup-database.ps1.
-- Creates the application role and its two databases. The role is not a superuser
-- and cannot create databases or roles; it only owns these two databases.

\set ON_ERROR_STOP on
\getenv app_password PORTAL_APP_PASSWORD

SELECT 'CREATE ROLE portal_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE'
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'portal_app') \gexec

ALTER ROLE portal_app WITH LOGIN PASSWORD :'app_password';

SELECT 'CREATE DATABASE student_portal OWNER portal_app'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'student_portal') \gexec

SELECT 'CREATE DATABASE student_portal_test OWNER portal_app'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'student_portal_test') \gexec

REVOKE ALL ON DATABASE student_portal FROM PUBLIC;
REVOKE ALL ON DATABASE student_portal_test FROM PUBLIC;
