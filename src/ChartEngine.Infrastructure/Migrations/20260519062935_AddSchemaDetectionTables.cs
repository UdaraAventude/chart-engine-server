using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChartEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemaDetectionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatasetColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cardinality = table.Column<int>(type: "int", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatasetColumns_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DatasetConfigs",
                columns: table => new
                {
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxDimCardinality = table.Column<int>(type: "int", nullable: false),
                    MaxHierarchyDepth = table.Column<int>(type: "int", nullable: false),
                    FallbackTopValues = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetConfigs", x => x.DatasetId);
                    table.ForeignKey(
                        name: "FK_DatasetConfigs_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetColumns_DatasetId",
                table: "DatasetColumns",
                column: "DatasetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatasetColumns");

            migrationBuilder.DropTable(
                name: "DatasetConfigs");
        }
    }
}
