using Microsoft.Reporting.NETCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Render centralizado de informes RDLC reutilizando un <see cref="LocalReport"/> por
    /// informe. Cada <see cref="LocalReport"/> compila el ensamblado de expresiones
    /// (ExpressionHost) una sola vez al cargar la definición. Crear un LocalReport nuevo en
    /// cada render —como se hacía antes en cada ViewModel— provocaba que cada Render cargara
    /// un ensamblado dinámico nuevo que el AssemblyLoadContext por defecto nunca descarga:
    /// la memoria del proceso subía con cada informe y no volvía a bajar (memory leak).
    /// Reutilizando la instancia, el ExpressionHost se compila una vez por tipo de informe.
    /// </summary>
    public static class RenderizadorInformes
    {
        private static readonly ConcurrentDictionary<string, LocalReport> _cache =
            new ConcurrentDictionary<string, LocalReport>();

        /// <summary>
        /// Render a PDF del caso habitual: un único dataset y parámetros opcionales.
        /// </summary>
        public static byte[] RenderizarPdf(string recursoRdlc, string nombreDataSet,
            object dataSource, IEnumerable<ReportParameter> parametros = null)
            => Renderizar(recursoRdlc, "PDF",
                new[] { new ReportDataSource(nombreDataSet, dataSource) }, parametros);

        /// <summary>
        /// Render general: permite varios datasets y elegir el formato (p.ej. "PDF" o
        /// "EXCELOPENXML"). Reutiliza el LocalReport cacheado para el recurso indicado.
        /// </summary>
        public static byte[] Renderizar(string recursoRdlc, string formato,
            IEnumerable<ReportDataSource> dataSources,
            IEnumerable<ReportParameter> parametros = null)
        {
            LocalReport report = ObtenerReport(recursoRdlc);

            // El LocalReport no es thread-safe y es compartido (cacheado): serializamos los
            // renders del mismo informe. Informes distintos usan instancias distintas.
            lock (report)
            {
                try
                {
                    report.DataSources.Clear();
                    foreach (ReportDataSource ds in dataSources)
                    {
                        report.DataSources.Add(ds);
                    }

                    List<ReportParameter> lista = parametros?.ToList();
                    if (lista != null && lista.Count > 0)
                    {
                        report.SetParameters(lista);
                    }

                    return report.Render(formato);
                }
                finally
                {
                    // Soltar la referencia a los datos (pueden ser grandes) para que no queden
                    // retenidos por la instancia cacheada hasta el siguiente render.
                    report.DataSources.Clear();
                }
            }
        }

        private static LocalReport ObtenerReport(string recursoRdlc)
            => _cache.GetOrAdd(recursoRdlc, clave =>
            {
                LocalReport report = new LocalReport();
                using (Stream definicion = Assembly.LoadFrom("Informes").GetManifestResourceStream(clave))
                {
                    if (definicion == null)
                    {
                        throw new InvalidOperationException(
                            $"No se encontró el recurso RDLC '{clave}' en el ensamblado Informes.");
                    }
                    report.LoadReportDefinition(definicion);
                }
                return report;
            });
    }
}
