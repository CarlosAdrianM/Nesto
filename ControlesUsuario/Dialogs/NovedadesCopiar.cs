using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Novedades (Carlos, 19/11/26): el texto plano de «Copiar datos», «Copiar con comentarios» y «Copiar comentario» del botón
    /// derecho, para pegárselo al asistente. Puro (sin portapapeles ni WPF):
    /// <code>
    /// Sugerencia 1362 (Nesto) — Pilar, 19/11/2026 10:21 — Estado: Pendiente
    /// &lt;lo que escribió el usuario, tal cual&gt;
    /// Descripción: &lt;la redactada, si la hay y es distinta&gt;
    /// Capturas: 1 · Comentarios: 3
    /// </code>
    /// </summary>
    public static class TextoCopiaNovedad
    {
        public const string NOVEDAD = "Novedad";
        public const string SUGERENCIA = "Sugerencia";
        public const string INCIDENCIA = "Incidencia";
        private const string RAYA = " — ";

        public static string Tipo(NovedadUsuario novedad)
            => novedad == null ? NOVEDAD : novedad.EsSugerencia ? (novedad.EsIncidencia ? INCIDENCIA : SUGERENCIA) : NOVEDAD;

        /// <summary>«Nesto» si viene sin ámbito; si no, el suyo (Ariadna, NestoApp, NestoAPI).</summary>
        public static string Aplicacion(string ambito) => string.IsNullOrWhiteSpace(ambito) ? AmbitosNovedades.NESTO : ambito.Trim();

        /// <summary>
        /// «Sugerencia 1362 (Nesto) — Pilar, 19/11/2026 10:21 — Estado: Pendiente» o, en una novedad publicada,
        /// «Novedad 1350 (Nesto, versión 1.10.64.0) — 18/11/2026».
        /// </summary>
        public static string Cabecera(NovedadUsuario novedad)
        {
            if (novedad == null)
            {
                return string.Empty;
            }
            string version = novedad.EsSugerencia || string.IsNullOrWhiteSpace(novedad.Version) ? string.Empty : $", versión {novedad.Version.Trim()}";
            var partes = new List<string> { $"{Tipo(novedad)} {novedad.Id} ({Aplicacion(novedad.Ambito)}{version})" };
            if (novedad.EsSugerencia)
            {
                string quien = novedad.SugeridaNombre?.Trim();
                string cuando = novedad.SugeridaFecha.HasValue ? novedad.SugeridaFecha.Value.ToString("dd/MM/yyyy HH:mm") : null;
                string quienCuando = string.Join(", ", new[] { quien, cuando }.Where(t => !string.IsNullOrWhiteSpace(t)));
                if (quienCuando.Length > 0)
                {
                    partes.Add(quienCuando);
                }
                if (!string.IsNullOrWhiteSpace(novedad.Estado))
                {
                    partes.Add($"Estado: {novedad.Estado.Trim()}");
                }
            }
            else if (novedad.Fecha != default)
            {
                partes.Add(novedad.Fecha.ToString("dd/MM/yyyy"));
            }
            return string.Join(RAYA, partes);
        }

        /// <summary>Cuántas capturas lleva: las de la lista o, con la API de antes, la única (si TieneImagen).</summary>
        public static int NumeroCapturas(NovedadUsuario novedad)
        {
            if (novedad?.Capturas != null && novedad.Capturas.Count > 0)
            {
                return novedad.Capturas.Count;
            }
            return novedad?.TieneImagen == true ? 1 : 0;
        }

        /// <summary>«Copiar datos»: cabecera, título (si no repite el texto), texto del usuario, descripción si es distinta y recuento.</summary>
        public static string Datos(NovedadUsuario novedad, int numeroComentarios)
        {
            if (novedad == null)
            {
                return string.Empty;
            }
            var texto = new StringBuilder();
            texto.AppendLine(Cabecera(novedad));
            string titulo = Limpio(novedad.Titulo);
            string original = Limpio(novedad.TextoOriginal);
            string descripcion = Limpio(novedad.Descripcion);
            if (titulo.Length > 0 && !Igual(titulo, original) && !Igual(titulo, descripcion))
            {
                texto.AppendLine(titulo);
            }
            if (original.Length > 0)
            {
                texto.AppendLine(original);
            }
            if (descripcion.Length > 0 && !Igual(descripcion, original))
            {
                texto.AppendLine(original.Length > 0 ? $"Descripción: {descripcion}" : descripcion);
            }
            texto.Append($"Capturas: {NumeroCapturas(novedad)} · Comentarios: {Math.Max(0, numeroComentarios)}");
            return texto.ToString();
        }

        /// <summary>«Copiar con comentarios»: los datos y, debajo, cada comentario («autor, fecha: texto»).</summary>
        public static string DatosConComentarios(NovedadUsuario novedad, IReadOnlyList<ComentarioItem> comentarios)
        {
            List<ComentarioItem> lista = comentarios?.Where(c => c != null).ToList() ?? new List<ComentarioItem>();
            var texto = new StringBuilder(Datos(novedad, lista.Count));
            if (lista.Count > 0)
            {
                texto.AppendLine();
                texto.AppendLine();
                texto.Append("Comentarios:");
                foreach (ComentarioItem comentario in lista)
                {
                    texto.AppendLine();
                    texto.Append(LineaComentario(comentario));
                }
            }
            return texto.ToString();
        }

        /// <summary>«Laura, 19/11/2026 10:30: el texto» (con «[2 capturas]» si las lleva).</summary>
        public static string LineaComentario(ComentarioItem comentario)
        {
            if (comentario == null)
            {
                return string.Empty;
            }
            string autor = string.IsNullOrWhiteSpace(comentario.NombreVisible) ? "(sin nombre)" : comentario.NombreVisible.Trim();
            int capturas = comentario.Capturas?.Count > 0 ? comentario.Capturas.Count : comentario.TieneImagen ? 1 : 0;
            string textoCapturas = capturas == 0 ? string.Empty : capturas == 1 ? " [1 captura]" : $" [{capturas} capturas]";
            return $"{autor}, {comentario.Fecha:dd/MM/yyyy HH:mm}: {Limpio(comentario.Texto)}{textoCapturas}";
        }

        /// <summary>«Copiar comentario»: «Comentario de la sugerencia 1362 (Nesto) — Laura, 19/11/2026 10:30: el texto».</summary>
        public static string Comentario(NovedadUsuario novedad, ComentarioItem comentario)
        {
            if (comentario == null)
            {
                return string.Empty;
            }
            string deQue = novedad == null ? string.Empty : $" de la {Tipo(novedad).ToLowerInvariant()} {novedad.Id} ({Aplicacion(novedad.Ambito)})";
            return $"Comentario{deQue}{RAYA}{LineaComentario(comentario)}";
        }

        private static string Limpio(string texto) => (texto ?? string.Empty).Trim();

        private static bool Igual(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);
    }
}
