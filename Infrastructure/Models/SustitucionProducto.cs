using System;
using System.Globalization;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// NestoAPI#581: «Compras pide servir la 45685 en lugar de la 25539», tal y como lo contesta la API
    /// (<c>GET api/Productos/{producto}/Sustitucion</c> y <c>GET api/Productos/{producto}/Sustituciones</c>).
    /// Cuándo avisa lo decide la API; aquí solo se enseña.
    /// </summary>
    public class SustitucionProductoDTO
    {
        public const string ESTADO_VIGENTE = "Vigente";

        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Producto { get; set; }
        public string NombreProducto { get; set; }
        public string ProductoSustituto { get; set; }
        public string NombreSustituto { get; set; }
        public string Motivo { get; set; }
        public bool MientrasNoHayaStock { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaAnulacion { get; set; }
        public string UsuarioAnulacion { get; set; }
        /// <summary>Vigente, Sin efecto: hay stock, Caducada o Anulada.</summary>
        public string Estado { get; set; }
        public bool Vigente { get; set; }
        /// <summary>El texto para el usuario, el mismo en todos los clientes.</summary>
        public string Aviso { get; set; }

        public bool EstaActiva => FechaAnulacion == null;

        /// <summary>«Mientras no haya stock», «Hasta el 31/10/2026» o las dos.</summary>
        public string TextoHastaCuando
        {
            get
            {
                string hasta = FechaHasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                if (MientrasNoHayaStock && hasta != null)
                {
                    return $"Mientras no haya stock (como mucho hasta el {hasta})";
                }
                return MientrasNoHayaStock ? "Mientras no haya stock" : $"Hasta el {hasta}";
            }
        }
    }

    /// <summary>NestoAPI#581: el alta (POST api/Productos/{producto}/Sustituciones).</summary>
    public class NuevaSustitucionProductoDTO
    {
        public string Empresa { get; set; }
        public string ProductoSustituto { get; set; }
        public string Motivo { get; set; }
        public bool MientrasNoHayaStock { get; set; } = true;
        public DateTime? FechaHasta { get; set; }
    }
}
