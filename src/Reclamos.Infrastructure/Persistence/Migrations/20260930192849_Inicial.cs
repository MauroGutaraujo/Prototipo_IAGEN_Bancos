using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reclamos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.EnsureSchema(
                name: "eval");

            migrationBuilder.CreateTable(
                name: "ClienteSintetico",
                schema: "core",
                columns: table => new
                {
                    IdCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AliasSeudonimo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DispositivoRegistrado = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClienteSintetico", x => x.IdCliente);
                });

            migrationBuilder.CreateTable(
                name: "CorridaBenchmark",
                schema: "app",
                columns: table => new
                {
                    IdCorrida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Condicion = table.Column<string>(type: "char(2)", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorridaBenchmark", x => x.IdCorrida);
                });

            migrationBuilder.CreateTable(
                name: "TipoCambio",
                schema: "core",
                columns: table => new
                {
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    UsdPen = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoCambio", x => x.Fecha);
                });

            migrationBuilder.CreateTable(
                name: "VerdadReferencia",
                schema: "eval",
                columns: table => new
                {
                    IdExpediente = table.Column<int>(type: "int", nullable: false),
                    IntencionReal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MontoReal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    MonedaReal = table.Column<string>(type: "char(3)", nullable: true),
                    FechaReal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CodigoReal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CamposLegibles = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RutaCorrecta = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ReglaEsperada = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerdadReferencia", x => x.IdExpediente);
                });

            migrationBuilder.CreateTable(
                name: "Transaccion",
                schema: "core",
                columns: table => new
                {
                    CodigoOperacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdCliente = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "char(3)", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comercio = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutenticacionReforzada = table.Column<bool>(type: "bit", nullable: false),
                    Dispositivo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IndicadorRiesgo = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaccion", x => x.CodigoOperacion);
                    table.ForeignKey(
                        name: "FK_Transaccion_ClienteSintetico_IdCliente",
                        column: x => x.IdCliente,
                        principalSchema: "core",
                        principalTable: "ClienteSintetico",
                        principalColumn: "IdCliente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expediente",
                schema: "app",
                columns: table => new
                {
                    IdExpediente = table.Column<int>(type: "int", nullable: false),
                    CodigoOperacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NarracionCliente = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expediente", x => x.IdExpediente);
                    table.ForeignKey(
                        name: "FK_Expediente_Transaccion_CodigoOperacion",
                        column: x => x.CodigoOperacion,
                        principalSchema: "core",
                        principalTable: "Transaccion",
                        principalColumn: "CodigoOperacion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ejecucion",
                schema: "app",
                columns: table => new
                {
                    IdEjecucion = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCorrida = table.Column<int>(type: "int", nullable: true),
                    IdExpediente = table.Column<int>(type: "int", nullable: false),
                    Condicion = table.Column<string>(type: "char(2)", nullable: false),
                    ModeloId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ModeloVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Repeticion = table.Column<byte>(type: "tinyint", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    T_Ocr_ms = table.Column<int>(type: "int", nullable: false),
                    T_Guardrail_ms = table.Column<int>(type: "int", nullable: false),
                    T_Rag_ms = table.Column<int>(type: "int", nullable: false),
                    L_Cls_ms = table.Column<int>(type: "int", nullable: false),
                    L_Gen_ms = table.Column<int>(type: "int", nullable: false),
                    T_Orq_ms = table.Column<int>(type: "int", nullable: false),
                    L_Total_ms = table.Column<int>(type: "int", nullable: false),
                    TokensIn = table.Column<int>(type: "int", nullable: true),
                    TokensOut = table.Column<int>(type: "int", nullable: true),
                    EstadoFinal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IntencionPredicha = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MontoExtraido = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    FechaExtraida = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CodigoExtraido = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ConfMonto = table.Column<decimal>(type: "decimal(4,3)", precision: 4, scale: 3, nullable: true),
                    ConfFecha = table.Column<decimal>(type: "decimal(4,3)", precision: 4, scale: 3, nullable: true),
                    ConfCodigo = table.Column<decimal>(type: "decimal(4,3)", precision: 4, scale: 3, nullable: true),
                    Regeneraciones = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ejecucion", x => x.IdEjecucion);
                    table.ForeignKey(
                        name: "FK_Ejecucion_CorridaBenchmark_IdCorrida",
                        column: x => x.IdCorrida,
                        principalSchema: "app",
                        principalTable: "CorridaBenchmark",
                        principalColumn: "IdCorrida",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ejecucion_Expediente_IdExpediente",
                        column: x => x.IdExpediente,
                        principalSchema: "app",
                        principalTable: "Expediente",
                        principalColumn: "IdExpediente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Evidencia",
                schema: "app",
                columns: table => new
                {
                    IdEvidencia = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdExpediente = table.Column<int>(type: "int", nullable: false),
                    RutaImagen = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    PerturbacionAplicada = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidencia", x => x.IdEvidencia);
                    table.ForeignKey(
                        name: "FK_Evidencia_Expediente_IdExpediente",
                        column: x => x.IdExpediente,
                        principalSchema: "app",
                        principalTable: "Expediente",
                        principalColumn: "IdExpediente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistroManual",
                schema: "app",
                columns: table => new
                {
                    IdRegistro = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdExpediente = table.Column<int>(type: "int", nullable: false),
                    CodigoAnalista = table.Column<string>(type: "char(2)", nullable: false),
                    EsCalibracion = table.Column<bool>(type: "bit", nullable: false),
                    TIngresoUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    TFinalUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    PausasMs = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Intencion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Monto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Ruta = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    TextoRespuesta = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroManual", x => x.IdRegistro);
                    table.ForeignKey(
                        name: "FK_RegistroManual_Expediente_IdExpediente",
                        column: x => x.IdExpediente,
                        principalSchema: "app",
                        principalTable: "Expediente",
                        principalColumn: "IdExpediente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DecisionLegal",
                schema: "app",
                columns: table => new
                {
                    IdEjecucion = table.Column<long>(type: "bigint", nullable: false),
                    Ruta = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Regla = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionLegal", x => x.IdEjecucion);
                    table.ForeignKey(
                        name: "FK_DecisionLegal_Ejecucion_IdEjecucion",
                        column: x => x.IdEjecucion,
                        principalSchema: "app",
                        principalTable: "Ejecucion",
                        principalColumn: "IdEjecucion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Recuperacion",
                schema: "app",
                columns: table => new
                {
                    IdEjecucion = table.Column<long>(type: "bigint", nullable: false),
                    Calificacion = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    FragmentosJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recuperacion", x => x.IdEjecucion);
                    table.ForeignKey(
                        name: "FK_Recuperacion_Ejecucion_IdEjecucion",
                        column: x => x.IdEjecucion,
                        principalSchema: "app",
                        principalTable: "Ejecucion",
                        principalColumn: "IdEjecucion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Resolucion",
                schema: "app",
                columns: table => new
                {
                    IdEjecucion = table.Column<long>(type: "bigint", nullable: false),
                    TextoBorrador = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextoFinal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AprobadaGuardrail = table.Column<bool>(type: "bit", nullable: false),
                    AfirmacionesVerificables = table.Column<int>(type: "int", nullable: true),
                    AfirmacionesNoSustentadas = table.Column<int>(type: "int", nullable: true),
                    ConformeChecklist = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resolucion", x => x.IdEjecucion);
                    table.ForeignKey(
                        name: "FK_Resolucion_Ejecucion_IdEjecucion",
                        column: x => x.IdEjecucion,
                        principalSchema: "app",
                        principalTable: "Ejecucion",
                        principalColumn: "IdEjecucion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransicionEstado",
                schema: "app",
                columns: table => new
                {
                    IdTransicion = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEjecucion = table.Column<long>(type: "bigint", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MarcaUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransicionEstado", x => x.IdTransicion);
                    table.ForeignKey(
                        name: "FK_TransicionEstado_Ejecucion_IdEjecucion",
                        column: x => x.IdEjecucion,
                        principalSchema: "app",
                        principalTable: "Ejecucion",
                        principalColumn: "IdEjecucion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ejecucion_IdCorrida",
                schema: "app",
                table: "Ejecucion",
                column: "IdCorrida");

            migrationBuilder.CreateIndex(
                name: "IX_Ejecucion_IdExpediente",
                schema: "app",
                table: "Ejecucion",
                column: "IdExpediente");

            migrationBuilder.CreateIndex(
                name: "IX_Evidencia_IdExpediente",
                schema: "app",
                table: "Evidencia",
                column: "IdExpediente");

            migrationBuilder.CreateIndex(
                name: "IX_Expediente_CodigoOperacion",
                schema: "app",
                table: "Expediente",
                column: "CodigoOperacion");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroManual_IdExpediente",
                schema: "app",
                table: "RegistroManual",
                column: "IdExpediente");

            migrationBuilder.CreateIndex(
                name: "IX_Transaccion_IdCliente",
                schema: "core",
                table: "Transaccion",
                column: "IdCliente");

            migrationBuilder.CreateIndex(
                name: "IX_TransicionEstado_IdEjecucion",
                schema: "app",
                table: "TransicionEstado",
                column: "IdEjecucion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionLegal",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Evidencia",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Recuperacion",
                schema: "app");

            migrationBuilder.DropTable(
                name: "RegistroManual",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Resolucion",
                schema: "app");

            migrationBuilder.DropTable(
                name: "TipoCambio",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TransicionEstado",
                schema: "app");

            migrationBuilder.DropTable(
                name: "VerdadReferencia",
                schema: "eval");

            migrationBuilder.DropTable(
                name: "Ejecucion",
                schema: "app");

            migrationBuilder.DropTable(
                name: "CorridaBenchmark",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Expediente",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Transaccion",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ClienteSintetico",
                schema: "core");
        }
    }
}
