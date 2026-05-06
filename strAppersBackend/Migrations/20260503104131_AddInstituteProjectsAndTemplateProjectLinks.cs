using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituteProjectsAndTemplateProjectLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApiVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MaxTokens = table.Column<int>(type: "integer", nullable: true),
                    DefaultTemperature = table.Column<double>(type: "double precision", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerChatHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SprintId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    AIModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerChatHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EarlyBirds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OrgName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FutureRole = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarlyBirds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FigmaOAuthPending",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FigmaAccessToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FigmaRefreshToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FigmaTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FigmaOAuthPending", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Institutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TermsUse = table.Column<string>(type: "text", nullable: true),
                    TermsAccepted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TermsAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Majors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Department = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Majors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Base64 = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Metrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TermsUse = table.Column<string>(type: "text", nullable: true),
                    TermsAccepted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TermsAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgrammingLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReleaseYear = table.Column<int>(type: "integer", nullable: true),
                    Creator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgrammingLanguages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCriterias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCriterias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptCategories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptCategories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "RoleTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StakeholderCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakeholderCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StakeholderStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakeholderStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Support",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 3)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Support", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Years",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Years", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstituteSquads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteSquads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteSquads_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teachers_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mission = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OneLiner = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExtendedDescription = table.Column<string>(type: "TEXT", nullable: true),
                    SystemDesign = table.Column<string>(type: "TEXT", nullable: true),
                    DataSchema = table.Column<string>(type: "TEXT", nullable: true),
                    Logo = table.Column<string>(type: "TEXT", nullable: true),
                    SystemDesignDoc = table.Column<byte[]>(type: "bytea", nullable: true),
                    SystemDesignFormatted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    InstituteId = table.Column<int>(type: "integer", nullable: true),
                    isAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InUse = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Kickoff = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrelloBoardJson = table.Column<string>(type: "TEXT", nullable: true),
                    CustomerPastStory = table.Column<string>(type: "TEXT", nullable: true),
                    ShortBrief = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    deployment_manifest = table.Column<string>(type: "TEXT", nullable: true),
                    ide_generation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_chunks = table.Column<int>(type: "integer", nullable: false),
                    completed_chunks = table.Column<int>(type: "integer", nullable: false),
                    mock_records_count = table.Column<int>(type: "integer", nullable: false),
                    CriteriaIds = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Projects_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    CustomerEngagement = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_RoleTypes_Type",
                        column: x => x.Type,
                        principalTable: "RoleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Roles_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Employers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubscriptionTypeId = table.Column<int>(type: "integer", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employers_Subscriptions_SubscriptionTypeId",
                        column: x => x.SubscriptionTypeId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DesignVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    DesignDocument = table.Column<string>(type: "TEXT", nullable: false),
                    DesignDocumentPdf = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignVersions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstituteAssistantChatHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteId = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsAssistant = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteAssistantChatHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteAssistantChatHistory_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstituteAssistantChatHistory_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstituteAssistantChatHistory_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstituteProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteId = table.Column<int>(type: "integer", nullable: false),
                    BaseProjectId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mission = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OneLiner = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExtendedDescription = table.Column<string>(type: "TEXT", nullable: true),
                    SystemDesign = table.Column<string>(type: "TEXT", nullable: true),
                    DataSchema = table.Column<string>(type: "TEXT", nullable: true),
                    Logo = table.Column<string>(type: "TEXT", nullable: true),
                    SystemDesignDoc = table.Column<byte[]>(type: "bytea", nullable: true),
                    SystemDesignFormatted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    isAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InUse = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Kickoff = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrelloBoardJson = table.Column<string>(type: "TEXT", nullable: true),
                    CustomerPastStory = table.Column<string>(type: "TEXT", nullable: true),
                    ShortBrief = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    deployment_manifest = table.Column<string>(type: "TEXT", nullable: true),
                    ide_generation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_chunks = table.Column<int>(type: "integer", nullable: false),
                    completed_chunks = table.Column<int>(type: "integer", nullable: false),
                    mock_records_count = table.Column<int>(type: "integer", nullable: false),
                    CriteriaIds = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteProjects_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstituteProjects_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InstituteProjects_Projects_BaseProjectId",
                        column: x => x.BaseProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    InstanceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInstances", x => x.Id);
                    table.UniqueConstraint("AK_ProjectInstances_InstanceId", x => x.InstanceId);
                    table.ForeignKey(
                        name: "FK_ProjectInstances_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectsIDE",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    chunk_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    chunk_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chunk_description = table.Column<string>(type: "text", nullable: true),
                    generation_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    files_json = table.Column<string>(type: "jsonb", nullable: true),
                    files_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dependencies = table.Column<string[]>(type: "text[]", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    tokens_used = table.Column<int>(type: "integer", nullable: true),
                    generation_time_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectsIDE", x => x.id);
                    table.ForeignKey(
                        name: "FK_ProjectsIDE_Projects_project_id",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorPrompt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    PromptString = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPrompt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorPrompt_PromptCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PromptCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPrompt_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EmployerAdds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployerId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    JobDescription = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerAdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployerAdds_Employers_EmployerId",
                        column: x => x.EmployerId,
                        principalTable: "Employers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployerAdds_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstituteTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstituteId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    InstituteProjectId = table.Column<int>(type: "integer", nullable: true),
                    SquadId = table.Column<int>(type: "integer", nullable: true),
                    TrelloBoardJson = table.Column<string>(type: "text", nullable: false),
                    BoardURL = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteTemplates_InstituteProjects_InstituteProjectId",
                        column: x => x.InstituteProjectId,
                        principalTable: "InstituteProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstituteTemplates_InstituteSquads_SquadId",
                        column: x => x.SquadId,
                        principalTable: "InstituteSquads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InstituteTemplates_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstituteTemplates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    InstituteProjectId = table.Column<int>(type: "integer", nullable: true),
                    ModuleType = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: true),
                    OriginalModuleId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModules_InstituteProjects_InstituteProjectId",
                        column: x => x.InstituteProjectId,
                        principalTable: "InstituteProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectModules_ModuleTypes_ModuleType",
                        column: x => x.ModuleType,
                        principalTable: "ModuleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectModules_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstituteRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteId = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Competencies = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    CustomerEngagement = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsTechnical = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteRoles_InstituteTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "InstituteTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InstituteRoles_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstituteRoles_RoleTypes_Type",
                        column: x => x.Type,
                        principalTable: "RoleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstituteRoles_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InstituteSquadRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SquadId = table.Column<int>(type: "integer", nullable: false),
                    BaseInstituteRoleId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Competencies = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    CustomerEngagement = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsTechnical = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteSquadRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteSquadRoles_InstituteRoles_BaseInstituteRoleId",
                        column: x => x.BaseInstituteRoleId,
                        principalTable: "InstituteRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InstituteSquadRoles_InstituteSquads_SquadId",
                        column: x => x.SquadId,
                        principalTable: "InstituteSquads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstituteSquadRoles_RoleTypes_Type",
                        column: x => x.Type,
                        principalTable: "RoleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstituteSquadRoles_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BoardMeetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MeetingTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StudentEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CustomMeetingUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ActualMeetingUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Attended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    JoinTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardMeetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BoardStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Webhook = table.Column<bool>(type: "boolean", nullable: true),
                    ServiceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    File = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Line = table.Column<int>(type: "integer", nullable: true),
                    StackTrace = table.Column<string>(type: "text", nullable: true),
                    RequestUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastBuildStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastBuildOutput = table.Column<string>(type: "text", nullable: true),
                    LatestErrorSummary = table.Column<string>(type: "text", nullable: true),
                    SprintNumber = table.Column<int>(type: "integer", nullable: true),
                    BranchName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BranchUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GithubBranch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LatestCommitId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LatestCommitDescription = table.Column<string>(type: "text", nullable: true),
                    LatestCommitDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMergeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestEvent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PRStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BranchStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MentorFeedback = table.Column<string>(type: "text", nullable: true),
                    DevRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CacheMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SprintNumber = table.Column<int>(type: "integer", nullable: false),
                    MetricId = table.Column<int>(type: "integer", nullable: false),
                    ReviewContent = table.Column<string>(type: "text", nullable: false),
                    Graph = table.Column<string>(type: "text", nullable: true),
                    Graph2 = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CacheMetrics_Metrics_MetricId",
                        column: x => x.MetricId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CacheReview",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SprintNumber = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewContent = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheReview", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployerBoards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployerId = table.Column<int>(type: "integer", nullable: false),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Observed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MeetRequest = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployerBoards_Employers_EmployerId",
                        column: x => x.EmployerId,
                        principalTable: "Employers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployerCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployerId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployerCandidates_Employers_EmployerId",
                        column: x => x.EmployerId,
                        principalTable: "Employers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Figma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FigmaAccessToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FigmaRefreshToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FigmaTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FigmaUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FigmaFileUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FigmaFileKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FigmaLastSync = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Figma", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    StudentEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StudentFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StudentLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    JoinDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Added = table.Column<bool>(type: "boolean", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorChatHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SprintId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    AIModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorChatHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrivateChats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email2 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChatHistory = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateChats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectBoards",
                columns: table => new
                {
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    StatusId = table.Column<int>(type: "integer", nullable: true),
                    AdminId = table.Column<int>(type: "integer", nullable: true),
                    SprintPlan = table.Column<string>(type: "jsonb", nullable: true),
                    BoardURL = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublishUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MovieUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NextMeetingTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextMeetingUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NextMeetingTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NextMeetingTeacherAttendance = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    GithubBackendUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GithubFrontendUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WebApiUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FacebookUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PresentationUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InstagramUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    YoutubeUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CollectionJourneyUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DatabaseSchemaUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Document1Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Document2Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Document3Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Document4Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Document1Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Document2Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Document3Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Document4Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GroupChat = table.Column<string>(type: "text", nullable: true),
                    Observed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DBPassword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NeonProjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NeonBranchId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SystemBoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsSystemBoard = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SquadName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBoards", x => x.BoardId);
                    table.ForeignKey(
                        name: "FK_ProjectBoards_ProjectBoards_SystemBoardId",
                        column: x => x.SystemBoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectBoards_ProjectStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "ProjectStatuses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectBoards_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectBoardSprintMerge",
                columns: table => new
                {
                    ProjectBoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SprintNumber = table.Column<int>(type: "integer", nullable: false),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ListId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBoardSprintMerge", x => new { x.ProjectBoardId, x.SprintNumber });
                    table.ForeignKey(
                        name: "FK_ProjectBoardSprintMerge_ProjectBoards_ProjectBoardId",
                        column: x => x.ProjectBoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stakeholders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    V1AlignmentScore = table.Column<int>(type: "integer", nullable: false),
                    Delta = table.Column<string>(type: "TEXT", nullable: true),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stakeholders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stakeholders_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Stakeholders_StakeholderCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "StakeholderCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stakeholders_StakeholderStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "StakeholderStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MajorId = table.Column<int>(type: "integer", nullable: false),
                    YearId = table.Column<int>(type: "integer", nullable: false),
                    LinkedInUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GithubUser = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    StartPendingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProjectPriority1 = table.Column<int>(type: "integer", nullable: true),
                    ProjectPriority2 = table.Column<int>(type: "integer", nullable: true),
                    ProjectPriority3 = table.Column<int>(type: "integer", nullable: true),
                    ProjectPriority4 = table.Column<int>(type: "integer", nullable: true),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InstituteId = table.Column<int>(type: "integer", nullable: true),
                    InstanceId = table.Column<int>(type: "integer", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    EmployerExposure = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SuperUser = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    b2c = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AssistMe = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NextMeetingTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextMeetingUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Photo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProgrammingLanguageId = table.Column<int>(type: "integer", nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CV = table.Column<string>(type: "text", nullable: true),
                    MinutesToWork = table.Column<int>(type: "integer", nullable: true),
                    HybridWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    HomeWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FullTimeWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PartTimeWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FreelanceWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TravelWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NightShiftWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RelocationWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StudentWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MultilingualWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SubscriptionTypeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Majors_MajorId",
                        column: x => x.MajorId,
                        principalTable: "Majors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_ProgrammingLanguages_ProgrammingLanguageId",
                        column: x => x.ProgrammingLanguageId,
                        principalTable: "ProgrammingLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_ProjectInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "ProjectInstances",
                        principalColumn: "InstanceId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Projects_ProjectPriority1",
                        column: x => x.ProjectPriority1,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Projects_ProjectPriority2",
                        column: x => x.ProjectPriority2,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Projects_ProjectPriority3",
                        column: x => x.ProjectPriority3,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Projects_ProjectPriority4",
                        column: x => x.ProjectPriority4,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Subscriptions_SubscriptionTypeId",
                        column: x => x.SubscriptionTypeId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Years_YearId",
                        column: x => x.YearId,
                        principalTable: "Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsFigma = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SprintNumber = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Resources_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentRoles_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AIModels",
                columns: new[] { "Id", "ApiVersion", "BaseUrl", "CreatedAt", "DefaultTemperature", "Description", "IsActive", "MaxTokens", "Name", "Provider", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, null, "https://api.openai.com/v1", new DateTime(2026, 5, 3, 10, 41, 29, 953, DateTimeKind.Utc).AddTicks(24), 0.20000000000000001, "OpenAI GPT-4o Mini model - fast and cost-effective", true, 16384, "gpt-4o-mini", "OpenAI", null },
                    { 2, "2023-06-01", "https://api.anthropic.com/v1", new DateTime(2026, 5, 3, 10, 41, 29, 953, DateTimeKind.Utc).AddTicks(31), 0.29999999999999999, "Anthropic Claude Sonnet 4.5 model - powerful for complex tasks", true, 200000, "claude-sonnet-4-5-20250929", "Anthropic", null }
                });

            migrationBuilder.InsertData(
                table: "Institutes",
                columns: new[] { "Id", "Address", "ContactEmail", "Country", "CreatedAt", "Description", "IsActive", "Logo", "Name", "PasswordHash", "Phone", "State", "TermsAcceptedAt", "TermsUse", "Type", "UpdatedAt", "Website" },
                values: new object[] { 1, "42 Innovation Way, Suite 100", "contact@strappers-academy.example.org", "Israel", new DateTime(2026, 4, 3, 10, 41, 29, 906, DateTimeKind.Utc).AddTicks(6378), "Technology-focused institute and applied learning campus.", true, null, "StrAppers Academy of Technology", null, "555-2100", "Tel Aviv District", null, null, "Institute", null, "https://academy.strappers.example.org" });

            migrationBuilder.InsertData(
                table: "Majors",
                columns: new[] { "Id", "CreatedAt", "Department", "Description", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 4, 10, 41, 29, 906, DateTimeKind.Utc).AddTicks(1887), "Computer Science", "Study of computational systems and design", true, "Computer Science", null },
                    { 2, new DateTime(2026, 3, 4, 10, 41, 29, 906, DateTimeKind.Utc).AddTicks(1901), "Computer Science", "Engineering approach to software development", true, "Software Engineering", null },
                    { 3, new DateTime(2026, 3, 4, 10, 41, 29, 906, DateTimeKind.Utc).AddTicks(1904), "Computer Science", "Extracting insights from data", true, "Data Science", null },
                    { 4, new DateTime(2026, 3, 4, 10, 41, 29, 906, DateTimeKind.Utc).AddTicks(1906), "Computer Science", "Protecting digital systems and data", true, "Cybersecurity", null },
                    { 5, new DateTime(2026, 3, 4, 10, 41, 29, 906, DateTimeKind.Utc).AddTicks(1908), "Information Systems", "Management and use of technology", true, "Information Technology", null },
                    { 6, new DateTime(2026, 3, 4, 10, 41, 29, 906, DateTimeKind.Utc).AddTicks(1911), "Business", "General business management", true, "Business Administration", null }
                });

            migrationBuilder.InsertData(
                table: "Metrics",
                columns: new[] { "Id", "Endpoint", "Name" },
                values: new object[,]
                {
                    { 1, null, "Adherence" },
                    { 2, null, "GapAnalysis" },
                    { 3, null, "Improvement" },
                    { 4, null, "Communication" },
                    { 5, null, "Attendance" },
                    { 6, null, "Strengths&weaknesses" },
                    { 7, null, "CustomerEngagement" }
                });

            migrationBuilder.InsertData(
                table: "ModuleTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Frontend" },
                    { 2, "Backend" },
                    { 3, "Database" },
                    { 4, "Authentication" },
                    { 5, "API" },
                    { 6, "Mobile" },
                    { 7, "DevOps" },
                    { 8, "Testing" }
                });

            migrationBuilder.InsertData(
                table: "Organizations",
                columns: new[] { "Id", "Address", "ContactEmail", "CreatedAt", "Description", "IsActive", "Logo", "Name", "PasswordHash", "Phone", "TermsAcceptedAt", "TermsUse", "Type", "UpdatedAt", "Website" },
                values: new object[,]
                {
                    { 1, "123 Tech Street, Tech City", "info@techuniversity.edu", new DateTime(2026, 3, 4, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(8242), "Leading technology university", true, null, "Tech University", null, "555-0101", null, null, "University", null, "https://techuniversity.edu" },
                    { 2, "456 Innovation Ave, Tech City", "contact@innovationlabs.com", new DateTime(2026, 3, 9, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(8249), "Research and development company", true, null, "Innovation Labs", null, "555-0102", null, null, "Company", null, "https://innovationlabs.com" },
                    { 3, "789 Good Street, Tech City", "hello@codeforgood.org", new DateTime(2026, 3, 14, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(8259), "Non-profit organization promoting tech for social good", true, null, "Code for Good", null, "555-0103", null, null, "Non-profit", null, "https://codeforgood.org" }
                });

            migrationBuilder.InsertData(
                table: "ProjectCriterias",
                columns: new[] { "Id", "Active", "Name" },
                values: new object[,]
                {
                    { 1, true, "Popular Projects" },
                    { 2, true, "UI/UX Designer Needed" },
                    { 3, true, "Backend Developer Needed" },
                    { 4, true, "Frontend Developer Needed" },
                    { 5, true, "Product manager Needed" },
                    { 6, true, "Marketing Needed" },
                    { 7, true, "New Projects" }
                });

            migrationBuilder.InsertData(
                table: "ProjectStatuses",
                columns: new[] { "Id", "Color", "CreatedAt", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "#10B981", new DateTime(2026, 3, 24, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(9547), "Newly created project", true, "New", 1, null },
                    { 2, "#3B82F6", new DateTime(2026, 3, 24, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(9554), "Project in planning phase", true, "Planning", 2, null },
                    { 3, "#F59E0B", new DateTime(2026, 3, 24, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(9556), "Project currently being worked on", true, "In Progress", 3, null },
                    { 4, "#EF4444", new DateTime(2026, 3, 24, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(9559), "Project temporarily paused", true, "On Hold", 4, null },
                    { 5, "#059669", new DateTime(2026, 3, 24, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(9562), "Project successfully completed", true, "Completed", 5, null },
                    { 6, "#6B7280", new DateTime(2026, 3, 24, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(9565), "Project cancelled or abandoned", true, "Cancelled", 6, null }
                });

            migrationBuilder.InsertData(
                table: "RoleTypes",
                columns: new[] { "Id", "Description" },
                values: new object[,]
                {
                    { 0, "Default" },
                    { 1, "bundle" },
                    { 2, "bundle" },
                    { 3, "Required" },
                    { 4, "leadership" }
                });

            migrationBuilder.InsertData(
                table: "Subscriptions",
                columns: new[] { "Id", "CreatedAt", "Description", "Price", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 5, 3, 10, 41, 29, 947, DateTimeKind.Utc).AddTicks(8470), "Junior", 0m, null },
                    { 2, new DateTime(2026, 5, 3, 10, 41, 29, 947, DateTimeKind.Utc).AddTicks(8474), "Product", 0m, null },
                    { 3, new DateTime(2026, 5, 3, 10, 41, 29, 947, DateTimeKind.Utc).AddTicks(8476), "Enterprise A", 0m, null },
                    { 4, new DateTime(2026, 5, 3, 10, 41, 29, 947, DateTimeKind.Utc).AddTicks(8478), "Enterprise B", 0m, null }
                });

            migrationBuilder.InsertData(
                table: "Years",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 4, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(5443), "First year of study", true, "Freshman", 1, null },
                    { 2, new DateTime(2026, 3, 4, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(5451), "Second year of study", true, "Sophomore", 2, null },
                    { 3, new DateTime(2026, 3, 4, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(5454), "Third year of study", true, "Junior", 3, null },
                    { 4, new DateTime(2026, 3, 4, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(5456), "Fourth year of study", true, "Senior", 4, null },
                    { 5, new DateTime(2026, 3, 4, 10, 41, 29, 909, DateTimeKind.Utc).AddTicks(5458), "Graduate level study", true, "Graduate", 5, null }
                });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "completed_chunks", "CreatedAt", "CriteriaIds", "CustomerPastStory", "DataSchema", "deployment_manifest", "Description", "ExtendedDescription", "ide_generation_status", "InUse", "InstituteId", "isAvailable", "Kickoff", "Logo", "Mission", "mock_records_count", "OneLiner", "OrganizationId", "Priority", "ShortBrief", "SystemDesign", "SystemDesignDoc", "SystemDesignFormatted", "Title", "total_chunks", "TrelloBoardJson", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 0, new DateTime(2026, 4, 3, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9645), null, null, null, null, "Web application for managing student records and academic progress", null, "not_started", true, null, true, false, null, null, 10, null, 1, "High", null, null, null, null, "Student Management System", 0, null, null },
                    { 2, 0, new DateTime(2026, 4, 13, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9656), null, null, null, null, "Machine learning platform for academic research", null, "not_started", true, null, true, false, null, null, 10, null, 2, "Medium", null, null, null, null, "AI Research Platform", 0, null, null },
                    { 3, 0, new DateTime(2026, 2, 2, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9660), null, null, null, null, "Mobile app connecting volunteers with local community needs", null, "not_started", true, null, true, false, null, null, 10, null, 3, "High", null, null, null, null, "Community Outreach App", 0, null, null },
                    { 4, 0, new DateTime(2026, 4, 18, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9664), null, null, null, null, "E-learning platform with video streaming and assessments", null, "not_started", true, null, true, false, null, null, 10, null, 1, "Critical", null, null, null, null, "Online Learning Platform", 0, null, null },
                    { 5, 0, new DateTime(2026, 4, 23, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9692), null, null, null, null, "Real-time dashboard for analyzing student performance metrics", null, "not_started", true, null, true, false, null, null, 10, null, 1, "Medium", null, null, null, null, "Data Analytics Dashboard", 0, null, null },
                    { 6, 0, new DateTime(2026, 4, 28, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9696), null, null, null, null, "Mobile application for students to discover and register for campus events", null, "not_started", true, null, true, false, null, null, 10, null, 1, "Medium", null, null, null, null, "Mobile App for Campus Events", 0, null, null },
                    { 7, 0, new DateTime(2026, 4, 30, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9699), null, null, null, null, "VR environment for immersive learning experiences", null, "not_started", true, null, true, false, null, null, 10, null, 2, "High", null, null, null, null, "Virtual Reality Learning Lab", 0, null, null },
                    { 8, 0, new DateTime(2026, 5, 2, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9703), null, null, null, null, "Secure voting system using blockchain technology", null, "not_started", true, null, true, false, null, null, 10, null, 1, "High", null, null, null, null, "Blockchain Voting System", 0, null, null },
                    { 9, 0, new DateTime(2026, 5, 1, 10, 41, 29, 925, DateTimeKind.Utc).AddTicks(9706), null, null, null, null, "Internet of Things system for campus management", null, "not_started", true, null, true, false, null, null, 10, null, 2, "Medium", null, null, null, null, "IoT Smart Campus", 0, null, null }
                });

            migrationBuilder.InsertData(
                table: "Students",
                columns: new[] { "Id", "b2c", "BoardId", "CV", "CreatedAt", "Email", "EmployerExposure", "FirstName", "GithubUser", "InstanceId", "InstituteId", "IsAdmin", "IsAvailable", "LastName", "LinkedInUrl", "MajorId", "MinutesToWork", "NextMeetingTime", "NextMeetingUrl", "PasswordHash", "Photo", "ProgrammingLanguageId", "ProjectId", "ProjectPriority1", "ProjectPriority2", "ProjectPriority3", "ProjectPriority4", "StartPendingAt", "Status", "StudentId", "SubscriptionTypeId", "UpdatedAt", "YearId" },
                values: new object[,]
                {
                    { 1, true, null, null, new DateTime(2026, 3, 19, 10, 41, 29, 924, DateTimeKind.Utc).AddTicks(6755), "alex.johnson@techuniversity.edu", true, "Alex", "", null, null, true, true, "Johnson", "https://linkedin.com/in/alexjohnson", 1, null, null, null, null, null, null, null, null, null, null, null, null, null, "TU001", null, null, 3 },
                    { 2, true, null, null, new DateTime(2026, 3, 24, 10, 41, 29, 924, DateTimeKind.Utc).AddTicks(6766), "sarah.williams@techuniversity.edu", true, "Sarah", "", null, null, false, true, "Williams", "https://linkedin.com/in/sarahwilliams", 2, null, null, null, null, null, null, null, null, null, null, null, null, null, "TU002", null, null, 4 },
                    { 3, true, null, null, new DateTime(2026, 3, 29, 10, 41, 29, 924, DateTimeKind.Utc).AddTicks(6770), "michael.brown@techuniversity.edu", true, "Michael", "", null, null, true, true, "Brown", "https://linkedin.com/in/michaelbrown", 3, null, null, null, null, null, null, null, null, null, null, null, null, null, "TU003", null, null, 5 },
                    { 4, true, null, null, new DateTime(2026, 4, 3, 10, 41, 29, 924, DateTimeKind.Utc).AddTicks(6774), "emily.davis@techuniversity.edu", true, "Emily", "", null, null, false, true, "Davis", "https://linkedin.com/in/emilydavis", 4, null, null, null, null, null, null, null, null, null, null, null, null, null, "TU004", null, null, 2 },
                    { 5, true, null, null, new DateTime(2026, 4, 8, 10, 41, 29, 924, DateTimeKind.Utc).AddTicks(6778), "david.miller@techuniversity.edu", true, "David", "", null, null, false, true, "Miller", "https://linkedin.com/in/davidmiller", 1, null, null, null, null, null, null, null, null, null, null, null, null, null, "TU005", null, null, 1 }
                });

            migrationBuilder.InsertData(
                table: "StudentRoles",
                columns: new[] { "Id", "AssignedDate", "IsActive", "Notes", "RoleId", "StudentId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 4, 3, 10, 41, 29, 935, DateTimeKind.Utc).AddTicks(5889), true, "Leading the Student Management System project", 1, 1 },
                    { 2, new DateTime(2026, 4, 8, 10, 41, 29, 935, DateTimeKind.Utc).AddTicks(5898), true, "Frontend development for multiple projects", 2, 2 },
                    { 3, new DateTime(2026, 4, 13, 10, 41, 29, 935, DateTimeKind.Utc).AddTicks(5900), true, "Backend development and database design", 3, 3 },
                    { 4, new DateTime(2026, 4, 18, 10, 41, 29, 935, DateTimeKind.Utc).AddTicks(5903), true, "UI/UX design for community outreach app", 4, 4 },
                    { 5, new DateTime(2026, 4, 23, 10, 41, 29, 935, DateTimeKind.Utc).AddTicks(5906), true, "QA testing for online learning platform", 5, 5 },
                    { 6, new DateTime(2026, 3, 29, 10, 41, 29, 935, DateTimeKind.Utc).AddTicks(5908), true, "Team lead for junior developers", 6, 1 },
                    { 7, new DateTime(2026, 4, 15, 10, 41, 29, 935, DateTimeKind.Utc).AddTicks(5910), true, "Research on AI and machine learning", 7, 3 },
                    { 8, new DateTime(2026, 4, 21, 10, 41, 29, 935, DateTimeKind.Utc).AddTicks(5912), true, "Documentation for frontend components", 8, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_Attended",
                table: "BoardMeetings",
                column: "Attended");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_BoardId",
                table: "BoardMeetings",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_MeetingTime",
                table: "BoardMeetings",
                column: "MeetingTime");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_StudentEmail",
                table: "BoardMeetings",
                column: "StudentEmail");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BoardId",
                table: "BoardStates",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BoardId_Source_Webhook_GithubBranch",
                table: "BoardStates",
                columns: new[] { "BoardId", "Source", "Webhook", "GithubBranch" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BranchName",
                table: "BoardStates",
                column: "BranchName");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BranchStatus",
                table: "BoardStates",
                column: "BranchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_CreatedAt",
                table: "BoardStates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_LastBuildStatus",
                table: "BoardStates",
                column: "LastBuildStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_LatestCommitId",
                table: "BoardStates",
                column: "LatestCommitId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_PRStatus",
                table: "BoardStates",
                column: "PRStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_Source",
                table: "BoardStates",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_UpdatedAt",
                table: "BoardStates",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CacheMetrics_BoardId",
                table: "CacheMetrics",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_CacheMetrics_MetricId",
                table: "CacheMetrics",
                column: "MetricId");

            migrationBuilder.CreateIndex(
                name: "IX_CacheMetrics_StudentId",
                table: "CacheMetrics",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_CacheReview_BoardId",
                table: "CacheReview",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_CacheReview_BoardId_StudentId_SprintNumber_SequenceNumber",
                table: "CacheReview",
                columns: new[] { "BoardId", "StudentId", "SprintNumber", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CacheReview_StudentId",
                table: "CacheReview",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerChatHistory_SprintId",
                table: "CustomerChatHistory",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerChatHistory_StudentId",
                table: "CustomerChatHistory",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerChatHistory_StudentId_SprintId",
                table: "CustomerChatHistory",
                columns: new[] { "StudentId", "SprintId" });

            migrationBuilder.CreateIndex(
                name: "IX_DesignVersions_IsActive",
                table: "DesignVersions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DesignVersions_ProjectId",
                table: "DesignVersions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignVersions_VersionNumber",
                table: "DesignVersions",
                column: "VersionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerAdds_EmployerId",
                table: "EmployerAdds",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerAdds_RoleId",
                table: "EmployerAdds",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerBoards_BoardId",
                table: "EmployerBoards",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerBoards_EmployerId",
                table: "EmployerBoards",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerBoards_EmployerId_BoardId",
                table: "EmployerBoards",
                columns: new[] { "EmployerId", "BoardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployerCandidates_CreatedAt",
                table: "EmployerCandidates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerCandidates_EmployerId",
                table: "EmployerCandidates",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerCandidates_EmployerId_StudentId",
                table: "EmployerCandidates",
                columns: new[] { "EmployerId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployerCandidates_StudentId",
                table: "EmployerCandidates",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employers_ContactEmail",
                table: "Employers",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Employers_SubscriptionTypeId",
                table: "Employers",
                column: "SubscriptionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Figma_BoardId",
                table: "Figma",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Figma_FigmaFileKey",
                table: "Figma",
                column: "FigmaFileKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FigmaOAuthPending_Email",
                table: "FigmaOAuthPending",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IACH_InstituteId_TeacherId_ProjectId_Source_CreatedAt",
                table: "InstituteAssistantChatHistory",
                columns: new[] { "InstituteId", "TeacherId", "ProjectId", "Source", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteAssistantChatHistory_ProjectId",
                table: "InstituteAssistantChatHistory",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteAssistantChatHistory_TeacherId",
                table: "InstituteAssistantChatHistory",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteProjects_BaseProjectId",
                table: "InstituteProjects",
                column: "BaseProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteProjects_InstituteId",
                table: "InstituteProjects",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteProjects_OrganizationId",
                table: "InstituteProjects",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteRoles_InstituteId",
                table: "InstituteRoles",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteRoles_SkillId",
                table: "InstituteRoles",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteRoles_TemplateId",
                table: "InstituteRoles",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteRoles_Type",
                table: "InstituteRoles",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_BaseInstituteRoleId",
                table: "InstituteSquadRoles",
                column: "BaseInstituteRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_SkillId",
                table: "InstituteSquadRoles",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_SquadId",
                table: "InstituteSquadRoles",
                column: "SquadId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_SquadId_Name",
                table: "InstituteSquadRoles",
                columns: new[] { "SquadId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_Type",
                table: "InstituteSquadRoles",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquads_InstituteId",
                table: "InstituteSquads",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquads_InstituteId_Name",
                table: "InstituteSquads",
                columns: new[] { "InstituteId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteTemplates_InstituteId",
                table: "InstituteTemplates",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteTemplates_InstituteId_ProjectId",
                table: "InstituteTemplates",
                columns: new[] { "InstituteId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteTemplates_InstituteProjectId",
                table: "InstituteTemplates",
                column: "InstituteProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteTemplates_ProjectId",
                table: "InstituteTemplates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteTemplates_SquadId",
                table: "InstituteTemplates",
                column: "SquadId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_Added",
                table: "JoinRequests",
                column: "Added");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ChannelId",
                table: "JoinRequests",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_JoinDate",
                table: "JoinRequests",
                column: "JoinDate");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ProjectId",
                table: "JoinRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_StudentEmail",
                table: "JoinRequests",
                column: "StudentEmail");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_StudentId",
                table: "JoinRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorChatHistory_StudentId",
                table: "MentorChatHistory",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPrompt_CategoryId",
                table: "MentorPrompt",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPrompt_RoleId",
                table: "MentorPrompt",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChats_BoardId",
                table: "PrivateChats",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChats_BoardId_Email1_Email2",
                table: "PrivateChats",
                columns: new[] { "BoardId", "Email1", "Email2" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammingLanguages_IsActive",
                table: "ProgrammingLanguages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammingLanguages_Name",
                table: "ProgrammingLanguages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_AdminId",
                table: "ProjectBoards",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_CreatedAt",
                table: "ProjectBoards",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_ProjectId",
                table: "ProjectBoards",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_SquadName",
                table: "ProjectBoards",
                column: "SquadName");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_StatusId",
                table: "ProjectBoards",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_SystemBoardId",
                table: "ProjectBoards",
                column: "SystemBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoardSprintMerge_ProjectBoardId",
                table: "ProjectBoardSprintMerge",
                column: "ProjectBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoardSprintMerge_SprintNumber",
                table: "ProjectBoardSprintMerge",
                column: "SprintNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInstances_InstanceId",
                table: "ProjectInstances",
                column: "InstanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInstances_ProjectId",
                table: "ProjectInstances",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_InstituteProjectId",
                table: "ProjectModules",
                column: "InstituteProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_ModuleType",
                table: "ProjectModules",
                column: "ModuleType");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_ProjectId",
                table: "ProjectModules",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_ProjectId_OriginalModuleId",
                table: "ProjectModules",
                columns: new[] { "ProjectId", "OriginalModuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_Sequence",
                table: "ProjectModules",
                column: "Sequence");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_InstituteId",
                table: "Projects",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId",
                table: "Projects",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectsIDE_generation_order",
                table: "ProjectsIDE",
                column: "generation_order");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectsIDE_project_id",
                table: "ProjectsIDE",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectsIDE_project_id_chunk_id",
                table: "ProjectsIDE",
                columns: new[] { "project_id", "chunk_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectsIDE_status",
                table: "ProjectsIDE",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_BoardId",
                table: "Resources",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_StudentId",
                table: "Resources",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SkillId",
                table: "Roles",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Type",
                table: "Roles",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stakeholders_BoardId",
                table: "Stakeholders",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Stakeholders_CategoryId",
                table: "Stakeholders",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Stakeholders_StatusId",
                table: "Stakeholders",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRoles_RoleId",
                table: "StudentRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRoles_StudentId",
                table: "StudentRoles",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_BoardId",
                table: "Students",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_Email",
                table: "Students",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstanceId",
                table: "Students",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstituteId",
                table: "Students",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_MajorId",
                table: "Students",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProgrammingLanguageId",
                table: "Students",
                column: "ProgrammingLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectId",
                table: "Students",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectPriority1",
                table: "Students",
                column: "ProjectPriority1");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectPriority2",
                table: "Students",
                column: "ProjectPriority2");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectPriority3",
                table: "Students",
                column: "ProjectPriority3");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectPriority4",
                table: "Students",
                column: "ProjectPriority4");

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentId",
                table: "Students",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_SubscriptionTypeId",
                table: "Students",
                column: "SubscriptionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_YearId",
                table: "Students",
                column: "YearId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_Email",
                table: "Teachers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_InstituteId",
                table: "Teachers",
                column: "InstituteId");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardMeetings_ProjectBoards_BoardId",
                table: "BoardMeetings",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardStates_ProjectBoards_BoardId",
                table: "BoardStates",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CacheMetrics_ProjectBoards_BoardId",
                table: "CacheMetrics",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CacheMetrics_Students_StudentId",
                table: "CacheMetrics",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CacheReview_ProjectBoards_BoardId",
                table: "CacheReview",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CacheReview_Students_StudentId",
                table: "CacheReview",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployerBoards_ProjectBoards_BoardId",
                table: "EmployerBoards",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployerCandidates_Students_StudentId",
                table: "EmployerCandidates",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Figma_ProjectBoards_BoardId",
                table: "Figma",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JoinRequests_Students_StudentId",
                table: "JoinRequests",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MentorChatHistory_Students_StudentId",
                table: "MentorChatHistory",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PrivateChats_ProjectBoards_BoardId",
                table: "PrivateChats",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoards_Students_AdminId",
                table: "ProjectBoards",
                column: "AdminId",
                principalTable: "Students",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_ProjectBoards_BoardId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "AIModels");

            migrationBuilder.DropTable(
                name: "BoardMeetings");

            migrationBuilder.DropTable(
                name: "BoardStates");

            migrationBuilder.DropTable(
                name: "CacheMetrics");

            migrationBuilder.DropTable(
                name: "CacheReview");

            migrationBuilder.DropTable(
                name: "CustomerChatHistory");

            migrationBuilder.DropTable(
                name: "DesignVersions");

            migrationBuilder.DropTable(
                name: "EarlyBirds");

            migrationBuilder.DropTable(
                name: "EmployerAdds");

            migrationBuilder.DropTable(
                name: "EmployerBoards");

            migrationBuilder.DropTable(
                name: "EmployerCandidates");

            migrationBuilder.DropTable(
                name: "Figma");

            migrationBuilder.DropTable(
                name: "FigmaOAuthPending");

            migrationBuilder.DropTable(
                name: "InstituteAssistantChatHistory");

            migrationBuilder.DropTable(
                name: "InstituteSquadRoles");

            migrationBuilder.DropTable(
                name: "JoinRequests");

            migrationBuilder.DropTable(
                name: "MarketingImages");

            migrationBuilder.DropTable(
                name: "MentorChatHistory");

            migrationBuilder.DropTable(
                name: "MentorPrompt");

            migrationBuilder.DropTable(
                name: "PrivateChats");

            migrationBuilder.DropTable(
                name: "ProjectBoardSprintMerge");

            migrationBuilder.DropTable(
                name: "ProjectCriterias");

            migrationBuilder.DropTable(
                name: "ProjectModules");

            migrationBuilder.DropTable(
                name: "ProjectsIDE");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "Stakeholders");

            migrationBuilder.DropTable(
                name: "StudentRoles");

            migrationBuilder.DropTable(
                name: "Support");

            migrationBuilder.DropTable(
                name: "Metrics");

            migrationBuilder.DropTable(
                name: "Employers");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropTable(
                name: "InstituteRoles");

            migrationBuilder.DropTable(
                name: "PromptCategories");

            migrationBuilder.DropTable(
                name: "ModuleTypes");

            migrationBuilder.DropTable(
                name: "StakeholderCategories");

            migrationBuilder.DropTable(
                name: "StakeholderStatuses");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "InstituteTemplates");

            migrationBuilder.DropTable(
                name: "RoleTypes");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "InstituteProjects");

            migrationBuilder.DropTable(
                name: "InstituteSquads");

            migrationBuilder.DropTable(
                name: "ProjectBoards");

            migrationBuilder.DropTable(
                name: "ProjectStatuses");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Majors");

            migrationBuilder.DropTable(
                name: "ProgrammingLanguages");

            migrationBuilder.DropTable(
                name: "ProjectInstances");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Years");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Institutes");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
