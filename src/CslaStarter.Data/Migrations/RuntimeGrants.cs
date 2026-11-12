namespace CslaStarter.Data.Migrations;

/// <summary>
/// SQL that grants app_runtime data access (never DDL) to the tables migrations create. Each statement does
/// nothing when the role doesn't exist, so a developer database without roles still migrates.
/// </summary>
internal static class RuntimeGrants
{
    public const string DefaultPrivileges = """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_runtime') THEN
                EXECUTE 'GRANT USAGE ON SCHEMA public TO app_runtime';
                EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_runtime', current_user);
                EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO app_runtime', current_user);
            END IF;
        END $$;
        """;

    public const string ExistingTables = """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_runtime') THEN
                EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_runtime';
                EXECUTE 'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO app_runtime';
                -- The migration history is read-only for the app (the ready check reads it).
                EXECUTE 'REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON "__EFMigrationsHistory" FROM app_runtime';
            END IF;
        END $$;
        """;
}
