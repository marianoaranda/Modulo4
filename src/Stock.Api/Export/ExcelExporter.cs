using ClosedXML.Excel;

namespace Stock.Api.Export;

/// <summary>
/// T054 — Exportación a Excel (RF-031, R-05).
///
/// Genera <c>.xlsx</c> nativo con ClosedXML (MIT), no un CSV renombrado que dispararía advertencias
/// de formato al abrirlo.
///
/// Recibe las filas <b>ya filtradas, ordenadas y recortadas</b> por el servicio de consulta. Es lo
/// que hace que RF-031 —"el Excel replica exactamente filas, orden y recorte de la pantalla"— se
/// cumpla por construcción y no por coincidencia entre dos implementaciones: no hay una segunda
/// consulta que pudiera divergir de la primera.
/// </summary>
public class ExcelExporter
{
    public const string TipoDeContenido =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Arma la planilla. Con cero filas escribe igual los encabezados, que es lo que RF-031 pide
    /// para el resultado vacío.
    /// </summary>
    public byte[] Exportar<T>(
        string nombreDeHoja,
        IReadOnlyList<string> encabezados,
        IReadOnlyList<T> filas,
        Func<T, object?[]> aCeldas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add(nombreDeHoja);

        for (var columna = 0; columna < encabezados.Count; columna++)
        {
            hoja.Cell(1, columna + 1).Value = encabezados[columna];
        }

        hoja.Row(1).Style.Font.Bold = true;

        for (var fila = 0; fila < filas.Count; fila++)
        {
            var celdas = aCeldas(filas[fila]);

            for (var columna = 0; columna < celdas.Length; columna++)
            {
                hoja.Cell(fila + 2, columna + 1).Value = XLCellValue.FromObject(celdas[columna]);
            }
        }

        hoja.Columns().AdjustToContents();

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);

        return memoria.ToArray();
    }
}
