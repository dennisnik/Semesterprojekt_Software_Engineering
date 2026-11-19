using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TNWatch.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.EnsureSchema(
                name: "telemetry");

            migrationBuilder.EnsureSchema(
                name: "alerting");

            migrationBuilder.CreateTable(
                name: "battery_events",
                schema: "core",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    new_battery_serial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    old_battery_serial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    accuracy_m = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_battery_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "traffic_nodes",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    is_simulated = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_voltage_v = table.Column<double>(type: "double precision", nullable: true),
                    smoothed_voltage_v = table.Column<double>(type: "double precision", nullable: true),
                    last_current_a = table.Column<double>(type: "double precision", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    battery_serial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_traffic_nodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "measurements",
                schema: "telemetry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    measured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    voltage_v = table.Column<double>(type: "double precision", nullable: false),
                    current_a = table.Column<double>(type: "double precision", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    altitude_m = table.Column<double>(type: "double precision", nullable: true),
                    temperature_c = table.Column<double>(type: "double precision", nullable: true),
                    satellites = table.Column<int>(type: "integer", nullable: true),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_measurements", x => x.id);
                    table.ForeignKey(
                        name: "fk_measurements_traffic_nodes_node_id",
                        column: x => x.node_id,
                        principalSchema: "core",
                        principalTable: "traffic_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "status_changes",
                schema: "alerting",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    smoothed_voltage_v = table.Column<double>(type: "double precision", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_status_changes_traffic_nodes_node_id",
                        column: x => x.node_id,
                        principalSchema: "core",
                        principalTable: "traffic_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_battery_events_node_id_occurred_at",
                schema: "core",
                table: "battery_events",
                columns: new[] { "node_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_measurements_node_id_measured_at",
                schema: "telemetry",
                table: "measurements",
                columns: new[] { "node_id", "measured_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_status_changes_node_id_occurred_at",
                schema: "alerting",
                table: "status_changes",
                columns: new[] { "node_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_traffic_nodes_status",
                schema: "core",
                table: "traffic_nodes",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "battery_events",
                schema: "core");

            migrationBuilder.DropTable(
                name: "measurements",
                schema: "telemetry");

            migrationBuilder.DropTable(
                name: "status_changes",
                schema: "alerting");

            migrationBuilder.DropTable(
                name: "traffic_nodes",
                schema: "core");
        }
    }
}
