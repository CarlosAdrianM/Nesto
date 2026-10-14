using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Media;

namespace Nesto.Infrastructure.Cinta
{
    /// <summary>
    /// Nesto#490 (4C.4, la cinta): la fusión de los menús de los módulos con la cinta principal, sin Prism. Es el port de
    /// <c>MergingItemsControlRegionAdapter</c> y <c>RibbonRegionAdapter</c> del paquete PrismRibbonRegionAdapter (0.22.0),
    /// con la misma lógica:
    /// <list type="bullet">
    /// <item>Los elementos se emparejan por <see cref="ClaveFusionProperty"/>; si no la tienen, por la cabecera (texto) o
    /// por el nombre. Si coinciden, se fusionan sus hijos (una pestaña «Clientes» de dos módulos queda en una sola, y
    /// dentro, dos grupos del mismo título se juntan también); si no, el elemento se mueve tal cual.</item>
    /// <item>Al moverlo se conserva su DataContext (el suyo o, si no tiene, el de su padre), que es el que da los comandos
    /// de los botones.</item>
    /// <item>Se inserta por <see cref="OrdenFusionProperty"/>: detrás de los que tienen un orden menor o igual. Como en
    /// Nesto nadie lo pone, queda en el orden en que se cargan los módulos.</item>
    /// <item>El menú de aplicación, la barra de acceso rápido y las pestañas contextuales del módulo pasan a los de la
    /// cinta principal (que se crean si no existen).</item>
    /// </list>
    /// Lo único que no se porta es quitar el menú de un módulo (el adaptador lo hacía al quitar la vista de la región):
    /// Nesto nunca lo hace.
    /// </summary>
    public static class FusionCinta
    {
        #region Propiedades adjuntas

