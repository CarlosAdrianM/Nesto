using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Infrastructure.Models
{
    // NestoAPI#553: la tienda prepara la reposición que manda a Algete y la termina (api/Reposiciones).
    // Espejo de los DTO de NestoAPI/Infraestructure/Reposiciones/PreparacionReposicion.cs.

    public class LineaReposicionEnPreparacion
    {
        /// <summary>La línea en el diario de salida: es la que se cambia con PUT …/EnPreparacion/Lineas/{NumeroOrden}.</summary>
        public int NumeroOrden { get; set; }
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string CodigoBarras { get; set; }
        public int Cantidad { get; set; }
        /// <summary>Stock en la tienda (el almacén de origen).</summary>
        public int StockOrigen { get; set; }
    }

    /// <summary>GET api/Reposiciones/EnPreparacion (y lo que contestan POST y PUT).</summary>
    public class ReposicionEnPreparacion
    {
        public string Empresa { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Diario { get; set; }
        public DateTime Fecha { get; set; }
        public string Usuario { get; set; }
        public List<LineaReposicionEnPreparacion> Lineas { get; set; } = new List<LineaReposicionEnPreparacion>();
        public int Unidades => (Lineas ?? new List<LineaReposicionEnPreparacion>()).Sum(l => l.Cantidad);
    }

    public class LineaCrearReposicion
    {
        public string Producto { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>POST api/Reposiciones. Sin líneas, el servidor calcula la propuesta (el «Rellenar» de Nesto viejo).</summary>
    public class CrearReposicion
    {
        public string Empresa { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public DateTime? Fecha { get; set; }
        public List<LineaCrearReposicion> Lineas { get; set; }
        /// <summary>NestoAPI#577: quién la crea ('Nesto' | 'Ariadna' | 'Automatico'). Nesto manda siempre «Nesto».</summary>
        public string Herramienta { get; set; }
    }

    /// <summary>
    /// GET api/Reposiciones/ProximaLlegada (NestoAPI#577): la próxima reposición de una ruta según el calendario.
    /// Espejo de ProximaReposicionDTO de NestoAPI.
    /// </summary>
    public class ProximaReposicion
    {
        public string Origen { get; set; }
        public string Destino { get; set; }
        /// <summary>Día y hora de cierre: a esa hora la API rellena sola la reposición.</summary>
        public DateTime CierraEl { get; set; }
        public DateTime LlegaEl { get; set; }
        public DateTime PedidoSaleEl { get; set; }
        public int DiasHastaSalida { get; set; }
    }

    public class LineaTraspasoTerminado
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>POST api/Reposiciones/EnPreparacion/Terminar.</summary>
    public class ResultadoTerminarReposicion
    {
        public int NumTraspaso { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string DiarioSalida { get; set; }
        public string DiarioEntrada { get; set; }
        public List<LineaTraspasoTerminado> Lineas { get; set; } = new List<LineaTraspasoTerminado>();
        public int Unidades => (Lineas ?? new List<LineaTraspasoTerminado>()).Sum(l => l.Cantidad);
    }

    /// <summary>La API no ha aceptado la petición: el motivo que da (inventario en curso, stock negativo, sin permiso…).</summary>
    public class EnvioReposicionException : Exception
    {
        public EnvioReposicionException(string motivo) : base(motivo) { }

        public EnvioReposicionException(string motivo, int codigo) : base(motivo)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP con el que ha contestado la API (null si no se sabe).</summary>
        public int? Codigo { get; }

        /// <summary>NestoAPI#577: 403, el usuario no puede hacer eso (p. ej. rellenar la reposición a mano).</summary>
        public bool EsSinPermiso => Codigo == 403;
    }
}
