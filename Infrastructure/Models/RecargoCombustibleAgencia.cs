using Newtonsoft.Json;
using System.ComponentModel;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Recargo de combustible (fuel) de una agencia, editable mensualmente desde Nesto.
    /// Se almacena como FRACCIÓN (0,1055); el usuario edita el PORCENTAJE (10,55 %).
    /// </summary>
    public class RecargoCombustibleAgencia : INotifyPropertyChanged
    {
        public int Numero { get; set; }
        public string Nombre { get; set; }

        private decimal _recargoCombustible;
        public decimal RecargoCombustible
        {
            get => _recargoCombustible;
            set
            {
                if (_recargoCombustible != value)
                {
                    _recargoCombustible = value;
                    OnPropertyChanged(nameof(RecargoCombustible));
                    OnPropertyChanged(nameof(PorcentajeFuel));
                }
            }
        }

        /// <summary>El % que ve y edita el usuario (10,55), sobre la fracción almacenada (0,1055).</summary>
        [JsonIgnore]
        public decimal PorcentajeFuel
        {
            get => RecargoCombustible * 100m;
            set => RecargoCombustible = value / 100m;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string nombre)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}
