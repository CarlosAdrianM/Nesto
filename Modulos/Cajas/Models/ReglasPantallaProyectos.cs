using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models
{
    /// <summary>
    /// Proyectos (gemela de NestoAPI#901): lo que la pantalla decide sola (puro, con tests). Las reglas de verdad (código, signos,
    /// reparto, cerrado) son de la API: aquí solo se avisa antes de mandar lo que seguro que no vale.
    /// </summary>
    public static class ReglasPantallaProyectos
    {
        public const string ESTADO_ABIERTO = "Abierto";
        public const string ESTADO_CERRADO = "Cerrado";
        /// <summary>Lo que la API admite de más en la suma de un reparto (redondeos de las partes iguales).</summary>
        public const decimal TOLERANCIA = 0.01M;
        public const int LONGITUD_CODIGO = 20;
        public const int LONGITUD_NOMBRE = 100;
        public const int LONGITUD_COMENTARIO_IMPUTACION = 200;

        private static readonly CultureInfo Espanol = new CultureInfo("es-ES");

        /// <summary>Los tipos de la API y cómo se leen en pantalla.</summary>
        public static readonly IReadOnlyList<OpcionTipoProyecto> Tipos = new List<OpcionTipoProyecto>
        {
            new OpcionTipoProyecto("CursoSEPE", "Curso del SEPE"),
            new OpcionTipoProyecto("Evento", "Evento"),
            new OpcionTipoProyecto("Feria", "Feria"),
            new OpcionTipoProyecto("Campana", "Campaña"),
            new OpcionTipoProyecto("Otro", "Otro")
        };

        public static string NombreTipo(string? tipo)
        {
            if (string.IsNullOrWhiteSpace(tipo))
            {
                return string.Empty;
            }
            OpcionTipoProyecto? opcion = Tipos.FirstOrDefault(t => string.Equals(t.Codigo, tipo.Trim(), StringComparison.OrdinalIgnoreCase));
            return opcion?.Nombre ?? tipo.Trim();
        }

        public static string Euros(decimal importe) => importe.ToString("N2", Espanol) + " €";

        /// <summary>«IMPE0210-2026-1 (60,00 €), FERIA-2026 (40,00 €)»; vacío si no hay ninguna.</summary>
        public static string TextoImputaciones(IEnumerable<ImputacionApunteModel>? imputaciones)
        {
            List<ImputacionApunteModel> lista = imputaciones?.Where(i => i != null).ToList() ?? new List<ImputacionApunteModel>();
            return string.Join(", ", lista.Select(i =>
                $"{(string.IsNullOrWhiteSpace(i.CodigoProyecto) ? i.ProyectoId.ToString(CultureInfo.InvariantCulture) : i.CodigoProyecto!.Trim())} ({Euros(i.Importe)})"));
        }

        /// <summary>El margen («35,20 %») o «—» si no hay ingresos.</summary>
        public static string TextoMargen(decimal? margen) => margen.HasValue ? margen.Value.ToString("N2", Espanol) + " %" : "—";

        /// <summary>
        /// Lo que seguro que la API rechaza en el alta o la edición (de usted). Vacío si se puede mandar (la API puede decir más).
        /// </summary>
        public static List<string> MotivosProyectoNoValido(string? codigo, string? nombre, string? tipo, DateTime? desde, DateTime? hasta,
            decimal? presupuestoIngresos, decimal? presupuestoGastos)
        {
            var motivos = new List<string>();
            string limpio = codigo?.Trim() ?? string.Empty;
            if (limpio.Length == 0)
            {
                motivos.Add("Falta el código del proyecto (por ejemplo, IMPE0210-2026-1).");
            }
            else if (limpio.Length > LONGITUD_CODIGO)
            {
                motivos.Add($"El código puede tener como mucho {LONGITUD_CODIGO} caracteres.");
            }
            string nombreLimpio = nombre?.Trim() ?? string.Empty;
            if (nombreLimpio.Length == 0)
            {
                motivos.Add("Falta el nombre del proyecto.");
            }
            else if (nombreLimpio.Length > LONGITUD_NOMBRE)
            {
                motivos.Add($"El nombre puede tener como mucho {LONGITUD_NOMBRE} caracteres.");
            }
            if (string.IsNullOrWhiteSpace(tipo))
            {
                motivos.Add("Elija el tipo de proyecto.");
            }
            if (desde.HasValue && hasta.HasValue && hasta.Value.Date < desde.Value.Date)
            {
                motivos.Add("La fecha de fin no puede ser anterior a la de inicio.");
            }
            if (presupuestoIngresos < 0 || presupuestoGastos < 0)
            {
                motivos.Add("Los presupuestos no pueden ser negativos.");
            }
            return motivos;
        }

        /// <summary>
        /// La comparativa en una tabla para la rejilla: columnas «Concepto» y P0, P1… (una por proyecto, con su código en Caption: la
        /// vista lo pone de cabecera, porque un código con «.» o «/» no vale de nombre de columna enlazada).
        /// </summary>
        public static DataTable TablaComparativa(ComparativaProyectosModel? comparativa)
        {
            var tabla = new DataTable("Comparativa");
            tabla.Columns.Add(new DataColumn("Concepto", typeof(string)) { Caption = "Cuenta" });
            List<ProyectoModel> proyectos = comparativa?.Proyectos ?? new List<ProyectoModel>();
            for (int i = 0; i < proyectos.Count; i++)
            {
                string cabecera = string.IsNullOrWhiteSpace(proyectos[i].Codigo) ? proyectos[i].Id.ToString(CultureInfo.InvariantCulture) : proyectos[i].Codigo!.Trim();
                tabla.Columns.Add(new DataColumn("P" + i.ToString(CultureInfo.InvariantCulture), typeof(string)) { Caption = cabecera });
            }
            if (comparativa == null)
            {
                return tabla;
            }

            void Fila(string concepto, Func<int, string> valor)
            {
                DataRow fila = tabla.NewRow();
                fila[0] = concepto;
                for (int i = 0; i < proyectos.Count; i++)
                {
                    fila[i + 1] = valor(i);
                }
                tabla.Rows.Add(fila);
            }

            static string Importe(List<decimal>? lista, int i) => lista != null && i < lista.Count ? Euros(lista[i]) : string.Empty;

            Fila("INGRESOS", _ => string.Empty);
            foreach (FilaComparativaProyectosModel f in comparativa.Ingresos ?? new List<FilaComparativaProyectosModel>())
            {
                Fila($"{f.Cuenta?.Trim()} {f.NombreCuenta?.Trim()}".Trim(), i => Importe(f.Importes, i));
            }
            Fila("Total ingresos", i => Importe(comparativa.TotalesIngresos, i));
            Fila("GASTOS", _ => string.Empty);
            foreach (FilaComparativaProyectosModel f in comparativa.Gastos ?? new List<FilaComparativaProyectosModel>())
            {
                Fila($"{f.Cuenta?.Trim()} {f.NombreCuenta?.Trim()}".Trim(), i => Importe(f.Importes, i));
            }
            Fila("Total gastos", i => Importe(comparativa.TotalesGastos, i));
            Fila("Resultado", i => Importe(comparativa.Resultados, i));
            Fila("Margen", i => comparativa.Margenes != null && i < comparativa.Margenes.Count ? TextoMargen(comparativa.Margenes[i]) : string.Empty);
            return tabla;
        }
    }

    public sealed class OpcionTipoProyecto
    {
        public OpcionTipoProyecto(string? codigo, string nombre)
        {
            Codigo = codigo;
            Nombre = nombre;
        }

        /// <summary>El valor de la API; null en la opción «Todos» del filtro.</summary>
        public string? Codigo { get; }
        public string Nombre { get; }
        public override string ToString() => Nombre;
    }

    /// <summary>Una fila del reparto tal como la ve el cálculo (sin WPF).</summary>
    public readonly struct ParteReparto
    {
        public ParteReparto(int? proyectoId, decimal? importe, string? comentario = null)
        {
            ProyectoId = proyectoId;
            Importe = importe;
            Comentario = comentario;
        }

        public int? ProyectoId { get; }
        public decimal? Importe { get; }
        public string? Comentario { get; }
    }

    /// <summary>
    /// EL CÁLCULO del reparto de un apunte entre proyectos (puro): lo pendiente en vivo, las partes iguales con el céntimo de
    /// ajuste y lo que no deja guardar. Las mismas reglas que la API (importes positivos, un proyecto una vez, la suma no pasa
    /// del apunte más 1 céntimo), para no mandar lo que va a rechazar.
    /// </summary>
    public static class CalculoRepartoApunte
    {
        public static decimal Redondear(decimal importe) => Math.Round(importe, 2, MidpointRounding.AwayFromZero);

        public static decimal Imputado(IEnumerable<ParteReparto>? partes)
            => Redondear(partes?.Where(p => p.Importe.HasValue).Sum(p => p.Importe!.Value) ?? 0M);

        /// <summary>Importe del apunte − lo repartido (negativo si se pasa).</summary>
        public static decimal Pendiente(decimal importeApunte, IEnumerable<ParteReparto>? partes)
            => Redondear(importeApunte - Imputado(partes));

        /// <summary>El porcentaje del apunte (2 decimales); 0 si el apunte es 0.</summary>
        public static decimal Porcentaje(decimal importe, decimal importeApunte)
            => importeApunte == 0M ? 0M : Redondear(importe * 100M / importeApunte);

        /// <summary>
        /// El importe en <paramref name="partes"/> partes iguales a céntimos: todas iguales por abajo y la última lleva el céntimo
        /// (o los céntimos) de ajuste, para que sumen justo el importe (100 entre 3 = 33,33 + 33,33 + 33,34).
        /// </summary>
        public static List<decimal> PartesIguales(decimal importe, int partes)
        {
            var resultado = new List<decimal>();
            if (partes <= 0)
            {
                return resultado;
            }
            decimal total = Redondear(importe);
            decimal cada = Math.Truncate(total * 100M / partes) / 100M;
            for (int i = 0; i < partes - 1; i++)
            {
                resultado.Add(cada);
            }
            resultado.Add(Redondear(total - cada * (partes - 1)));
            return resultado;
        }

        /// <summary>Lo que impide guardar el reparto (de usted); vacío si se puede guardar. Una lista vacía = quitar el reparto: vale.</summary>
        public static List<string> Motivos(decimal importeApunte, IEnumerable<ParteReparto>? partes)
        {
            var motivos = new List<string>();
            List<ParteReparto> lista = partes?.ToList() ?? new List<ParteReparto>();
            int numero = 0;
            foreach (ParteReparto p in lista)
            {
                numero++;
                bool vacia = !p.ProyectoId.HasValue && (!p.Importe.HasValue || p.Importe.Value == 0M) && string.IsNullOrWhiteSpace(p.Comentario);
                if (vacia)
                {
                    continue; // una fila en blanco no cuenta (no se manda)
                }
                if (!p.ProyectoId.HasValue)
                {
                    motivos.Add($"La fila {numero} no tiene proyecto.");
                }
                if (!p.Importe.HasValue || p.Importe.Value <= 0M)
                {
                    motivos.Add($"El importe de la fila {numero} tiene que ser mayor que cero (sin signo: el abono lo da el apunte).");
                }
                else if (Redondear(p.Importe.Value) != p.Importe.Value)
                {
                    motivos.Add($"El importe de la fila {numero} tiene más de dos decimales.");
                }
                if ((p.Comentario?.Trim().Length ?? 0) > ReglasPantallaProyectos.LONGITUD_COMENTARIO_IMPUTACION)
                {
                    motivos.Add($"El comentario de la fila {numero} puede tener como mucho {ReglasPantallaProyectos.LONGITUD_COMENTARIO_IMPUTACION} caracteres.");
                }
            }
            if (lista.Where(p => p.ProyectoId.HasValue).GroupBy(p => p.ProyectoId!.Value).Any(g => g.Count() > 1))
            {
                motivos.Add("Un mismo proyecto está en varias filas: júntelas en una.");
            }
            decimal imputado = Imputado(lista.Where(p => p.Importe > 0M));
            if (imputado > importeApunte + ReglasPantallaProyectos.TOLERANCIA)
            {
                motivos.Add($"Lo repartido ({ReglasPantallaProyectos.Euros(imputado)}) pasa del importe del apunte ({ReglasPantallaProyectos.Euros(importeApunte)}).");
            }
            return motivos;
        }

        /// <summary>Las filas que se mandan (las que no están en blanco), con el comentario limpio.</summary>
        public static List<ImputacionPedida> ParaEnviar(IEnumerable<ParteReparto>? partes)
            => (partes ?? Enumerable.Empty<ParteReparto>())
                .Where(p => p.ProyectoId.HasValue && p.Importe.HasValue && p.Importe.Value > 0M)
                .Select(p => new ImputacionPedida
                {
                    ProyectoId = p.ProyectoId!.Value,
                    Importe = p.Importe!.Value,
                    Comentario = string.IsNullOrWhiteSpace(p.Comentario) ? null : p.Comentario!.Trim()
                })
                .ToList();

        /// <summary>«Pendiente: 40,00 € de 100,00 € (60 % repartido)», «Repartido entero» o «Se pasa en 0,50 €».</summary>
        public static string TextoPendiente(decimal importeApunte, IEnumerable<ParteReparto>? partes)
        {
            decimal pendiente = Pendiente(importeApunte, partes);
            decimal imputado = Imputado(partes);
            if (pendiente < -ReglasPantallaProyectos.TOLERANCIA)
            {
                return $"Se pasa en {ReglasPantallaProyectos.Euros(-pendiente)} del importe del apunte ({ReglasPantallaProyectos.Euros(importeApunte)}).";
            }
            if (Math.Abs(pendiente) <= ReglasPantallaProyectos.TOLERANCIA && imputado > 0M)
            {
                return $"Repartido entero ({ReglasPantallaProyectos.Euros(importeApunte)}).";
            }
            return $"Pendiente: {ReglasPantallaProyectos.Euros(pendiente)} de {ReglasPantallaProyectos.Euros(importeApunte)} " +
                $"({Porcentaje(imputado, importeApunte).ToString("N2", new CultureInfo("es-ES"))} % repartido).";
        }
    }
}
