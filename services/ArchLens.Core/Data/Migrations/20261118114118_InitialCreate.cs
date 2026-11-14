using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArchLens.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_models", x => x.id);
                    table.ForeignKey(
                        name: "fk_models_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    parent_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    commit_sha = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_versions_models_model_id",
                        column: x => x.model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versions_versions_parent_version_id",
                        column: x => x.parent_version_id,
                        principalTable: "versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_models_workspace_id",
                table: "models",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_versions_model_id_number",
                table: "versions",
                columns: new[] { "model_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_versions_parent_version_id",
                table: "versions",
                column: "parent_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_owner_id",
                table: "workspaces",
                column: "owner_id");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS versions_no_truncate ON versions;
                DROP TRIGGER IF EXISTS versions_immutable ON versions;
                DROP FUNCTION IF EXISTS versions_are_immutable();
                """);

            migrationBuilder.DropTable(
                name: "versions");

            migrationBuilder.DropTable(
                name: "models");

            migrationBuilder.DropTable(
                name: "workspaces");
        }
    }
}
