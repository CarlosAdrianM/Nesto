using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4, región propia): las regiones de un ámbito, por nombre, sin Prism. Hace lo que hacía el
    /// <c>RegionManager</c> de Prism para Nesto: el del shell tiene MainRegion (y las regiones que declaran dentro las
    /// vistas que no abren ámbito propio, como RapportDetailRegion); cada maestro-detalle (PedidoVenta, PedidoCompra)
    /// tiene el suyo, con sus regiones de lista y detalle, que se llaman igual en todas las pestañas. Los ámbitos no se
    /// ven entre sí, igual que <c>IRegionManager.CreateRegionManager()</c>.
    ///
    /// Cada vista que entra en una región lleva colgado el gestor de su ámbito (<see cref="GestorProperty"/>, como
    /// <c>RegionManager.RegionManagerProperty</c>), para que las regiones de dentro se registren en él. Sin cablear en
    /// este tramo: todavía no hay propiedad adjunta en el XAML que llame a <see cref="Registrar"/>.
    /// </summary>
    public sealed class GestorRegiones
    {
        private readonly Dictionary<string, RegionNesto> _regiones = new Dictionary<string, RegionNesto>(StringComparer.Ordinal);

        /// <summary>El gestor de regiones del ámbito de la vista; null si la vista no está en ninguna región.</summary>
        public static readonly DependencyProperty GestorProperty = DependencyProperty.RegisterAttached(
            "Gestor", typeof(GestorRegiones), typeof(GestorRegiones), new PropertyMetadata(null));

        public static GestorRegiones GetGestor(DependencyObject elemento)
            => (GestorRegiones)(elemento ?? throw new ArgumentNullException(nameof(elemento))).GetValue(GestorProperty);

        public static void SetGestor(DependencyObject elemento, GestorRegiones gestor)
            => (elemento ?? throw new ArgumentNullException(nameof(elemento))).SetValue(GestorProperty, gestor);

        /// <summary>
        /// Crea la región del control con el adaptador que le toca (Selector → <see cref="RegionPestanas"/>,
        /// ContentControl → <see cref="RegionContenido"/>) y la registra con ese nombre. Como Prism, lanza si ya hay
        /// una región con ese nombre en el ámbito o si el control no tiene adaptador.
        /// </summary>
        public RegionNesto Registrar(string nombre, DependencyObject control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new ArgumentException("La región necesita un nombre", nameof(nombre));
            }
            if (_regiones.ContainsKey(nombre))
            {
                throw new ArgumentException($"Ya hay una región llamada {nombre} en este ámbito", nameof(nombre));
            }
            RegionNesto region = control switch
            {
                Selector selector => new RegionPestanas(nombre, selector, this),
                ContentControl contenido => new RegionContenido(nombre, contenido, this),
                _ => throw new NotSupportedException($"No hay región para un {control.GetType().Name} ({nombre})")
            };
            _regiones.Add(nombre, region);
            return region;
        }

        /// <summary>Quita la región del ámbito (sus vistas se quedan donde están). Si no existe, no hace nada.</summary>
        public bool QuitarRegion(string nombre) => nombre != null && _regiones.Remove(nombre);

        public bool Contiene(string nombre) => nombre != null && _regiones.ContainsKey(nombre);

        /// <summary>La región con ese nombre en este ámbito; null si no existe.</summary>
        public RegionNesto Region(string nombre)
            => nombre != null && _regiones.TryGetValue(nombre, out RegionNesto region) ? region : null;

        /// <summary>Un ámbito nuevo, vacío e independiente de este (el de una pestaña de maestro-detalle).</summary>
        public GestorRegiones CrearAmbito() => new GestorRegiones();
    }
}
