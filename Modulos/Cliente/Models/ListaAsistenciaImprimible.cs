using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>Una columna de la hoja para pasar lista: el título, su ancho relativo y qué se escribe en cada fila.</summary>
    public sealed class ColumnaListaAsistencia
    {
        public ColumnaListaAsistencia(string titulo, double ancho, Func<int, InscripcionEventoModel, EventoModel, string> valor)
        {
            Titulo = titulo;
            Ancho = ancho;
            Valor = valor ?? ((n, i, e) => string.Empty);
        }

        public string Titulo { get; }
        /// <summary>Proporcional (como «2*» en WPF): las columnas se reparten el ancho de la hoja.</summary>
        public double Ancho { get; }
        /// <summary>El texto de la fila: con el número de fila (1, 2…), la inscripción y el evento.</summary>
        public Func<int, InscripcionEventoModel, EventoModel, string> Valor { get; }
    }

    /// <summary>
    /// Sugerencia 1362 (Pilar, 19/11/26): la hoja para pasar lista el día de la formación. Una fila por inscripción NO anulada ni caducada,
    /// por nombre del cliente. Las columnas están en <see cref="COLUMNAS"/>: para quitar, añadir o reordenar, solo ahí (Pilar aún
    /// no ha dicho cuáles quiere). Lo que dibuja la hoja (FlowDocument + PrintDialog) es <c>ImpresoraListaAsistencia</c>.
    /// </summary>
    public static class ListaAsistenciaImprimible
    {
        private static readonly CultureInfo ES = new("es-ES");

        public static readonly IReadOnlyList<ColumnaListaAsistencia> COLUMNAS = new[]
        {
            new ColumnaListaAsistencia("Nº", 0.5, (n, i, e) => n.ToString(ES)),
            new ColumnaListaAsistencia("Cliente", 4, (n, i, e) => TextoCliente(i)),
            // El de la persona que asiste y, si no lo tiene (o no hay persona), el de la ficha del cliente.
            new ColumnaListaAsistencia("Teléfono", 1.6, (n, i, e) => TextoTelefono(i)),
            new ColumnaListaAsistencia("Pers.", 0.6, (n, i, e) => i.NumeroAsistentes.ToString(ES)),
            new ColumnaListaAsistencia("Pago", 1.4, (n, i, e) => TextoPago(i)),
            new ColumnaListaAsistencia("Asistencia", 1.3, (n, i, e) => TextoFormaAsistencia(i, e)),
            new ColumnaListaAsistencia("Asiste", 0.9, (n, i, e) => string.Empty),
            new ColumnaListaAsistencia("Firma", 2.4, (n, i, e) => string.Empty)
        };

        /// <summary>Sugerencia 1362 (fleco): el teléfono de la persona que asiste; sin él, el de la ficha del cliente/contacto.</summary>
        public static string TextoTelefono(InscripcionEventoModel inscripcion)
        {
            if (inscripcion == null)
            {
                return string.Empty;
            }
            string persona = inscripcion.TelefonoPersona;
            return !string.IsNullOrWhiteSpace(persona) ? persona : inscripcion.Telefono?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Las inscripciones que salen en la hoja: todas menos las anuladas y las caducadas (no vienen: Carlos 20/11/26), por nombre
        /// (y por número de cliente).
        /// </summary>
        public static List<InscripcionEventoModel> Filas(IEnumerable<InscripcionEventoModel> inscripciones)
        {
            if (inscripciones == null)
            {
                return new List<InscripcionEventoModel>();
            }
            return inscripciones
                .Where(i => i != null && i.Estado != InscripcionEventoModel.ANULADA && i.Estado != InscripcionEventoModel.CADUCADA)
                .OrderBy(i => (i.Nombre ?? string.Empty).Trim(), StringComparer.Create(ES, true))
                .ThenBy(i => i.ClienteContacto, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>La tabla ya escrita: una lista de celdas por fila, en el orden de <see cref="COLUMNAS"/>.</summary>
        public static List<string[]> Tabla(EventoModel evento, IEnumerable<InscripcionEventoModel> inscripciones)
        {
            List<InscripcionEventoModel> filas = Filas(inscripciones);
            var tabla = new List<string[]>(filas.Count);
            for (int n = 0; n < filas.Count; n++)
            {
                tabla.Add(COLUMNAS.Select(c => c.Valor(n + 1, filas[n], evento) ?? string.Empty).ToArray());
            }
            return tabla;
        }

        public static string Titulo(EventoModel evento) => string.IsNullOrWhiteSpace(evento?.Titulo) ? "Lista de asistencia" : evento.Titulo.Trim();

        /// <summary>«Jueves 20/11/2026 · 10:00 – 13:00 · Reina» (lo que no haya, se omite).</summary>
        public static string Subtitulo(EventoModel evento)
        {
            if (evento == null)
            {
                return string.Empty;
            }
            string fecha = evento.Fecha == default ? null : Mayuscula(evento.Fecha.ToString("dddd dd/MM/yyyy", ES));
            string tienda = string.IsNullOrWhiteSpace(evento.NombreTienda) ? null : evento.NombreTienda;
            return string.Join(" · ", new[] { fecha, evento.TextoHorario, tienda }.Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        /// <summary>«12 inscripciones · 14 asistentes».</summary>
        public static string Resumen(IEnumerable<InscripcionEventoModel> inscripciones)
        {
            List<InscripcionEventoModel> filas = Filas(inscripciones);
            int personas = filas.Sum(i => i.NumeroAsistentes);
            string textoInscripciones = filas.Count == 1 ? "1 inscripción" : $"{filas.Count} inscripciones";
            return $"{textoInscripciones} · {InscripcionEventoModel.TextoPersonas(personas)}";
        }

        /// <summary>«PELUQUERÍA LA RUBIA (15191/0) — Ana Luna»: el cliente, su número y, si la hay, la persona que asiste.</summary>
        public static string TextoCliente(InscripcionEventoModel inscripcion)
        {
            string nombre = (inscripcion.Nombre ?? string.Empty).Trim();
            string cliente = string.IsNullOrWhiteSpace(inscripcion.Cliente) ? string.Empty : $" ({inscripcion.ClienteContacto})";
            string persona = inscripcion.TextoAsiste;
            return string.IsNullOrWhiteSpace(persona) ? $"{nombre}{cliente}" : $"{nombre}{cliente} — {persona}";
        }

        /// <summary>«Gratuita» o el estado de la inscripción («Pagada», «Reservada», «Facturada»…).</summary>
        public static string TextoPago(InscripcionEventoModel inscripcion)
        {
            if (inscripcion.EsGratuita)
            {
                return "Gratuita";
            }
            return string.IsNullOrWhiteSpace(inscripcion.EstadoTexto) ? inscripcion.Estado ?? string.Empty : inscripcion.EstadoTexto.Trim();
        }

        /// <summary>Presencial u Online: lo que dijo el inscrito en un evento mixto o, si no, el formato del evento.</summary>
        public static string TextoFormaAsistencia(InscripcionEventoModel inscripcion, EventoModel evento)
        {
            string forma = inscripcion.FormaAsistenciaGuardada;
            if (!string.IsNullOrEmpty(forma))
            {
                return forma;
            }
            if (evento == null || evento.EsMixto)
            {
                return evento == null ? string.Empty : "Sin indicar";
            }
            return evento.EsOnline ? "Online" : "Presencial";
        }

        private static string Mayuscula(string texto) => string.IsNullOrEmpty(texto) ? texto : char.ToUpper(texto[0], ES) + texto.Substring(1);
    }
}
