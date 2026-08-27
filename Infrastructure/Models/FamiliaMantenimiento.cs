using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Una familia en la pantalla de mantenimiento (NestoAPI#406). Lo único editable es
    /// <see cref="PublicoIgualQueProfesional"/>: el resto se muestra para poder identificarla.
    /// Los porcentajes de comisión NO viajan a propósito, para que no se puedan tocar desde aquí.
    /// </summary>
    public class FamiliaMantenimiento : INotifyPropertyChanged
    {
        public string Empresa { get; set; }
        public string Numero { get; set; }
        public string Descripcion { get; set; }
        public short Estado { get; set; }

        private bool _publicoIgualQueProfesional;
        /// <summary>
        /// Esta familia se vende al público al MISMO precio que al profesional (sin el descuento
        /// del 30 %). Marcarla o desmarcarla cambia el precio de la web de todos sus productos.
        /// </summary>
        public bool PublicoIgualQueProfesional
        {
            get => _publicoIgualQueProfesional;
            set
            {
                if (_publicoIgualQueProfesional == value)
                {
                    return;
                }
                _publicoIgualQueProfesional = value;
                OnPropertyChanged(nameof(PublicoIgualQueProfesional));
                Modificada = true;
            }
        }

        /// <summary>
        /// Marcada por el usuario en esta sesión. Solo se envían al servidor las que cambian, para
        /// no republicar el catálogo entero cada vez que alguien abre la pantalla y pulsa Guardar.
        /// </summary>
        [JsonIgnore]
        public bool Modificada { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string nombre = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}
