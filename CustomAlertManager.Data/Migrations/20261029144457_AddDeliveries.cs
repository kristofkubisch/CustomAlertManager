using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomAlertManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlertId = table.Column<int>(type: "int", nullable: false),
                    NotificationTargetId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertDeliveries_Alerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "Alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertDeliveries_NotificationTargets_NotificationTargetId",
                        column: x => x.NotificationTargetId,
                        principalTable: "NotificationTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionTargets",
                columns: table => new
                {
                    AlertSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    NotificationTargetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionTargets", x => new { x.AlertSubscriptionId, x.NotificationTargetId });
                    table.ForeignKey(
                        name: "FK_SubscriptionTargets_AlertSubscriptions_AlertSubscriptionId",
                        column: x => x.AlertSubscriptionId,
                        principalTable: "AlertSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriptionTargets_NotificationTargets_NotificationTargetId",
                        column: x => x.NotificationTargetId,
                        principalTable: "NotificationTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_AlertId_NotificationTargetId",
                table: "AlertDeliveries",
                columns: new[] { "AlertId", "NotificationTargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_NotificationTargetId",
                table: "AlertDeliveries",
                column: "NotificationTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_Status_NextAttemptAt",
                table: "AlertDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTargets_NotificationTargetId",
                table: "SubscriptionTargets",
                column: "NotificationTargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertDeliveries");

            migrationBuilder.DropTable(
                name: "SubscriptionTargets");

            migrationBuilder.DropTable(
                name: "NotificationTargets");
        }
    }
}
