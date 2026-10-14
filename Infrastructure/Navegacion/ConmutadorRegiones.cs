using Nesto.Infrastructure.Contracts;
using System;
using System.Diagnostics;
using System.Windows;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4, tramo 3): piloto de la región propia, como lo fue <c>ServicioDialogosConmutable</c> con la ventana de
    /// diálogos (4C.2). Con el parámetro de usuario <c>RegionesPropias</c> = "1", Nesto navega con
    /// <see cref="ServicioNavegacionNesto"/> sobre el <see cref="GestorRegiones"/> del shell, y las regiones del XAML
    /// (<see cref="Regiones.NombreProperty"/>) se registran en él. Sin el parámetro, con otro valor o si no se puede leer,
    /// todo va por Prism (<see cref="ServicioNavegacionPrism"/>) como siempre.
    ///
    /// Se decide una vez, la primera vez que se pregunta (en <c>Application.CreateShell</c>, antes de crear la ventana
    /// principal), y vale hasta cerrar Nesto: el XAML del shell y la navegación tienen que ir por el mismo lado. Cuando la
    /// región propia esté validada se quita esta clase y se registra directamente.
    /// </summary>
    public sealed class ConmutadorRegiones
    {
        public const string VALOR_ACTIVADO = "1";

        private readonly Lazy<bool> _propias;
        private readonly Action<Exception, string> _registrarError;

        /// <param name="leerParametro">Lee el parámetro <c>RegionesPropias</c> del usuario.</param>
        /// <param name="registrarError">Error y contexto de un fallo no fatal de la región propia (en Nesto, a ELMAH).</param>
        public ConmutadorRegiones(Func<string> leerParametro, Action<Exception, string> registrarError = null)
        {
            if (leerParametro == null)
            {
                throw new ArgumentNullException(nameof(leerParametro));
            }
            _registrarError = registrarError;
            _propias = new Lazy<bool>(() =>
            {
                try
                {
                    return string.Equals(leerParametro()?.Trim(), VALOR_ACTIVADO, StringComparison.Ordinal);
                }
                catch
                {
                    return false; // sin parámetro legible, lo de siempre
                }
            });
        }

        /// <summary>true si este usuario va con la región propia (se lee el parámetro la primera vez).</summary>
        public bool RegionesPropias => _propias.Value;

        /// <summary>Las regiones del shell (MainRegion y las de las vistas que no abren ámbito propio) con la región propia.</summary>
        public GestorRegiones GestorShell { get; } = new GestorRegiones();

        /// <summary>
        /// Antes de crear la ventana principal: decide y enciende (o deja apagada) la propiedad adjunta
        /// <see cref="Regiones.NombreProperty"/>, que tiene que saberlo cuando se lee el XAML.
        /// </summary>
        public void AntesDeCrearShell()
        {
            Regiones.Propias = RegionesPropias;
            if (RegionesPropias)
            {
                Regiones.AlFallarRegistro = (region, error) => Registrar(error, $"Regiones: registro de la región {region}");
            }
        }

        /// <summary>Con la región propia, cuelga el gestor del shell de la ventana principal (como hace Prism con el suyo).</summary>
        public void AlCrearShell(DependencyObject shell)
        {
            if (shell == null)
            {
                throw new ArgumentNullException(nameof(shell));
            }
            if (RegionesPropias)
            {
                GestorRegiones.SetGestor(shell, GestorShell);
            }
        }

        /// <summary>
        /// La navegación de la aplicación: la propia (con las vistas creadas por su nombre y las navegaciones fallidas a la
        /// traza y a <c>registrarError</c>) o la de Prism.
        /// </summary>
        public IServicioNavegacion CrearNavegacion(Func<IServicioNavegacion> crearPrism, Func<string, object> crearVista)
        {
            if (crearPrism == null)
            {
                throw new ArgumentNullException(nameof(crearPrism));
            }
            if (!RegionesPropias)
            {
                return crearPrism();
            }
            return new ServicioNavegacionNesto(GestorShell, crearVista, (region, destino, error) =>
            {
                Debug.WriteLine($"[ServicioNavegacionNesto] Falló la navegación a {destino} en {region}: {error}");
                Registrar(error, $"ServicioNavegacionNesto: navegación a {destino} en {region}");
            });
        }

        private void Registrar(Exception error, string contexto)
        {
            try
            {
                _registrarError?.Invoke(error, contexto);
            }
            catch
            {
                // El registro de errores nunca rompe la navegación
            }
        }
    }
}
