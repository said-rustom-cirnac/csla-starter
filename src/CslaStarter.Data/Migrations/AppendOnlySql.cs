namespace CslaStarter.Data.Migrations;

/// <summary>
/// Makes the audit and security event tables append-only: a trigger rejects UPDATE, DELETE and TRUNCATE for
/// every role (including the owner), and app_runtime also loses those privileges.
/// </summary>
internal static class AppendOnlySql
{
    private static readonly string[] Tables = ["audit_event", "audit_change", "security_event"];

    public static string Create { get; } = $$"""
        CREATE OR REPLACE FUNCTION audit_block_mutation() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            RAISE EXCEPTION '% is append-only', TG_TABLE_NAME USING ERRCODE = 'insufficient_privilege';
        END
        $fn$;
        {{string.Concat(Tables.Select(t => $"""
            CREATE TRIGGER {t}_append_only BEFORE UPDATE OR DELETE ON {t} FOR EACH ROW EXECUTE FUNCTION audit_block_mutation();
            CREATE TRIGGER {t}_no_truncate BEFORE TRUNCATE ON {t} FOR EACH STATEMENT EXECUTE FUNCTION audit_block_mutation();

            """))}}
        DO $do$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_runtime') THEN
                EXECUTE 'REVOKE UPDATE, DELETE, TRUNCATE ON audit_event, audit_change, security_event FROM app_runtime';
            END IF;
        END
        $do$;
        """;

    public static string Drop { get; } = string.Concat(Tables.Select(t => $"""
        DROP TRIGGER IF EXISTS {t}_append_only ON {t};
        DROP TRIGGER IF EXISTS {t}_no_truncate ON {t};

        """)) + "DROP FUNCTION IF EXISTS audit_block_mutation();";
}
