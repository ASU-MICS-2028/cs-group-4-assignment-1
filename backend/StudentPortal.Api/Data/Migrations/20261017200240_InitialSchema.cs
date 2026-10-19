using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StudentPortal.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    user_handle = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                    table.CheckConstraint("ck_accounts_role", "role IN ('student', 'lecturer')");
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<string>(type: "text", nullable: true),
                    @event = table.Column<string>(name: "event", type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<string>(type: "text", nullable: true),
                    detail = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    semester = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webauthn_ceremonies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<string>(type: "text", nullable: true),
                    enrollment_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    options_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webauthn_ceremonies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enrollment_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<string>(type: "character varying(10)", nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollment_codes", x => x.id);
                    table.ForeignKey(
                        name: "fk_enrollment_codes_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "passkey_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<string>(type: "character varying(10)", nullable: false),
                    credential_id = table.Column<byte[]>(type: "bytea", nullable: false),
                    public_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    sign_count = table.Column<long>(type: "bigint", nullable: false),
                    aa_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    transports = table.Column<string[]>(type: "text[]", nullable: false),
                    attestation_format = table.Column<string>(type: "text", nullable: false),
                    is_backup_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    is_backed_up = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_passkey_credentials", x => x.id);
                    table.ForeignKey(
                        name: "fk_passkey_credentials_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    account_id = table.Column<string>(type: "character varying(10)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_profiles",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "character varying(10)", nullable: false),
                    programme = table.Column<string>(type: "text", nullable: false),
                    year_group = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_profiles", x => x.account_id);
                    table.ForeignKey(
                        name: "fk_student_profiles_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_assignments",
                columns: table => new
                {
                    lecturer_id = table.Column<string>(type: "character varying(10)", nullable: false),
                    course_id = table.Column<string>(type: "character varying(10)", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_assignments", x => new { x.lecturer_id, x.course_id });
                    table.ForeignKey(
                        name: "fk_course_assignments_accounts_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_course_assignments_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_enrollments",
                columns: table => new
                {
                    student_id = table.Column<string>(type: "character varying(10)", nullable: false),
                    course_id = table.Column<string>(type: "character varying(10)", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    grade_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    grade_updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_enrollments", x => new { x.student_id, x.course_id });
                    table.CheckConstraint("ck_course_enrollments_score", "score IS NULL OR (score >= 0 AND score <= 100)");
                    table.ForeignKey(
                        name: "fk_course_enrollments_accounts_student_id",
                        column: x => x.student_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_course_enrollments_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "accounts",
                columns: new[] { "id", "created_at", "display_name", "email", "role", "user_handle" },
                values: new object[,]
                {
                    { "L001", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dr. Kofi Mensah", "l001@staff.portal.test", "lecturer", new byte[] { 192, 46, 90, 141, 113, 244, 155, 54, 232, 161, 13, 76, 95, 43, 151, 99 } },
                    { "L002", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dr. Abena Ofori", "l002@staff.portal.test", "lecturer", new byte[] { 88, 217, 225, 75, 42, 108, 7, 243, 185, 229, 216, 42, 28, 79, 96, 183 } },
                    { "S001", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ama Owusu", "s001@students.portal.test", "student", new byte[] { 111, 28, 42, 155, 78, 125, 48, 200, 90, 18, 240, 158, 59, 109, 76, 113 } },
                    { "S002", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Kwame Asante", "s002@students.portal.test", "student", new byte[] { 164, 62, 157, 1, 114, 184, 95, 198, 225, 212, 9, 58, 124, 43, 94, 136 } },
                    { "S003", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Esi Boateng", "s003@students.portal.test", "student", new byte[] { 29, 123, 63, 96, 201, 226, 164, 133, 27, 15, 110, 45, 147, 199, 164, 21 } }
                });

            migrationBuilder.InsertData(
                table: "courses",
                columns: new[] { "id", "semester", "title" },
                values: new object[,]
                {
                    { "C101", "2026/27 Semester 1", "Introduction to Information Security" },
                    { "C102", "2026/27 Semester 1", "Data Structures and Algorithms" }
                });

            migrationBuilder.InsertData(
                table: "course_assignments",
                columns: new[] { "course_id", "lecturer_id", "ends_on", "starts_on" },
                values: new object[,]
                {
                    { "C101", "L001", null, new DateOnly(2026, 8, 24) },
                    { "C102", "L002", null, new DateOnly(2026, 8, 24) }
                });

            migrationBuilder.InsertData(
                table: "course_enrollments",
                columns: new[] { "course_id", "student_id", "grade_updated_at", "grade_updated_by", "score" },
                values: new object[,]
                {
                    { "C101", "S001", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "L001", 78 },
                    { "C102", "S001", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "L002", 71 },
                    { "C101", "S002", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "L001", 64 },
                    { "C102", "S003", new DateTimeOffset(new DateTime(2026, 8, 24, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "L002", 85 }
                });

            migrationBuilder.InsertData(
                table: "student_profiles",
                columns: new[] { "account_id", "programme", "year_group" },
                values: new object[,]
                {
                    { "S001", "BSc Computer Science", 2028 },
                    { "S002", "BSc Management Information Systems", 2028 },
                    { "S003", "BSc Computer Engineering", 2027 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_user_handle",
                table: "accounts",
                column: "user_handle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_occurred_at",
                table: "audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_course_assignments_course_id",
                table: "course_assignments",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_enrollments_course_id",
                table: "course_enrollments",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollment_codes_account_id",
                table: "enrollment_codes",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_passkey_credentials_account_id",
                table: "passkey_credentials",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_passkey_credentials_credential_id",
                table: "passkey_credentials",
                column: "credential_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_account_id",
                table: "sessions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_token_hash",
                table: "sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_webauthn_ceremonies_token_hash",
                table: "webauthn_ceremonies",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "course_assignments");

            migrationBuilder.DropTable(
                name: "course_enrollments");

            migrationBuilder.DropTable(
                name: "enrollment_codes");

            migrationBuilder.DropTable(
                name: "passkey_credentials");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "student_profiles");

            migrationBuilder.DropTable(
                name: "webauthn_ceremonies");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
