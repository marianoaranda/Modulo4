using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stock.Api.Data.Migrations
{
    /// <summary>
    /// T037 — Agrega al esquema desnudo todo lo que codifica reglas de negocio: columnas
    /// calculadas, <c>CHECK</c>, índices únicos (incluido el filtrado del perfil administrador),
    /// collations, las claves foráneas con su comportamiento de borrado, el índice de cobertura de
    /// la agregación y la vista <c>vw_StockActual</c>. Es lo que pone en verde a T016–T019a.
    ///
    /// <b>Editada a mano</b> en tres puntos, porque el diff de EF Core se calcula contra el
    /// snapshot del modelo y no contra la base real, y la migración inicial se dejó desnuda a
    /// propósito:
    /// <list type="number">
    ///   <item>se quitaron los <c>DropForeignKey</c>/<c>DropIndex</c> de objetos que la migración
    ///         inicial nunca creó;</item>
    ///   <item>se agregó a mano la FK <c>MovimientoDetalle → Movimiento</c> con <c>CASCADE</c>
    ///         (RF-021), que el diff omitió porque en el snapshot ya figuraba sin cambios;</item>
    ///   <item>se agregó el <c>CREATE VIEW</c>, que EF Core Migrations no genera.</item>
    /// </list>
    /// </summary>
    public partial class Restricciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "Salt",
                table: "Usuario",
                type: "varbinary(16)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<string>(
                name: "NombreUsuario",
                table: "Usuario",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "NombreCompleto",
                table: "Usuario",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Hash",
                table: "Usuario",
                type: "varbinary(32)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "EsAdministrador",
                table: "Perfil",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Perfil",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<byte>(
                name: "Tipo",
                table: "Movimiento",
                type: "tinyint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "MachineName",
                table: "ErrorLog",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Margen",
                table: "Articulo",
                type: "decimal(9,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Articulo",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                table: "Articulo",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Modern_Spanish_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioTotal",
                table: "MovimientoDetalle",
                type: "decimal(18,2)",
                nullable: false,
                computedColumnSql: "CAST([Cantidad] * [PrecioUnitario] AS decimal(18,2))",
                stored: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioVenta",
                table: "Articulo",
                type: "decimal(18,2)",
                nullable: false,
                computedColumnSql: "CAST([PrecioCosto] * (1 + [Margen] / 100.0) AS decimal(18,2))",
                stored: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.CreateIndex(
                name: "UX_Usuario_NombreUsuario",
                table: "Usuario",
                column: "NombreUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Perfil_EsAdministrador",
                table: "Perfil",
                column: "EsAdministrador",
                unique: true,
                filter: "[EsAdministrador] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoDetalle_ArticuloId",
                table: "MovimientoDetalle",
                column: "ArticuloId")
                .Annotation("SqlServer:Include", new[] { "Cantidad", "MovimientoNumero" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoDetalle_Cantidad",
                table: "MovimientoDetalle",
                sql: "[Cantidad] > 0 AND [Cantidad] <= 1000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoDetalle_PrecioUnitario",
                table: "MovimientoDetalle",
                sql: "[PrecioUnitario] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Movimiento_Tipo",
                table: "Movimiento",
                sql: "[Tipo] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "UX_Articulo_Codigo",
                table: "Articulo",
                column: "Codigo",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Articulo_OrdenDeStocks",
                table: "Articulo",
                sql: "[StockMinimo] <= [PuntoPedido] AND [PuntoPedido] <= [StockIdeal]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Articulo_ValoresNoNegativos",
                table: "Articulo",
                sql: "[PrecioCosto] >= 0 AND [Margen] >= 0 AND [StockMinimo] >= 0 AND [PuntoPedido] >= 0 AND [StockIdeal] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoDetalle_MovimientoNumero",
                table: "MovimientoDetalle",
                column: "MovimientoNumero");

            // RF-014a: NO ACTION. La baja de un artículo con movimientos se rechaza, de modo que
            // el histórico y el Stock Actual derivado se preserven íntegros.
            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoDetalle_Articulo_ArticuloId",
                table: "MovimientoDetalle",
                column: "ArticuloId",
                principalTable: "Articulo",
                principalColumn: "ArticuloId",
                onDelete: ReferentialAction.NoAction);

            // RF-021: CASCADE. La baja del encabezado arrastra su detalle.
            // Agregada a mano: el diff no la emitió porque en el snapshot del modelo ya figuraba
            // sin cambios, pero la migración inicial no la creó en la base.
            migrationBuilder.AddForeignKey(
                name: "FK_MovimientoDetalle_Movimiento_MovimientoNumero",
                table: "MovimientoDetalle",
                column: "MovimientoNumero",
                principalTable: "Movimiento",
                principalColumn: "Numero",
                onDelete: ReferentialAction.Cascade);

            // RF-002a: NO ACTION. La baja de un perfil con usuarios asignados se rechaza.
            migrationBuilder.AddForeignKey(
                name: "FK_Usuario_Perfil_PerfilId",
                table: "Usuario",
                column: "PerfilId",
                principalTable: "Perfil",
                principalColumn: "PerfilId",
                onDelete: ReferentialAction.NoAction);

            // vw_StockActual: el ÚNICO lugar del sistema donde se calcula el saldo (Principio III).
            // EF Core Migrations no genera vistas, así que va como SQL literal.
            //
            // El LEFT JOIN con ISNULL(..., 0) es lo que hace que un artículo sin movimientos
            // aparezca con Stock Actual 0 en lugar de desaparecer del resultado (RF-030). Un INNER
            // JOIN sería la forma natural de escribirla y estaría mal: un artículo nuevo debe
            // poder pedirse.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_StockActual AS
                SELECT  a.ArticuloId,
                        a.Codigo,
                        a.Descripcion,
                        ISNULL(SUM(CASE WHEN m.Tipo = 1 THEN d.Cantidad ELSE -d.Cantidad END), 0) AS StockActual
                FROM        dbo.Articulo          a
                LEFT JOIN   dbo.MovimientoDetalle d ON d.ArticuloId       = a.ArticuloId
                LEFT JOIN   dbo.Movimiento        m ON m.Numero           = d.MovimientoNumero
                GROUP BY    a.ArticuloId, a.Codigo, a.Descripcion;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_StockActual;");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoDetalle_Articulo_ArticuloId",
                table: "MovimientoDetalle");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoDetalle_Movimiento_MovimientoNumero",
                table: "MovimientoDetalle");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuario_Perfil_PerfilId",
                table: "Usuario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientoDetalle_MovimientoNumero",
                table: "MovimientoDetalle");

            migrationBuilder.DropIndex(
                name: "UX_Usuario_NombreUsuario",
                table: "Usuario");

            migrationBuilder.DropIndex(
                name: "UX_Perfil_EsAdministrador",
                table: "Perfil");

            migrationBuilder.DropIndex(
                name: "IX_MovimientoDetalle_ArticuloId",
                table: "MovimientoDetalle");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoDetalle_Cantidad",
                table: "MovimientoDetalle");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoDetalle_PrecioUnitario",
                table: "MovimientoDetalle");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Movimiento_Tipo",
                table: "Movimiento");

            migrationBuilder.DropIndex(
                name: "UX_Articulo_Codigo",
                table: "Articulo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Articulo_OrdenDeStocks",
                table: "Articulo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Articulo_ValoresNoNegativos",
                table: "Articulo");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Salt",
                table: "Usuario",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(16)");

            migrationBuilder.AlterColumn<string>(
                name: "NombreUsuario",
                table: "Usuario",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "NombreCompleto",
                table: "Usuario",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<byte[]>(
                name: "Hash",
                table: "Usuario",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(32)");

            migrationBuilder.AlterColumn<bool>(
                name: "EsAdministrador",
                table: "Perfil",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Perfil",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioTotal",
                table: "MovimientoDetalle",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldComputedColumnSql: "CAST([Cantidad] * [PrecioUnitario] AS decimal(18,2))");

            migrationBuilder.AlterColumn<int>(
                name: "Tipo",
                table: "Movimiento",
                type: "int",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<string>(
                name: "MachineName",
                table: "ErrorLog",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioVenta",
                table: "Articulo",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldComputedColumnSql: "CAST([PrecioCosto] * (1 + [Margen] / 100.0) AS decimal(18,2))");

            migrationBuilder.AlterColumn<decimal>(
                name: "Margen",
                table: "Articulo",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,4)");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Articulo",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                table: "Articulo",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Modern_Spanish_CI_AS");

            // Sin AddForeignKey ni CreateIndex al revertir: la migración inicial deja el esquema
            // desnudo, sin claves foráneas ni índices más allá de las claves primarias.
        }
    }
}