        /// <summary>Clave con la que se emparejan dos elementos al fusionar (MergeKey en el adaptador).</summary>
        public static readonly DependencyProperty ClaveFusionProperty = DependencyProperty.RegisterAttached("ClaveFusion",
            typeof(string), typeof(FusionCinta), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static void SetClaveFusion(UIElement elemento, string valor) => elemento.SetValue(ClaveFusionProperty, valor);

        public static string GetClaveFusion(UIElement elemento) => (string)elemento.GetValue(ClaveFusionProperty);

        /// <summary>El orden de un elemento que no lo tiene puesto (DefaultMergeOrder en el adaptador).</summary>
        public const double ORDEN_POR_DEFECTO = 10000d;

        /// <summary>Orden en que se inserta un elemento al fusionar (MergeOrder en el adaptador).</summary>
        public static readonly DependencyProperty OrdenFusionProperty = DependencyProperty.RegisterAttached("OrdenFusion",
            typeof(double), typeof(FusionCinta), new FrameworkPropertyMetadata(ORDEN_POR_DEFECTO, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static void SetOrdenFusion(UIElement elemento, double valor) => elemento.SetValue(OrdenFusionProperty, valor);

        public static double GetOrdenFusion(UIElement elemento) => (double)elemento.GetValue(OrdenFusionProperty);

        #endregion

        /// <summary>
        /// Fusiona el menú de un módulo con la cinta principal. El menú es un UserControl cuyo contenido es una
        /// <see cref="Ribbon"/> (como todos los de Nesto) o directamente un ItemsControl.
        /// </summary>
        public static void Fusionar(UIElement menu, Ribbon cintaPrincipal)
        {
            if (cintaPrincipal == null)
            {
                throw new ArgumentNullException(nameof(cintaPrincipal));
            }
            if (menu == null)
            {
                return;
            }
            if (menu is ItemsControl itemsControl)
            {
                FusionarItemsControl(itemsControl, cintaPrincipal);
            }
            else
            {
                FusionarCinta(ObtenerCinta(menu), cintaPrincipal);
            }
        }

        private static Ribbon ObtenerCinta(UIElement elemento)
        {
            if (elemento is UserControl control)
            {
                if (control.Content is Ribbon cinta)
                {
                    return cinta;
                }
                throw new NotSupportedException(string.Format("UserControl.Content of {0} is not a ribbon", elemento.GetType().FullName));
            }
            throw new NotSupportedException(string.Format("Cannot merge view of type {0} with a ribbon", elemento.GetType().FullName));
        }

        private static void FusionarCinta(Ribbon cintaModulo, Ribbon cintaPrincipal)
        {
            FusionarMenuAplicacion(cintaModulo, cintaPrincipal);
            FusionarBarraAccesoRapido(cintaModulo, cintaPrincipal);
            FusionarPestanasContextuales(cintaModulo, cintaPrincipal);
            FusionarItemsControl(cintaModulo, cintaPrincipal);
        }

        private static void FusionarMenuAplicacion(Ribbon cintaModulo, Ribbon cintaPrincipal)
        {
            if (cintaModulo.ApplicationMenu != null)
            {
                if (cintaPrincipal.ApplicationMenu == null)
                {
                    cintaPrincipal.ApplicationMenu = new RibbonApplicationMenu();
                }
                FusionarItemsControl(cintaModulo.ApplicationMenu, cintaPrincipal.ApplicationMenu);
            }
        }

        private static void FusionarBarraAccesoRapido(Ribbon cintaModulo, Ribbon cintaPrincipal)
        {
            if (cintaModulo.QuickAccessToolBar != null)
            {
                if (cintaPrincipal.QuickAccessToolBar == null)
                {
                    cintaPrincipal.QuickAccessToolBar = new RibbonQuickAccessToolBar();
                }
                FusionarItemsControl(cintaModulo.QuickAccessToolBar, cintaPrincipal.QuickAccessToolBar);
            }
        }

        private static void FusionarPestanasContextuales(Ribbon cintaModulo, Ribbon cintaPrincipal)
        {
            // Copia de la lista: al desconectar un grupo puede salir de la colección del módulo
            foreach (RibbonContextualTabGroup grupo in cintaModulo.ContextualTabGroups.ToList())
            {
                if (!cintaPrincipal.ContextualTabGroups.Any(g => Coinciden(g, grupo)))
                {
                    Desconectar(grupo);
                    if (grupo.DataContext == null)
                    {
                        grupo.DataContext = cintaModulo.DataContext;
                    }
                    InsertarOrdenado(grupo, cintaPrincipal.ContextualTabGroups);
                }
            }
        }

        private static void FusionarItemsControl(ItemsControl origen, ItemsControl destino)
        {
            foreach (UIElement elemento in origen.Items.Cast<UIElement>().ToArray())
            {
                if (elemento is ItemsControl itemsControl)
                {
                    ItemsControl existente = destino.Items
                        .OfType<ItemsControl>()
                        .FirstOrDefault(d => Coinciden(d, elemento));
                    if (existente == null)
                    {
                        InsertarOrdenado(elemento, destino.Items);
                    }
                    else
                    {
                        FusionarItemsControl(itemsControl, existente);
                    }
                }
                else
                {
                    InsertarOrdenado(elemento, destino.Items);
                }
            }
        }

        private static bool Coinciden(UIElement elemento1, UIElement elemento2)
            => ClaveFusion(elemento1).Equals(ClaveFusion(elemento2));

        /// <summary>
        /// La clave de fusión del elemento; si no la tiene, su cabecera (si es texto), su nombre o, si tampoco, una
        /// clave única (no coincide con nada). Se le queda puesta.
        /// </summary>
        internal static string ClaveFusion(UIElement elemento)
        {
            string clave = GetClaveFusion(elemento);
            if (string.IsNullOrEmpty(clave))
            {
                if (elemento is HeaderedItemsControl conCabecera)
                {
                    clave = conCabecera.Header as string;
                }
                if (string.IsNullOrEmpty(clave) && elemento is FrameworkElement frameworkElement)
                {
                    clave = frameworkElement.Name;
                }
                if (string.IsNullOrEmpty(clave))
                {
                    clave = Guid.NewGuid().ToString();
                }
                SetClaveFusion(elemento, clave);
            }
            return clave;
        }

        /// <summary>Inserta el elemento detrás de todos los que tienen un orden menor o igual que el suyo.</summary>
        internal static void InsertarOrdenado(UIElement elemento, IList coleccion)
        {
            Desconectar(elemento);
            double orden = GetOrdenFusion(elemento);
            if (Math.Abs(orden - ORDEN_POR_DEFECTO) < 0.001)
            {
                SetOrdenFusion(elemento, ORDEN_POR_DEFECTO);
            }

            int posicion = 0;
            foreach (UIElement actual in coleccion)
            {
                if (GetOrdenFusion(actual) > orden)
                {
                    break;
                }
                posicion++;
            }
            Desconectar(elemento);
            coleccion.Insert(posicion, elemento);
        }

        /// <summary>
        /// Saca el elemento de su padre (lógico o, si no tiene, visual). Si <paramref name="conservarDataContext"/>, se le
        /// deja puesto el DataContext que tenía (el suyo o, si no tenía, el de su padre).
        /// </summary>
        internal static void Desconectar(UIElement hijo, bool conservarDataContext = true)
        {
            DependencyObject padre = (hijo as FrameworkElement)?.Parent;
            if (padre == null)
            {
                padre = VisualTreeHelper.GetParent(hijo);
            }
            if (padre == null)
            {
                return;
            }

            object dataContext = null;
            if (conservarDataContext && hijo is FrameworkElement hijoFramework)
            {
                dataContext = hijoFramework.DataContext;
                if (dataContext == null && padre is FrameworkElement padreFramework)
                {
                    dataContext = padreFramework.DataContext;
                }
            }

            try
            {
                switch (padre)
                {
                    case ContentPresenter presentador:
                        presentador.Content = null;
                        break;
                    case Panel panel:
                        panel.Children.Remove(hijo);
                        break;
                    case ContentControl contentControl:
                        contentControl.Content = null;
                        break;
                    case Decorator decorador:
                        decorador.Child = null;
                        break;
                    case ItemsControl itemsControl:
                        itemsControl.Items.Remove(hijo);
                        break;
                }
            }
            finally
            {
                if (conservarDataContext && dataContext != null)
                {
                    ((FrameworkElement)hijo).DataContext = dataContext;
                }
            }
        }
    }
}
