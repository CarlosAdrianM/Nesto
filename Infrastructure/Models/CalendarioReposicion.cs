using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// NestoAPI#577: una fila del calendario de reposiciones (GET/PUT api/Reposiciones/Calendario): una ruta (origen →
    /// destino) en un día de llegada.
    /// </summary>
    public class FilaCalendarioReposicion
    {
        /// <summary>Null al crear una fila nueva.</summary>
        public int? Id { get; set; }
        public string Empresa { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        /// <summary>1 lunes … 7 domingo: el día de LLEGADA.</summary>
        public byte DiaSemana { get; set; }
        /// <summary>A esa hora se rellena sola la reposición.</summary>
        public TimeSpan HoraCierre { get; set; }
        /// <summary>A qué hora suele entrar en el destino.</summary>
        public TimeSpan HoraLlegadaHabitual { get; set; }
        /// <summary>Cuántos laborables del origen antes del día de llegada se cierra (0 = el mismo día).</summary>
        public byte LaborablesAntelacionCierre { get; set; }
        public bool Activo { get; set; } = true;
        /// <summary>Solo lectura: quién la cambió por última vez.</summary>
        public string Usuario { get; set; }
        /// <summary>Solo lectura.</summary>
        public DateTime? FechaModificacion { get; set; }
    }

    /// <summary>
    /// NestoAPI#577: el cuerpo de PUT api/Reposiciones/Calendario. Nesto manda solo filas sueltas (las cambiadas y las
    /// nuevas, sin Origen ni Destino arriba): así la API no desactiva nada que no se haya tocado.
    /// </summary>
    public class GuardarCalendarioReposiciones
    {
        public string Empresa { get; set; }
        public List<FilaCalendarioReposicion> Filas { get; set; } = new List<FilaCalendarioReposicion>();
    }

    /// <summary>
    /// NestoAPI#867: una excepción por fecha del calendario de reposiciones (GET/POST/DELETE
    /// api/Reposiciones/Calendario/Excepciones): ese día de llegada no hay la reposición del calendario
    /// (<see cref="TiposExcepcionCalendario.SIN_REPOSICION"/>) o hay una que el calendario no tiene
    /// (<see cref="TiposExcepcionCalendario.REPOSICION_EXTRA"/>, con sus horas; sin ellas, las de la ruta).
    /// </summary>
    public class ExcepcionCalendarioReposicion
    {
        /// <summary>Solo lectura (la API busca por empresa, ruta y fecha).</summary>
        public int? Id { get; set; }
        public string Empresa { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        /// <summary>El día de llegada.</summary>
        public DateTime FechaLlegada { get; set; }
        public string Tipo { get; set; }
        public TimeSpan? HoraCierre { get; set; }
        public byte? LaborablesAntelacionCierre { get; set; }
        public TimeSpan? HoraLlegadaHabitual { get; set; }
        public string Motivo { get; set; }
        /// <summary>Solo lectura.</summary>
        public string Usuario { get; set; }
        /// <summary>Solo lectura.</summary>
        public DateTime? FechaModificacion { get; set; }
        /// <summary>Solo lectura: cuándo cierra (o cerraba) la reposición de ese día; desde entonces no se puede tocar.</summary>
        public DateTime? CierraEl { get; set; }
    }

    /// <summary>NestoAPI#867: los valores de <see cref="ExcepcionCalendarioReposicion.Tipo"/>.</summary>
    public static class TiposExcepcionCalendario
    {
        public const string SIN_REPOSICION = "SinReposicion";
        public const string REPOSICION_EXTRA = "ReposicionExtra";
    }

    /// <summary>La API no ha leído o guardado el calendario: el mensaje es el suyo (400 con el motivo, 403 sin permiso…).</summary>
    public class CalendarioReposicionesException : Exception
    {
        public CalendarioReposicionesException(string motivo, int codigo) : base(motivo)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP con el que ha contestado la API.</summary>
        public int Codigo { get; }
    }
}
