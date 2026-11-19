using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNWatch.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NodeAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "node_assignments",
                schema: "core",
                columns: table => new
                {
                    node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    assignee_user_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    assignee_display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    assigned_by_user_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    assigned_by_display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_node_assignments", x => x.node_id);
                    table.ForeignKey(
                        name: "fk_node_assignments_traffic_nodes_node_id",
                        column: x => x.node_id,
                        principalSchema: "core",
                        principalTable: "traffic_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_node_assignments_assignee_user_name",
                schema: "core",
                table: "node_assignments",
                column: "assignee_user_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "node_assignments",
                schema: "core");
        }
    }
}
