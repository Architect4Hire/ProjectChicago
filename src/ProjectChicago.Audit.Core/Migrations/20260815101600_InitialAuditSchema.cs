using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectChicago.Audit.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "audit",
                columns: table => new
                {
                    AuditEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newid()"),
                    EventId = table.Column<string>(type: "nvarchar(256)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    ActionCategory = table.Column<string>(type: "nvarchar(32)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorType = table.Column<string>(type: "nvarchar(32)", nullable: false),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(256)", nullable: true),
                    SourceService = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    SourceEventType = table.Column<string>(type: "nvarchar(128)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuditedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", nullable: false),
                    CausationId = table.Column<string>(type: "nvarchar(256)", nullable: true),
                    ChangedFields = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SummaryDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawEventPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.AuditEntryId);
                    table.UniqueConstraint("AK_AuditEntries_EventId", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContractType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContractVersion = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CausationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ProcessingCompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LeaseOwner = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LeasedUntilUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Actor_AuditedAt",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "ActorUserId", "AuditedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CorrelationId",
                schema: "audit",
                table: "AuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityTypeId_OccurredAt",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OccurredAt",
                schema: "audit",
                table: "AuditEntries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Service_Action_AuditedAt",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "SourceService", "Action", "AuditedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TraceId",
                schema: "audit",
                table: "AuditEntries",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_LeasedUntilUtc",
                table: "InboxMessages",
                columns: new[] { "Status", "LeasedUntilUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "InboxMessages");
        }
    }
}
