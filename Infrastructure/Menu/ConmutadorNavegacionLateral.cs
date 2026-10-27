using System;

namespace Nesto.Infrastructure.Menu
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1): el piloto del menú lateral, como lo fue <c>ConmutadorRegiones</c> con la región
    /// propia. Con el parámetro de usuario <c>NavegacionLateral</c> = "1", la ventana principal esconde la cinta y pone el
    /// menú lateral (NavigationView de WPF-UI). Sin el parámetro, con otro valor o si no se puede leer, la cinta de siempre
    /// (y el panel ni se crea: quien no tiene el piloto no carga nada de WPF-UI).
    /// <para>
    /// Se decide una vez, la primera vez que se pregunta (al crear la ventana principal), y vale hasta cerrar Nesto: por
    /// eso, al cambiarlo desde Parámetros hay que reiniciar. Cuando el menú lateral sea el de todos (tramo 7) se quita.
    /// </para>
    /// <para>
    /// Lleva también si el panel sale compacto (solo iconos, 48 px; Carlos 27/10/26, decisión 2: por defecto) o abierto,
    /// que se recuerda por usuario en <c>NavegacionLateralCompacta</c> (necesita la fila «(defecto)» = "1": el PUT de
    /// api/ParametrosUsuario solo actualiza, y la fila del usuario la crea el GET copiando la de «(defecto)»).
    /// </para>
    /// </summary>
    public sealed class ConmutadorNavegacionLateral
    {
        public const string VALOR_ACTIVADO = "1";
        /// <summary>Valor de <c>NavegacionLateralCompacta</c> con el panel abierto (cualquier otro: compacto).</summary>
        public const string VALOR_ABIERTA = "0";
        public const string VALOR_COMPACTA = "1";

        private readonly Lazy<bool> _activa;
        private readonly Lazy<bool> _compactaAlArrancar;
        private readonly Action<string> _guardarCompacta;
        private readonly Action<Exception, string> _registrarError;
        private bool? _compactaGuardada;

        /// <param name="leerParametro">Lee el parámetro <c>NavegacionLateral</c> del usuario.</param>
        /// <param name="leerCompacta">Lee <c>NavegacionLateralCompacta</c> (si es null, compacto).</param>
        /// <param name="guardarCompacta">Guarda <c>NavegacionLateralCompacta</c> ("0" abierto, "1" compacto).</param>
        /// <param name="registrarError">Error y contexto de un fallo no fatal (en Nesto, a ELMAH).</param>
        public ConmutadorNavegacionLateral(Func<string> leerParametro, Func<string> leerCompacta = null,
            Action<string> guardarCompacta = null, Action<Exception, string> registrarError = null)
        {
            if (leerParametro == null)
            {
                throw new ArgumentNullException(nameof(leerParametro));
            }
            _guardarCompacta = guardarCompacta;
            _registrarError = registrarError;
            _activa = new Lazy<bool>(() =>
            {
                try
                {
                    return string.Equals(leerParametro()?.Trim(), VALOR_ACTIVADO, StringComparison.Ordinal);
                }
                catch
                {
                    return false; // sin parámetro legible, la cinta de siempre
                }
            });
            _compactaAlArrancar = new Lazy<bool>(() =>
            {
                try
                {
                    return !string.Equals(leerCompacta?.Invoke()?.Trim(), VALOR_ABIERTA, StringComparison.Ordinal);
                }
                catch
                {
                    return true; // sin parámetro legible, compacto
                }
            });
        }

        /// <summary>true si este usuario va con el menú lateral (se lee el parámetro la primera vez).</summary>
        public bool Activa => _activa.Value;

        /// <summary>Si el panel sale compacto al arrancar (se lee la primera vez; por defecto, sí).</summary>
        public bool CompactaAlArrancar => _compactaAlArrancar.Value;

        /// <summary>
        /// El usuario ha abierto o cerrado el panel con su botón: se guarda para el próximo arranque (solo si cambia, y
        /// sin romper nada si no se puede guardar).
        /// </summary>
        public void RecordarCompacta(bool compacta)
        {
            bool anterior = _compactaGuardada ?? CompactaAlArrancar;
            if (anterior == compacta)
            {
                return;
            }
            _compactaGuardada = compacta;
            try
            {
                _guardarCompacta?.Invoke(compacta ? VALOR_COMPACTA : VALOR_ABIERTA);
            }
            catch (Exception ex)
            {
                Registrar(ex, "NavegacionLateral: guardar NavegacionLateralCompacta");
            }
        }

        /// <summary>Un fallo no fatal del panel (a ELMAH en Nesto). Nunca lanza.</summary>
        public void Registrar(Exception error, string contexto)
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
