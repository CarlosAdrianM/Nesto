using Nesto.Infrastructure.Contracts;
using System;
using System.Diagnostics;
using System.Windows;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4, tramo 4): las regiones de la ventana principal con la región propia, ya para todos y sin Prism.
    /// Es lo que hacía el conmutador del piloto (<c>ConmutadorRegiones</c>, parámetro <c>RegionesPropias</c>) cuando
    /// estaba encendido, que lo estuvo para todos los usuarios desde el 15/10/26 sin un solo error en ELMAH:
    /// <list type="bullet">
    /// <item>el <see cref="GestorRegiones"/> del shell (MainRegion y las regiones de las vistas que no abren ámbito propio,
    /// como RapportDetailRegion), colgado de la ventana principal (<see cref="AlCrearShell"/>);</item>
    /// <item>los registros de región que fallan (<see cref="Regiones.AlFallarRegistro"/>), a la traza y a
    /// <c>registrarError</c> (en Nesto, ELMAH);</item>
    /// <item>la navegación de la aplicación (<see cref="CrearNavegacion"/>): <see cref="ServicioNavegacionNesto"/> sobre ese
    /// gestor, con las navegaciones fallidas a la traza y a <c>registrarError</c>.</item>
    /// </list>
    /// No usa ningún tipo de Prism ni del contenedor: <c>ComposicionNesto</c> la registra como singleton y la navegación sale
    /// de ella con las vistas creadas por su nombre (<see cref="IResolutorNesto.ResolverVista"/>), así que sirve igual con
    /// Prism/Unity que con Microsoft.Extensions.DependencyInjection (tramo 4 de 4C.5).
    /// </summary>
    public sealed class RegionesDelShell
    {
        private readonly Action<Exception, string> _registrarError;

        /// <param name="registrarError">Error y contexto de un fallo no fatal de la región propia (en Nesto, a ELMAH).</param>
        public RegionesDelShell(Action<Exception, string> registrarError = null)
        {
            _registrarError = registrarError;
        }

        /// <summary>Las regiones del shell (MainRegion y las de las vistas que no abren ámbito propio).</summary>
        public GestorRegiones GestorShell { get; } = new GestorRegiones();

        /// <summary>
        /// Al crear la ventana principal (antes de que se registren sus regiones): los registros que fallen van a
        /// <c>registrarError</c>, y el gestor del shell queda colgado de la ventana, para que MainRegion se registre en él.
        /// </summary>
        public void AlCrearShell(DependencyObject shell)
        {
            if (shell == null)
            {
                throw new ArgumentNullException(nameof(shell));
            }
            Regiones.AlFallarRegistro = (region, error) => Registrar(error, $"Regiones: registro de la región {region}");
            GestorRegiones.SetGestor(shell, GestorShell);
        }

        /// <summary>
        /// La navegación de la aplicación, sobre el gestor del shell, con las vistas creadas por su nombre y las
        /// navegaciones fallidas a la traza y a <c>registrarError</c>.
        /// </summary>
        public IServicioNavegacion CrearNavegacion(Func<string, object> crearVista)
        {
            if (crearVista == null)
            {
                throw new ArgumentNullException(nameof(crearVista));
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
