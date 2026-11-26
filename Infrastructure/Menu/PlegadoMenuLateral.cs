using System;
using System.ComponentModel;

namespace Nesto.Infrastructure.Menu
{
    /// <summary>
    /// Nesto#655 (paso 0, Carlos 25/11/26): el menú lateral se pliega solo. Mucha gente trabaja con el panel desplegado y es
    /// un desperdicio de pantalla: abierto y sin la chincheta, en cuanto se trabaja FUERA de él (un clic o una tecla en la
    /// pantalla de la pestaña) vuelve a compacto. No se pliega mientras el ratón está encima ni mientras se escribe en su
    /// buscador. Con la chincheta se deja fijo, y eso se recuerda por usuario en <c>NavegacionLateralFija</c> ("1" fijo;
    /// cualquier otro valor o sin fila, se pliega solo; necesita la fila «(defecto)» = "0" para poder guardarse).
    /// <para>Sin temporizadores: lo dispara lo que hace el usuario (ratón y teclado), no el paso del tiempo.</para>
    /// <para>Plegarse solo no cambia lo que se recuerda de abierto/compacto (<c>NavegacionLateralCompacta</c>): eso sigue
    /// siendo lo que el usuario hace con el botón del panel.</para>
    /// </summary>
    public sealed class PlegadoMenuLateral : INotifyPropertyChanged
    {
        public const string VALOR_FIJA = "1";
        public const string VALOR_NO_FIJA = "0";

        public const string TEXTO_FIJAR = "Dejar el menú fijo: si no, se pliega solo en cuanto trabaja en una pantalla.";
        public const string TEXTO_SOLTAR = "El menú está fijo y no se pliega solo. Pulse para que se pliegue solo al trabajar en una pantalla.";

        private readonly Func<string> _leerFija;
        private readonly Action<string> _guardarFija;
        private readonly Action<Exception, string> _registrarError;
        private bool? _fija;

        /// <param name="leerFija">Lee <c>NavegacionLateralFija</c> (si es null o no se puede leer, no está fija).</param>
        /// <param name="guardarFija">Guarda <c>NavegacionLateralFija</c> ("1" fija, "0" se pliega sola).</param>
        /// <param name="registrarError">Error y contexto de un fallo no fatal (en Nesto, a ELMAH).</param>
        public PlegadoMenuLateral(Func<string> leerFija = null, Action<string> guardarFija = null, Action<Exception, string> registrarError = null)
        {
            _leerFija = leerFija;
            _guardarFija = guardarFija;
            _registrarError = registrarError;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// La chincheta: con true el panel no se pliega solo. Se lee la primera vez que se pregunta; al cambiarla se guarda
        /// (solo si cambia, y sin romper nada si no se puede guardar).
        /// </summary>
        public bool Fija
        {
            get
            {
                if (!_fija.HasValue)
                {
                    _fija = LeerFija();
                }
                return _fija.Value;
            }
            set
            {
                if (Fija == value)
                {
                    return;
                }
                _fija = value;
                try
                {
                    _guardarFija?.Invoke(value ? VALOR_FIJA : VALOR_NO_FIJA);
                }
                catch (Exception ex)
                {
                    Registrar(ex, "NavegacionLateral: guardar NavegacionLateralFija");
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fija)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextoChincheta)));
            }
        }

        /// <summary>El ToolTip de la chincheta, de usted.</summary>
        public string TextoChincheta => Fija ? TEXTO_SOLTAR : TEXTO_FIJAR;

        /// <summary>
        /// PURA (con la chincheta). Si el panel debe plegarse porque el usuario acaba de trabajar fuera de él: abierto, sin la
        /// chincheta, sin el ratón encima y sin estar escribiendo en su buscador.
        /// </summary>
        public bool DebePlegarse(bool abierto, bool ratonEncima, bool escribiendoEnBusqueda)
            => abierto && !Fija && !ratonEncima && !escribiendoEnBusqueda;

        private bool LeerFija()
        {
            try
            {
                return string.Equals(_leerFija?.Invoke()?.Trim(), VALOR_FIJA, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                Registrar(ex, "NavegacionLateral: leer NavegacionLateralFija");
                return false; // sin parámetro legible, se pliega solo
            }
        }

        private void Registrar(Exception error, string contexto)
        {
            try
            {
                _registrarError?.Invoke(error, contexto);
            }
            catch
            {
                // El registro de errores nunca rompe el panel
            }
        }
    }
}
