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
