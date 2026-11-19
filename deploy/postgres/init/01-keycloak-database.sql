-- Keycloak stores realms/users in its own database on the same PostgreSQL instance (ADR-09: one DB server).
-- Runs only on first initialization of the db volume.
CREATE DATABASE keycloak OWNER tnwatch;
