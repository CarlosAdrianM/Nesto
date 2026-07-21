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
        public decimal ImportePendiente { get; set; }
        public string Ccc { get; set; }
        public bool Preseleccionado { get; set; }
        public string Motivo { get; set; }
        public bool ClienteConNegativos { get; set; }

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
