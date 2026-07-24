using System;
using System.ComponentModel;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// NestoAPI#332: efecto candidato a remesa (EfectoCandidatoDTO del API). Seleccionado se
    /// inicializa con la preselección del servidor y el usuario lo marca/desmarca; notifica
    /// para refrescar el importe seleccionado y el CanExecute de crear la remesa.
    /// </summary>
    public class EfectoCandidatoModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Documento { get; set; }
        public string Efecto { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime? Vencimiento { get; set; }
        public string Ccc { get; set; }
        public bool Preseleccionado { get; set; }
        public string Motivo { get; set; }

        // ImportePendiente y ClienteConNegativos notifican porque se actualizan EN SITIO cuando
        // el usuario liquida efectos en el Extracto de Cliente (EfectosLiquidadosEvent): el grid
        // debe reflejar el nuevo importe y quitar el naranja sin recargar toda la lista.
        private decimal _importePendiente;
        public decimal ImportePendiente
        {
            get => _importePendiente;
            set
            {
                if (_importePendiente != value)
                {
                    _importePendiente = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportePendiente)));
                }
            }
        }

        private bool _clienteConNegativos;
        public bool ClienteConNegativos
        {
            get => _clienteConNegativos;
            set
            {
                if (_clienteConNegativos != value)
                {
                    _clienteConNegativos = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClienteConNegativos)));
                }
            }
        }

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

    /// <summary>Respuesta del POST api/Remesas (NestoAPI#332).</summary>
    public class CrearRemesaResponseModel
    {
        public int NumeroRemesa { get; set; }
        public decimal Importe { get; set; }
        public int NumeroEfectos { get; set; }
    }
}
