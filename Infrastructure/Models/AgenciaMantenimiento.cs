using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Una agencia de transporte para la ventana de mantenimiento (Nesto#340). Numero es la PK de
    /// cada fila (agencia × empresa). El fuel se almacena como FRACCIÓN (0,1055); el usuario edita
    /// el PORCENTAJE (10,55 %). EnCuarentena y EsNueva son solo de UI (no viajan al servidor de
    /// agencias): la cuarentena se persiste en el parámetro AgenciasEnCuarentena.
    /// </summary>
    public class AgenciaMantenimiento : INotifyPropertyChanged
    {
        private int _numero;
        public int Numero { get => _numero; set => Set(ref _numero, value); }

        private string _empresa;
        public string Empresa { get => _empresa; set => Set(ref _empresa, value); }

        private string _nombre;
        public string Nombre { get => _nombre; set => Set(ref _nombre, value); }

        private string _ruta;
        public string Ruta { get => _ruta; set => Set(ref _ruta, value); }

        private string _identificador;
        public string Identificador { get => _identificador; set => Set(ref _identificador, value); }

        private string _prefijoCodigoBarras;
        public string PrefijoCodigoBarras { get => _prefijoCodigoBarras; set => Set(ref _prefijoCodigoBarras, value); }

        private string _cuentaReembolsos;
        public string CuentaReembolsos { get => _cuentaReembolsos; set => Set(ref _cuentaReembolsos, value); }

        private decimal _recargoCombustible;
        public decimal RecargoCombustible
        {
            get => _recargoCombustible;
            set
            {
                if (Set(ref _recargoCombustible, value))
                {
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

        private bool _enCuarentena;
        /// <summary>Si la agencia está en el parámetro AgenciasEnCuarentena (no se compara/usa).</summary>
        [JsonIgnore]
        public bool EnCuarentena { get => _enCuarentena; set => Set(ref _enCuarentena, value); }

        /// <summary>Alta pendiente de POST (true) vs edición de una existente (PUT).</summary>
        [JsonIgnore]
        public bool EsNueva { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Set<T>(ref T campo, T valor, [CallerMemberName] string nombre = null)
        {
            if (Equals(campo, valor))
            {
                return false;
            }
            campo = valor;
            OnPropertyChanged(nombre);
            return true;
        }

        private void OnPropertyChanged(string nombre)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}
