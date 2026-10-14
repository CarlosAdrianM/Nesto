using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4, región propia): <see cref="IServicioNavegacion"/> sin Prism, sobre un <see cref="GestorRegiones"/>
    /// propio. Hace lo mismo que <see cref="ServicioNavegacionPrism"/> con el IRegionManager de Prism y lo que Nesto le
    /// añadía: el cargador de vistas (<see cref="CargadorVistasNavegacion"/>), la entrega de la navegación
    /// (<see cref="ReceptorNavegacionRegionBehavior"/>) y, en la región, la limpieza al quitar
    /// (<see cref="Shared.LimpiarVistaAlQuitarRegionBehavior"/>) y el aviso de activación (<see cref="IConActivacion"/>).
    /// Las pruebas del shell (<c>ShellRegionPrincipalTests</c>) se ejecutan con las dos implementaciones.
    ///
    /// Una navegación (<see cref="RequestNavigate(string, string, ParametrosNavegacion)"/>), como la de Prism:
    /// <list type="number">
    /// <item>si la región no existe, no hace nada;</item>
    /// <item>busca entre las vistas de la región una cuyo tipo se llame como el destino y que no sea un
    /// <see cref="IReceptorNavegacionPestanaNueva"/>; si no la hay, la crea por su nombre con
    /// <c>crearVista</c> (el contenedor, con los mismos registros: <c>Register&lt;object, XView&gt;("XView")</c>)
    /// y la mete en la región;</item>
    /// <item>la activa y entrega la navegación a la vista de destino (<see cref="ReceptorNavegacion.Entregar"/>).</item>
    /// </list>
    /// Como Prism, una navegación que falla (una vista que no está registrada, un constructor o un <c>AlLlegar</c> que
    /// lanza) no sube a quien navega: va a <c>alFallarNavegacion</c> (por defecto, la traza de depuración).
    /// </summary>
    public class ServicioNavegacionNesto : IServicioNavegacion
    {
        private readonly GestorRegiones _gestor;
        private readonly Func<string, object> _crearVista;
        private readonly Action<string, string, Exception> _alFallarNavegacion;

        /// <param name="gestor">Las regiones de este ámbito (el del shell, o el de una pestaña de maestro-detalle).</param>
        /// <param name="crearVista">Crea una vista por el nombre con que se registró para la navegación.</param>
        /// <param name="alFallarNavegacion">Región, destino y error de una navegación que ha fallado.</param>
        public ServicioNavegacionNesto(GestorRegiones gestor, Func<string, object> crearVista,
            Action<string, string, Exception> alFallarNavegacion = null)
        {
            _gestor = gestor ?? throw new ArgumentNullException(nameof(gestor));
            _crearVista = crearVista ?? throw new ArgumentNullException(nameof(crearVista));
            _alFallarNavegacion = alFallarNavegacion ?? ((region, destino, error) =>
                Debug.WriteLine($"[ServicioNavegacionNesto] Falló la navegación a {destino} en {region}: {error}"));
        }

        /// <summary>Las regiones de este ámbito.</summary>
        public GestorRegiones Gestor => _gestor;

        public void RequestNavigate(string regionName, string source)
            => RequestNavigate(regionName, source, null);

        public void RequestNavigate(string regionName, string source, ParametrosNavegacion parameters)
        {
            RegionNesto region = _gestor.Region(regionName);
            if (region == null)
            {
                return;
            }
            try
            {
                string contrato = ReceptorNavegacion.Contrato(source)
                    ?? throw new ArgumentException("Falta la vista a la que navegar", nameof(source));
                ParametrosNavegacion parametros = ConLosDeLaConsulta(parameters, source);
                // Como CargadorVistasNavegacion: se reutiliza la primera abierta del mismo tipo, salvo las de pestaña nueva
                object vista = ReceptorNavegacion.VistasDelContrato(region.Vistas, contrato)
                    .FirstOrDefault(v => !ReceptorNavegacion.QuierePestanaNueva(v));
                if (vista == null)
                {
                    vista = _crearVista(contrato) ?? throw new InvalidOperationException($"No se ha podido crear la vista {contrato}");
                    if (!region.Contiene(vista))
                    {
                        region.Anadir(vista);
                    }
                }
                region.Activar(vista);
                ReceptorNavegacion.Entregar(region.VistasActivas, region.Vistas, source, parametros);
            }
            catch (Exception ex)
            {
                _alFallarNavegacion(regionName, source, ex);
            }
        }

        public object VistaActiva(string regionName)
        {
            try
            {
                return _gestor.Region(regionName)?.VistasActivas.FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Como en Prism: desactivarla y luego quitarla
        public void CerrarVistaActiva(string regionName)
        {
            RegionNesto region = RegionObligatoria(regionName);
            object vista = region.VistasActivas.FirstOrDefault();
            if (vista != null)
            {
                region.Desactivar(vista);
                region.Quitar(vista);
            }
        }

        // La X de una pestaña: quitarla solo si está, sin desactivarla antes
        public void CerrarVista(string regionName, object vista)
        {
            RegionNesto region = _gestor.Region(regionName);
            if (vista != null && region != null && region.Contiene(vista))
            {
                region.Quitar(vista);
            }
        }

        public void QuitarVistas(string regionName)
        {
            RegionNesto region = _gestor.Region(regionName);
            if (region == null)
            {
                return;
            }
            foreach (object vista in region.Vistas.ToList())
            {
                region.Quitar(vista);
            }
        }

        // Nombre único (Clientes, Clientes2…), meterla, entregarle este ámbito y activarla
        public void AbrirVistaNueva(string regionName, object vista, string nombre)
        {
            RegionNesto region = RegionObligatoria(regionName);
            string unico = nombre;
            int contador = 2;
            while (region.Vista(unico) != null)
            {
                unico = nombre + contador;
                contador++;
            }
            region.Anadir(vista, unico);
            ReceptorNavegacion.EntregarAmbito(vista, this);
            region.Activar(vista);
        }

        // Maestro-detalle: la vista entra con un gestor de regiones nuevo colgado, para que las suyas no choquen
        public IServicioNavegacion AbrirVistaConAmbito(string regionName, object vista)
        {
            RegionNesto region = RegionObligatoria(regionName);
            GestorRegiones ambito = _gestor.CrearAmbito();
            region.Anadir(vista, null, ambito);
            var navegacionAmbito = new ServicioNavegacionNesto(ambito, _crearVista, _alFallarNavegacion);
            ReceptorNavegacion.EntregarAmbito(vista, navegacionAmbito);
            region.Activar(vista);
            return navegacionAmbito;
        }

        // Como Regions[nombre] de Prism: si no existe, KeyNotFoundException
        private RegionNesto RegionObligatoria(string regionName)
            => _gestor.Region(regionName) ?? throw new KeyNotFoundException($"No hay ninguna región llamada {regionName}");

        // Como NavigationContext de Prism: primero los parámetros de la consulta del destino («XView?a=1»), luego los que se pasan
        private static ParametrosNavegacion ConLosDeLaConsulta(ParametrosNavegacion parametros, string destino)
        {
            int consulta = destino?.IndexOf('?') ?? -1;
            if (consulta < 0)
            {
                return parametros ?? new ParametrosNavegacion();
            }
            var resultado = new ParametrosNavegacion();
            foreach (string par in destino.Substring(consulta + 1).Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int igual = par.IndexOf('=');
                string clave = Uri.UnescapeDataString(igual < 0 ? par : par.Substring(0, igual));
                string valor = igual < 0 ? string.Empty : Uri.UnescapeDataString(par.Substring(igual + 1));
                resultado.Add(clave, valor);
            }
            foreach (KeyValuePair<string, object> entrada in parametros ?? new ParametrosNavegacion())
            {
                resultado.Add(entrada.Key, entrada.Value);
            }
            return resultado;
        }
    }
}
