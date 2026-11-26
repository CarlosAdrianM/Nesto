using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    // NestoAPI#876 (corte nuevo, Carlos 26/11/26): el contrato de api/Clientes/CambioFiscal (Previa, Copiar y PasarPedidos).

    /// <summary>Un bloque de lo que se copia del cliente anterior, con lo que tiene cada uno y lo que cambiaría (o ha cambiado).</summary>
    public class BloqueCambioFiscalModel
    {
        public string Bloque { get; set; }
        public string Nombre { get; set; }
        public List<string> Anterior { get; set; } = new List<string>();
        public List<string> Nuevo { get; set; } = new List<string>();
        public List<string> Cambios { get; set; } = new List<string>();
        public List<string> Avisos { get; set; } = new List<string>();
        public bool HayCambios { get; set; }
    }

    /// <summary>Un pedido pendiente del cliente anterior.</summary>
    public class PedidoPendienteCambioFiscalModel
    {
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string Contacto { get; set; }
        public DateTime? Fecha { get; set; }
        public decimal Importe { get; set; }
        public decimal Total { get; set; }
        public int Lineas { get; set; }
        public bool TienePicking { get; set; }
        public List<int> Pickings { get; set; } = new List<int>();
        public bool NotaEntrega { get; set; }
        public bool SePuedePasar { get; set; }
        public string Motivo { get; set; }
    }

    /// <summary>GET api/Clientes/CambioFiscal/Previa y POST …/Copiar.</summary>
    public class CopiaCambioFiscalModel
    {
        public bool Ensayo { get; set; }
        public string Empresa { get; set; }
        public string ClienteAnterior { get; set; }
        public string NombreAnterior { get; set; }
        public string ClienteNuevo { get; set; }
        public string NombreNuevo { get; set; }
        public bool Enlazados { get; set; }
        public bool SeEnlazaAlCopiar { get; set; }
        public string MotivoNoSePuedeCopiar { get; set; }
        public List<BloqueCambioFiscalModel> Bloques { get; set; } = new List<BloqueCambioFiscalModel>();
        public bool HayCambios { get; set; }
        public List<PedidoPendienteCambioFiscalModel> PedidosPendientes { get; set; }
        public List<string> Avisos { get; set; } = new List<string>();
        public string Mensaje { get; set; }
    }

    /// <summary>Cuerpo de POST api/Clientes/CambioFiscal/Copiar.</summary>
    public class CopiaCambioFiscalPeticion
    {
        public string Empresa { get; set; }
        public string ClienteAnterior { get; set; }
        public string ClienteNuevo { get; set; }
        /// <summary>Null o vacío = todos (OJO: vacío NO es «ninguno»).</summary>
        public List<string> Bloques { get; set; }
        public string Usuario { get; set; }
    }

    /// <summary>Cuerpo de POST api/Clientes/CambioFiscal/PasarPedidos.</summary>
    public class PasarPedidosCambioFiscalPeticion
    {
        public string Empresa { get; set; }
        public string ClienteAnterior { get; set; }
        public string ClienteNuevo { get; set; }
        public List<int> Pedidos { get; set; } = new List<int>();
        public bool CreadoSinPasarValidacion { get; set; }
        public string Usuario { get; set; }
    }

    /// <summary>Cómo ha ido (o iría, en el ensayo) un pedido.</summary>
    public class PedidoPasadoCambioFiscalModel
    {
        public int Pedido { get; set; }
        public bool Pasado { get; set; }
        public bool TeniaPicking { get; set; }
        public bool SinCambiosDeImportes { get; set; }
        public string Codigo { get; set; }
        public string Mensaje { get; set; }
        public List<string> Cambios { get; set; } = new List<string>();
        public List<string> Avisos { get; set; } = new List<string>();
    }

    /// <summary>Respuesta de POST api/Clientes/CambioFiscal/PasarPedidos.</summary>
    public class PasarPedidosCambioFiscalModel
    {
        public bool Ensayo { get; set; }
        public string ClienteAnterior { get; set; }
        public string ClienteNuevo { get; set; }
        public List<PedidoPasadoCambioFiscalModel> Pedidos { get; set; } = new List<PedidoPasadoCambioFiscalModel>();
        public int Pasados { get; set; }
        public int Rechazados { get; set; }
    }
}
