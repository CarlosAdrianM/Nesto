using System;

namespace Nesto.Modulos.Cliente.Models
{
    // Nesto#523 (NestoAPI#623, corte 2): el contrato del origen del cliente (campañas).

    /// <summary>Una fila del catálogo de orígenes (<c>GET api/Clientes/Origenes</c>, solo los activos).</summary>
    public class OrigenClienteModel
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public string Campana { get; set; }
        public bool Activo { get; set; }
        public int? HorasParaLlamar { get; set; }

        /// <summary>Lo que se ve en el desplegable: «Lead de Meta · campaña Harmonia» o «(ninguno)».</summary>
        public string Nombre => string.IsNullOrWhiteSpace(Codigo)
            ? OrigenClienteTexto.NINGUNO
            : OrigenClienteTexto.DescripcionConCampana(Descripcion, Campana, Codigo);

        public override string ToString() => Nombre;
    }

    /// <summary>El origen de un cliente (<c>GET api/Clientes/{empresa}/{cliente}/Origen</c>).</summary>
    public class OrigenDeClienteModel
    {
        public string Origen { get; set; }
        public string Descripcion { get; set; }
        public DateTime? FechaOrigen { get; set; }
        public string UsuarioOrigen { get; set; }
    }

    /// <summary>Cuerpo del <c>PUT api/Clientes/{empresa}/{cliente}/Origen</c>: <c>Origen</c> null lo quita.</summary>
    public class GuardarOrigenClienteModel
    {
        public string Origen { get; set; }
    }

    /// <summary>Nesto#523: los textos del origen, iguales en «Crear cliente» y en la ficha.</summary>
    public static class OrigenClienteTexto
    {
        public const string NINGUNO = "(ninguno)";

        public static string DescripcionConCampana(string descripcion, string campana, string codigo)
        {
            string texto = string.IsNullOrWhiteSpace(descripcion) ? codigo?.Trim() : descripcion.Trim();
            return string.IsNullOrWhiteSpace(campana) ? texto : $"{texto} · campaña {campana.Trim()}";
        }

        /// <summary>«NUEVAVISION\Paloma» → «Paloma».</summary>
        public static string UsuarioSinDominio(string usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario))
            {
                return null;
            }
            string u = usuario.Trim();
            int barra = u.LastIndexOf('\\');
            return barra >= 0 ? u.Substring(barra + 1) : u;
        }
    }
}
