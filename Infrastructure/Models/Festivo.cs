using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Nesto#616 (NestoAPI#779): un festivo del calendario (GET api/Festivos?anio=). Los festivos están en la tabla
    /// <c>dbo.Festivos</c>; mientras la tabla no tenga un año, la API devuelve los de la lista del programa con
    /// <see cref="Origen"/> = <see cref="TiposFestivo.ORIGEN_CODIGO"/>.
    /// </summary>
    public class Festivo
    {
        private static readonly CultureInfo Espanol = new CultureInfo("es-ES");

        public DateTime Fecha { get; set; }
        /// <summary>Nacional, Autonómica, Empresa, Algete, Alcobendas o Madrid.</summary>
        public string Tipo { get; set; }
        public string Descripcion { get; set; }
        /// <summary>Solo lectura: quién lo dio de alta o lo cambió.</summary>
        public string Usuario { get; set; }
        /// <summary>Solo lectura.</summary>
        public DateTime? FechaModificacion { get; set; }
        /// <summary>«Tabla» o «Código».</summary>
        public string Origen { get; set; }

        /// <summary>«Lunes», «Martes»…</summary>
        [JsonIgnore]
        public string DiaSemana
        {
            get
            {
                string dia = Espanol.DateTimeFormat.GetDayName(Fecha.DayOfWeek);
                return string.IsNullOrEmpty(dia) ? dia : char.ToUpper(dia[0], Espanol) + dia.Substring(1);
            }
        }

        /// <summary>Viene de la lista del programa, no de la tabla: no cuenta como dado de alta y no se puede dar de baja.</summary>
        [JsonIgnore]
        public bool VieneDelCodigo => string.Equals(Origen, TiposFestivo.ORIGEN_CODIGO, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nesto#616: el cuerpo de POST api/Festivos (alta, o cambio de la descripción si ya está esa fecha con ese tipo).</summary>
    public class PeticionFestivo
    {
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; }
        public string Descripcion { get; set; }
    }

    /// <summary>Nesto#616: los tipos de festivo que admite la API (enum TipoFestivo de NestoAPI) y los orígenes.</summary>
    public static class TiposFestivo
    {
        public const string NACIONAL = "Nacional";
        public const string AUTONOMICA = "Autonómica";
        public const string EMPRESA = "Empresa";
        public const string ALGETE = "Algete";
        public const string ALCOBENDAS = "Alcobendas";
        public const string MADRID = "Madrid";

        public const string ORIGEN_TABLA = "Tabla";
        public const string ORIGEN_CODIGO = "Código";

        /// <summary>La API admite como mucho 100 caracteres (corta lo que sobre).</summary>
        public const int LONGITUD_DESCRIPCION = 100;

        public static IReadOnlyList<string> Todos { get; } = new[] { NACIONAL, AUTONOMICA, EMPRESA, ALGETE, ALCOBENDAS, MADRID };
    }

    /// <summary>La API no ha leído o guardado los festivos: el mensaje es el suyo (403 sin permiso, 409 sin tabla…).</summary>
    public class FestivosException : Exception
    {
        public FestivosException(string motivo, int codigo) : base(motivo)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP con el que ha contestado la API.</summary>
        public int Codigo { get; }
    }
}
