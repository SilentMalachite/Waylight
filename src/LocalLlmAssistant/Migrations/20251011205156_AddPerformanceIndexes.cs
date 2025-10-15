using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalLlmAssistant.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Messages_ConversationId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Messages_CreatedAt\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_DocumentChunks_DocumentId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Conversations_UserId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Conversations_UpdatedAt\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_UserPreferences_UserId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ToolLogs_UserId_CreatedAt\";");

            // Messages インデックス
            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CreatedAt",
                table: "Messages",
                column: "CreatedAt");

            // DocumentChunks インデックス
            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId");

            // Conversations インデックス
            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UserId",
                table: "Conversations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UpdatedAt",
                table: "Conversations",
                column: "UpdatedAt");

            // UserPreferences インデックス (ユニーク)
            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);

            // ToolLogs 複合インデックス
            migrationBuilder.CreateIndex(
                name: "IX_ToolLogs_UserId_CreatedAt",
                table: "ToolLogs",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_CreatedAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_DocumentId",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_UserId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_UpdatedAt",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences");

            migrationBuilder.DropIndex(
                name: "IX_ToolLogs_UserId_CreatedAt",
                table: "ToolLogs");
        }
    }
}
