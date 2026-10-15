using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Nesto#526 (NestoAPI#634): una diferencia entre lo enviado y lo recibido en una reposición, tal y como la devuelve
    /// GET api/Reposiciones/Diferencias. Lo que se calcula (QuePaso, QueComprobar, ResolucionesPosibles…) lo hace la API.
    /// </summary>
    public class DiferenciaReposicion
    {
        public const string ESTADO_PENDIENTE = "Pendiente";
        public const string ESTADO_RESUELTA = "Resuelta";
        public const string ESTADO_TODAS = "Todas";

        public const string RESOLUCION_CONFIRMADA = "Confirmada";
        public const string RESOLUCION_NO_ESTABA = "NoEstaba";
        public const string RESOLUCION_SOBRABA = "Sobraba";
        public const string RESOLUCION_ERROR_RECEPCION = "ErrorRecepcion";

        public const string AJUSTE_HECHO = "Hecho";
        public const string AJUSTE_APAGADO = "Apagado";
        public const string AJUSTE_FALLIDO = "Fallido";

        public int Id { get; set; }
        public string Empresa { get; set; }
        public int Traspaso { get; set; }
        /// <summary>Donde se comprueba (de donde salió).</summary>
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Producto { get; set; }
        public string Descripcion { get; set; }
        public int Enviado { get; set; }
        public int Leido { get; set; }
        /// <summary>Leído menos enviado: negativo falta, positivo sobra.</summary>
        public int Diferencia { get; set; }
        /// <summary>«Falta» o «Sobra».</summary>
        public string Tipo { get; set; }
        public string QuePaso { get; set; }
        public string QueComprobar { get; set; }
        public string HuecoSalida { get; set; }
        /// <summary>Los huecos con stock libre del producto ahora en el origen, separados por comas.</summary>
        public string Huecos { get; set; }
        public DateTime FechaRecepcion { get; set; }
        public string UsuarioRecepcion { get; set; }
        public string Preparador { get; set; }

        /// <summary>Null (por hacer), «Hecho», «Apagado» o «Fallido».</summary>
        public string Ajuste { get; set; }
        public string AjusteMensaje { get; set; }
        public int? NumOrdenAjuste { get; set; }

        /// <summary>«Pendiente» o «Resuelta».</summary>
        public string Estado { get; set; }
        public string Resolucion { get; set; }
        public string UsuarioResolucion { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public string Comentario { get; set; }
        public int? NumOrdenResolucion { get; set; }
        public int? NumOrdenResolucionDestino { get; set; }

        /// <summary>Las resoluciones que se pueden elegir (vacía si ya está resuelta).</summary>
        public List<string> ResolucionesPosibles { get; set; } = new List<string>();
        /// <summary>El origen lleva control de ubicaciones (Algete): solo la resuelven Almacén y Dirección.</summary>
        public bool OrigenConControlUbicaciones { get; set; }
    }

    /// <summary>Nesto#526: el cuerpo de POST api/Reposiciones/Diferencias/{id}/Resolver.</summary>
    public class ResolverDiferenciaReposicion
    {
        public string Resolucion { get; set; }
        public string Comentario { get; set; }
    }

    /// <summary>Nesto#526: lo que contesta la API al resolver: la diferencia como queda y qué ha pasado con el stock.</summary>
    public class ResultadoResolverDiferenciaReposicion
    {
        public DiferenciaReposicion Diferencia { get; set; }
        public string Mensaje { get; set; }
    }

    /// <summary>Nesto#526: GET api/Reposiciones/Diferencias/Contador.</summary>
    public class ContadorDiferenciasReposicion
    {
        public string Almacen { get; set; }
        public int Pendientes { get; set; }
    }

    /// <summary>La API no ha leído o resuelto la diferencia: el mensaje es el suyo (400, 403, 404, 409…).</summary>
    public class DiferenciasReposicionException : Exception
    {
        public DiferenciasReposicionException(string motivo, int codigo) : base(motivo)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP con el que ha contestado la API.</summary>
        public int Codigo { get; }
    }
}
