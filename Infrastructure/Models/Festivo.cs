using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
        /// <summary>Nacional, Autonómica, Empresa, Algete, Alcobendas, Madrid o RepartoPropio (NestoAPI#841).</summary>
        public string Tipo { get; set; }

        /// <summary>NestoAPI#841: el tipo como se enseña («Sin reparto propio (furgoneta)» para RepartoPropio).</summary>
        [JsonIgnore]
        public string TipoEtiqueta => TiposFestivo.Etiqueta(Tipo);

        /// <summary>NestoAPI#841: ese día no sale la furgoneta (las entregas de nuestra ruta van al siguiente día de reparto).</summary>
        [JsonIgnore]
        public bool EsSinRepartoPropio => string.Equals(Tipo?.Trim(), TiposFestivo.REPARTO_PROPIO, StringComparison.OrdinalIgnoreCase);
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
        /// <summary>
        /// NestoAPI#841: «día sin reparto propio»: la furgoneta no sale (no es fiesta de nadie, es operativo, como «Empresa»).
        /// Las entregas de nuestra ruta (AT, 16) y las reposiciones de las tiendas se van al siguiente día de reparto.
        /// </summary>
        public const string REPARTO_PROPIO = "RepartoPropio";
        public const string ETIQUETA_REPARTO_PROPIO = "Sin reparto propio (furgoneta)";

        /// <summary>NestoAPI#841: el aviso de Picking y de Agencias (reparto propio) cuando hoy no sale la furgoneta.</summary>
        public const string AVISO_HOY_SIN_REPARTO_PROPIO = "Hoy no hay reparto propio: las entregas de nuestra ruta se mueven al siguiente día de reparto.";

        public const string ORIGEN_TABLA = "Tabla";
        public const string ORIGEN_CODIGO = "Código";

        /// <summary>La API admite como mucho 100 caracteres (corta lo que sobre).</summary>
        public const int LONGITUD_DESCRIPCION = 100;

        public static IReadOnlyList<string> Todos { get; } = new[] { NACIONAL, AUTONOMICA, EMPRESA, ALGETE, ALCOBENDAS, MADRID, REPARTO_PROPIO };

        /// <summary>NestoAPI#841: los tipos con su etiqueta, para el desplegable (Valor = lo que va a la API).</summary>
        public static IReadOnlyList<OpcionTipoFestivo> Opciones { get; } = Todos.Select(t => new OpcionTipoFestivo(t, Etiqueta(t))).ToList();

        /// <summary>NestoAPI#841: cómo se enseña cada tipo. Todos se enseñan tal cual salvo RepartoPropio.</summary>
        public static string Etiqueta(string tipo)
            => string.Equals(tipo?.Trim(), REPARTO_PROPIO, StringComparison.OrdinalIgnoreCase) ? ETIQUETA_REPARTO_PROPIO : tipo;
    }

    /// <summary>
    /// NestoAPI#841 (Carlos 09/11/26): quién mantiene los festivos. Administración, Dirección e Informática, todos los tipos;
    /// Almacén, solo los días sin reparto propio (alta y baja de ese tipo; no hay grupo de Logística en el dominio). La API aplica la misma regla (403).
    /// </summary>
    public static class PermisosFestivos
    {
        public static bool PuedeMantenerTodos(IConfiguracion configuracion)
            => configuracion != null &&
               (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) ||
                configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) ||
                configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        /// <summary>Almacén sin ser de los de arriba: solo el tipo RepartoPropio.</summary>
        public static bool PuedeMantenerSoloRepartoPropio(IConfiguracion configuracion)
            => configuracion != null && !PuedeMantenerTodos(configuracion) &&
               configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN);

        /// <summary>Ve la pantalla de Festivos (con todos los tipos o solo el del reparto propio).</summary>
        public static bool PuedeEntrar(IConfiguracion configuracion)
            => PuedeMantenerTodos(configuracion) || PuedeMantenerSoloRepartoPropio(configuracion);
    }

    /// <summary>NestoAPI#841: una opción del desplegable de tipos: lo que va a la API y lo que se enseña.</summary>
    public class OpcionTipoFestivo
    {
        public OpcionTipoFestivo(string valor, string etiqueta)
        {
            Valor = valor;
            Etiqueta = etiqueta;
        }

        public string Valor { get; }
        public string Etiqueta { get; }

        public override string ToString() => Etiqueta;
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
