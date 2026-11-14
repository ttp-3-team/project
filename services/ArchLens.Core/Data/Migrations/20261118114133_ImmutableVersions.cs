using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArchLens.Core.Data.Migrations
{
    /// <summary>Versions are immutable: the database itself rejects UPDATE and DELETE on <c>versions</c>.</summary>
    public partial class ImmutableVersions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION versions_are_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'versions are immutable: % is not allowed', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER versions_immutable
                    BEFORE UPDATE OR DELETE ON versions
                    FOR EACH ROW EXECUTE FUNCTION versions_are_immutable();

                CREATE TRIGGER versions_no_truncate
                    BEFORE TRUNCATE ON versions
                    FOR EACH STATEMENT EXECUTE FUNCTION versions_are_immutable();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS versions_no_truncate ON versions;
                DROP TRIGGER IF EXISTS versions_immutable ON versions;
                DROP FUNCTION IF EXISTS versions_are_immutable();
                """);
        }
    }
}
