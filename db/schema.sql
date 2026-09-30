-- Esquema de referencia (la fuente de verdad serán las migraciones de EF Core)
CREATE SCHEMA core; GO
CREATE SCHEMA app;  GO
CREATE SCHEMA eval; GO

CREATE TABLE core.ClienteSintetico (
  IdCliente INT IDENTITY PRIMARY KEY,
  AliasSeudonimo NVARCHAR(60) NOT NULL,
  DispositivoRegistrado NVARCHAR(60) NOT NULL
);
CREATE TABLE core.TipoCambio (
  Fecha DATE PRIMARY KEY,
  UsdPen DECIMAL(9,4) NOT NULL
);
CREATE TABLE core.Transaccion (
  CodigoOperacion NVARCHAR(20) PRIMARY KEY,
  IdCliente INT NOT NULL REFERENCES core.ClienteSintetico(IdCliente),
  Monto DECIMAL(12,2) NOT NULL,
  Moneda CHAR(3) NOT NULL,              -- PEN | USD
  FechaHora DATETIME2 NOT NULL,
  Comercio NVARCHAR(80) NOT NULL,
  Estado NVARCHAR(20) NOT NULL,         -- COMPLETADA | FALLIDA | NO_COMPLETADA
  AutenticacionReforzada BIT NOT NULL,
  Dispositivo NVARCHAR(60) NULL,
  IndicadorRiesgo BIT NOT NULL DEFAULT 0
);

CREATE TABLE app.Expediente (
  IdExpediente INT PRIMARY KEY,
  CodigoOperacion NVARCHAR(20) NOT NULL REFERENCES core.Transaccion(CodigoOperacion),
  NarracionCliente NVARCHAR(MAX) NOT NULL,
  FechaIngreso DATETIME2 NOT NULL
);
CREATE TABLE app.Evidencia (
  IdEvidencia INT IDENTITY PRIMARY KEY,
  IdExpediente INT NOT NULL REFERENCES app.Expediente(IdExpediente),
  RutaImagen NVARCHAR(260) NOT NULL,
  PerturbacionAplicada NVARCHAR(120) NOT NULL
);
CREATE TABLE app.CorridaBenchmark (
  IdCorrida INT IDENTITY PRIMARY KEY,
  Condicion CHAR(2) NOT NULL,           -- T1 | T2
  InicioUtc DATETIME2 NOT NULL,
  FinUtc DATETIME2 NULL,
  Notas NVARCHAR(400) NULL
);
CREATE TABLE app.Ejecucion (
  IdEjecucion BIGINT IDENTITY PRIMARY KEY,
  IdCorrida INT NULL REFERENCES app.CorridaBenchmark(IdCorrida),
  IdExpediente INT NOT NULL REFERENCES app.Expediente(IdExpediente),
  Condicion CHAR(2) NOT NULL,           -- T1 | T2
  ModeloId NVARCHAR(60) NOT NULL,
  ModeloVersion NVARCHAR(120) NULL,
  PromptVersion NVARCHAR(40) NOT NULL,
  Repeticion TINYINT NOT NULL,
  InicioUtc DATETIME2(3) NOT NULL,
  FinUtc DATETIME2(3) NOT NULL,
  T_Ocr_ms INT NOT NULL, T_Guardrail_ms INT NOT NULL, T_Rag_ms INT NOT NULL,
  L_Cls_ms INT NOT NULL, L_Gen_ms INT NOT NULL, T_Orq_ms INT NOT NULL, L_Total_ms INT NOT NULL,
  TokensIn INT NULL, TokensOut INT NULL,
  EstadoFinal NVARCHAR(20) NOT NULL,    -- Emitido | Derivado | Error
  IntencionPredicha NVARCHAR(20) NULL,
  MontoExtraido DECIMAL(12,2) NULL, FechaExtraida DATETIME2 NULL, CodigoExtraido NVARCHAR(20) NULL,
  ConfMonto DECIMAL(4,3) NULL, ConfFecha DECIMAL(4,3) NULL, ConfCodigo DECIMAL(4,3) NULL,
  Regeneraciones TINYINT NOT NULL DEFAULT 0
);
CREATE TABLE app.TransicionEstado (
  IdTransicion BIGINT IDENTITY PRIMARY KEY,
  IdEjecucion BIGINT NOT NULL REFERENCES app.Ejecucion(IdEjecucion),
  Estado NVARCHAR(20) NOT NULL,
  MarcaUtc DATETIME2(3) NOT NULL
);
CREATE TABLE app.DecisionLegal (
  IdEjecucion BIGINT PRIMARY KEY REFERENCES app.Ejecucion(IdEjecucion),
  Ruta NVARCHAR(15) NOT NULL,           -- Derivar | Procedente | Improcedente
  Regla NVARCHAR(4) NOT NULL,
  Motivo NVARCHAR(200) NOT NULL
);
CREATE TABLE app.Recuperacion (
  IdEjecucion BIGINT PRIMARY KEY REFERENCES app.Ejecucion(IdEjecucion),
  Calificacion NVARCHAR(12) NOT NULL,   -- Correcta | Ambigua | Incorrecta | Omitida
  FragmentosJson NVARCHAR(MAX) NOT NULL -- [{id, score, admitido}]
);
CREATE TABLE app.Resolucion (
  IdEjecucion BIGINT PRIMARY KEY REFERENCES app.Ejecucion(IdEjecucion),
  TextoBorrador NVARCHAR(MAX) NULL,
  TextoFinal NVARCHAR(MAX) NULL,
  AprobadaGuardrail BIT NOT NULL,
  AfirmacionesVerificables INT NULL,
  AfirmacionesNoSustentadas INT NULL,
  ConformeChecklist BIT NULL            -- lo completa la revisión normativa
);
CREATE TABLE app.RegistroManual (
  IdRegistro INT IDENTITY PRIMARY KEY,
  IdExpediente INT NOT NULL REFERENCES app.Expediente(IdExpediente),
  CodigoAnalista CHAR(2) NOT NULL,      -- A1 | A2 | A3
  EsCalibracion BIT NOT NULL,
  TIngresoUtc DATETIME2(3) NOT NULL,
  TFinalUtc DATETIME2(3) NOT NULL,
  PausasMs INT NOT NULL DEFAULT 0,
  Intencion NVARCHAR(20) NULL, Monto DECIMAL(12,2) NULL, Fecha DATE NULL, Codigo NVARCHAR(20) NULL,
  Ruta NVARCHAR(15) NULL,
  TextoRespuesta NVARCHAR(MAX) NULL
);

CREATE TABLE eval.VerdadReferencia (
  IdExpediente INT PRIMARY KEY,
  IntencionReal NVARCHAR(20) NOT NULL,
  MontoReal DECIMAL(12,2) NULL, MonedaReal CHAR(3) NULL,
  FechaReal DATETIME2 NULL,
  CodigoReal NVARCHAR(20) NULL,
  CamposLegibles NVARCHAR(40) NOT NULL, -- p. ej. 'monto,fecha,codigo'
  RutaCorrecta NVARCHAR(15) NOT NULL,
  ReglaEsperada NVARCHAR(4) NOT NULL
);
