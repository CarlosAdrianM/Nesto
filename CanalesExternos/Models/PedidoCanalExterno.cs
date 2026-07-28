using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.ComponentModel;

namespace Nesto.Modulos.CanalesExternos
{
    public class PedidoCanalExterno : IFiltrableItem, INotifyPropertyChanged
    {
        public string PedidoCanalId { get; set; }
        public int PedidoNestoId { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string TelefonoFijo { get; set; }
        public string TelefonoMovil { get; set; }
        public string CorreoElectronico { get; set; }
        public string PaisISO { get; set; }
        public string Observaciones { get; set; }
        public string UltimoSeguimiento { get; set; }
        /// <summary>
        /// NestoAPI#258 slice (a): último envío tramitado con los identificadores por canal que
        /// declara la agencia en el servidor. Si está informado, ConfirmarPedido usa estos datos
        /// en vez de re-parsear UltimoSeguimiento.
        /// </summary>
        public PedidoVentaModel.EnvioAgenciaDTO UltimoEnvio { get; set; }
        public PedidoVentaDTO Pedido { get; set; }
        public string Almacen { get; set; }

        // Nesto#434: estado de la factura subida a Amazon. Con INPC (solo estas propiedades)
        // para que la columna del grid se refresque al subir sin recargar la lista.
        private string _numeroFactura;
        public string NumeroFactura
        {
            get => _numeroFactura;
            set
            {
                _numeroFactura = value;
                OnPropertyChanged(nameof(NumeroFactura));
                OnPropertyChanged(nameof(FacturaSubida));
                OnPropertyChanged(nameof(EtiquetaFactura));
            }
        }

        private string _estadoFacturaAmazon;
        /// <summary>ENVIADA (pendiente de procesar en Amazon), DONE, FATAL o CANCELLED.</summary>
        public string EstadoFacturaAmazon
        {
            get => _estadoFacturaAmazon;
            set
            {
                _estadoFacturaAmazon = value;
                OnPropertyChanged(nameof(EstadoFacturaAmazon));
                OnPropertyChanged(nameof(EtiquetaFactura));
            }
        }

        public bool FacturaSubida => !string.IsNullOrEmpty(NumeroFactura);

        /// <summary>Texto de la columna Factura del grid: número + icono del estado del feed.</summary>
        public string EtiquetaFactura
        {
            get
            {
                if (string.IsNullOrEmpty(NumeroFactura))
                {
                    // OMITIDA = cliente de factura simplificada (Amazon/tienda/público final):
                    // el servidor no la sube (NestoAPI#366).
                    return EstadoFacturaAmazon == "OMITIDA" ? "no se sube (simplificada)" : string.Empty;
                }
                string icono = EstadoFacturaAmazon switch
                {
                    "DONE" => "✔",
                    "FATAL" or "CANCELLED" => "✖",
                    _ => "⏳"
                };
                return $"{NumeroFactura} {icono}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string nombre) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));

        public bool Contains(string filtro)
        {
            return (PedidoNestoId.ToString() == filtro) || (Nombre != null && Nombre.ToLower().ToString().Contains(filtro)) ||
                (Observaciones != null && Observaciones.ToLower().ToString().Contains(filtro)) || (Direccion != null && Direccion.ToLower().ToString().Contains(filtro)) ||
                (CodigoPostal != null && CodigoPostal.ToString() == filtro) || (Poblacion != null && Poblacion.ToLower().ToString().Contains(filtro)) ||
                (TelefonoFijo != null && TelefonoFijo.ToString() == filtro) || (TelefonoMovil != null && TelefonoMovil.ToString() == filtro);
        }
    }
}
