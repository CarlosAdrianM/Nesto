using System;
using System.ComponentModel;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>
    /// Nesto#419: movimiento del extracto de cliente (ExtractoClienteDTO de NestoAPI, que ya
    /// viene con nombres en camelCase — Newtonsoft es case-insensitive). Seleccionado es
    /// estado de UI (checkbox del grid para elegir los dos movimientos a liquidar) y notifica
    /// para que el ViewModel refresque el CanExecute de Liquidar.
    /// </summary>
    public class ExtractoClienteModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public int Asiento { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; }
        public string Documento { get; set; }
        public string Efecto { get; set; }
        public string Concepto { get; set; }
        public decimal Importe { get; set; }
        public decimal ImportePendiente { get; set; }
        public string Vendedor { get; set; }
        public DateTime? Vencimiento { get; set; }
        public string Ccc { get; set; }
        public string Ruta { get; set; }
        public string Estado { get; set; }
        public string FormaPago { get; set; }

        private bool _seleccionado;
        public bool Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (_seleccionado != value)
                {
                    _seleccionado = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Seleccionado)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>Respuesta del POST ExtractosCliente/Liquidar (NestoAPI#333).</summary>
    public class ResultadoLiquidacionModel
    {
        public bool Exito { get; set; }
        public decimal ImportePdteOrigen { get; set; }
        public decimal ImportePdteDestino { get; set; }
    }
}
