using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Menu;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Input;

namespace Nesto.Infrastructure.Cinta
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1): saca el menú de la cinta ya fusionada (después de que todos los módulos hayan
    /// puesto el suyo), sin tocar los 13 módulos. No resuelve nada del contenedor: cada elemento usa la MISMA instancia
    /// que la cinta (el mismo <c>ICommand</c> y, como <see cref="ElementoMenuBase.Fuente"/>, el mismo DataContext), porque
    /// los menús en VB llevan ViewModels completos con efectos al construirse y MS.DI los registra transitorios.
    /// <para>
    /// Texto, ayuda, icono, visibilidad y comando COPIAN los Binding del elemento de la cinta (con su DataContext como
    /// Source) o, si no son Binding, se enlazan con sus propiedades; así siguen vivos: si el ViewModel oculta un botón o
    /// cambia su texto («Diferencias (3)»), el panel lo ve. La cinta sigue existiendo (colapsada) mientras dure el piloto.
    /// </para>
    /// </summary>
    public static class LectorCinta
    {
        /// <summary>Lee la cinta y reparte lo leído por rol (<see cref="MapaMenuPorRol"/>).</summary>
        public static CatalogoMenu LeerCatalogo(Ribbon cinta)
        {
            var menuAplicacion = new List<ElementoMenu>();
            List<ElementoMenuBase> elementos = Leer(cinta, menuAplicacion);
            return CatalogoMenu.Construir(elementos, menuAplicacion);
        }

        /// <summary>
        /// Los elementos de las pestañas, en el orden de la cinta (pestaña, grupo, elemento), y, en
        /// <paramref name="menuAplicacion"/>, los del menú de aplicación. Los grupos vacíos («Ratios») no dan nada.
        /// </summary>
        public static List<ElementoMenuBase> Leer(Ribbon cinta, List<ElementoMenu> menuAplicacion = null)
        {
            if (cinta == null)
            {
                throw new ArgumentNullException(nameof(cinta));
            }

            if (menuAplicacion != null && cinta.ApplicationMenu != null)
            {
                foreach (object item in cinta.ApplicationMenu.Items)
                {
                    if (item is RibbonApplicationMenuItem menuItem)
                    {
                        string titulo = (menuItem.Header as string ?? string.Empty).Replace("_", string.Empty);
                        var elemento = new ElementoMenu(MapaMenuPorRol.Clave("Menú de aplicación", string.Empty, titulo))
                        {
                            Fuente = menuItem.DataContext,
                            Origen = menuItem,
                            Titulo = titulo
                        };
                        Enlazar(elemento, ElementoMenuBase.VisibilidadProperty, menuItem, UIElement.VisibilityProperty);
                        Enlazar(elemento, ElementoMenuBase.AyudaProperty, menuItem, FrameworkElement.ToolTipProperty);
                        Enlazar(elemento, ElementoMenu.ComandoProperty, menuItem, MenuItem.CommandProperty);
                        Enlazar(elemento, ElementoMenu.ParametroComandoProperty, menuItem, MenuItem.CommandParameterProperty);
                        menuAplicacion.Add(elemento);
                    }
                }
            }

            var elementos = new List<ElementoMenuBase>();
            foreach (object objetoPestana in cinta.Items)
            {
                if (objetoPestana is not RibbonTab pestana)
                {
                    continue;
                }
                string tituloPestana = pestana.Header as string ?? pestana.Name;
                foreach (object objetoGrupo in pestana.Items)
                {
                    if (objetoGrupo is not RibbonGroup grupo)
                    {
                        continue;
                    }
                    string tituloGrupo = grupo.Header as string ?? grupo.Name;
                    foreach (object objeto in grupo.Items)
                    {
                        if (objeto is FrameworkElement origen)
                        {
                            elementos.Add(Crear(origen, tituloPestana, tituloGrupo, pestana, grupo));
                        }
                    }
                }
            }
            return elementos;
        }

        private static ElementoMenuBase Crear(FrameworkElement origen, string pestana, string grupo, RibbonTab tab, RibbonGroup ribbonGroup)
        {
            string clave = MapaMenuPorRol.Clave(pestana, grupo, NombreEnLaCinta(origen));
            ElementoMenuBase elemento;
            if (origen is RibbonButton boton)
            {
                var elementoBoton = new ElementoMenu(clave);
                Enlazar(elementoBoton, ElementoMenuBase.TituloProperty, boton, RibbonButton.LabelProperty);
                Enlazar(elementoBoton, ElementoMenuBase.IconoProperty, boton,
                    boton.LargeImageSource != null || BindingOperations.IsDataBound(boton, RibbonButton.LargeImageSourceProperty)
                        ? RibbonButton.LargeImageSourceProperty
                        : RibbonButton.SmallImageSourceProperty);
                Enlazar(elementoBoton, ElementoMenu.ComandoProperty, boton, ButtonBase.CommandProperty);
                Enlazar(elementoBoton, ElementoMenu.ParametroComandoProperty, boton, ButtonBase.CommandParameterProperty);
                elemento = elementoBoton;
            }
            else
            {
                elemento = new ContenidoMenu(clave);
                if (origen is RibbonCheckBox casilla)
                {
                    Enlazar(elemento, ElementoMenuBase.TituloProperty, casilla, RibbonCheckBox.LabelProperty);
                }
                else
                {
                    elemento.Titulo = grupo;
                }
            }
            elemento.Fuente = origen.DataContext;
            elemento.Origen = origen;
            Enlazar(elemento, ElementoMenuBase.AyudaProperty, origen, FrameworkElement.ToolTipProperty);
            Enlazar(elemento, ElementoMenuBase.VisibilidadProperty, origen, UIElement.VisibilityProperty);
            Enlazar(elemento, ElementoMenuBase.VisibilidadGrupoProperty, ribbonGroup, UIElement.VisibilityProperty);
            Enlazar(elemento, ElementoMenuBase.VisibilidadPestanaProperty, tab, UIElement.VisibilityProperty);
            return elemento;
        }

        /// <summary>
        /// Cómo se nombra el elemento en la clave del mapa: su texto si es fijo; si el texto es un Binding (cambia), el
        /// comando entre corchetes; si no tiene ni lo uno ni lo otro, su tipo entre corchetes.
        /// </summary>
        internal static string NombreEnLaCinta(FrameworkElement origen)
        {
            (DependencyProperty propiedadTexto, string texto) = origen switch
            {
                RibbonButton b => (RibbonButton.LabelProperty, b.Label),
                RibbonToggleButton t => (RibbonToggleButton.LabelProperty, t.Label),
                RibbonCheckBox c => (RibbonCheckBox.LabelProperty, c.Label),
                _ => ((DependencyProperty)null, (string)null)
            };
            if (propiedadTexto != null && !BindingOperations.IsDataBound(origen, propiedadTexto) && !string.IsNullOrWhiteSpace(texto))
            {
                return texto;
            }
            if (origen is ICommandSource && BindingOperations.GetBinding(origen, ButtonBase.CommandProperty) is Binding enlaceComando
                && !string.IsNullOrEmpty(enlaceComando.Path?.Path))
            {
                return "[" + enlaceComando.Path.Path + "]";
            }
            return "[" + origen.GetType().Name + "]";
        }

        /// <summary>
        /// Enlaza la propiedad del elemento con la del elemento de la cinta. Si la de la cinta es un Binding normal (sobre su
        /// DataContext: Command="{Binding XCommand}", Visibility="{Binding XVisible, Converter=...}"), se COPIA con el
        /// DataContext como Source: así funciona desde ya (los Binding de la cinta no se activan hasta que corre el
        /// dispatcher, a prioridad DataBind, y el catálogo se hace antes de mostrar la ventana) y sigue vivo, porque
        /// escucha al mismo ViewModel. Si no (un valor fijo, ElementName, RelativeSource…), se enlaza con la propiedad del
        /// elemento de la cinta.
        /// </summary>
        internal static void Enlazar(DependencyObject destino, DependencyProperty propiedad, FrameworkElement origen, DependencyProperty propiedadOrigen)
        {
            Binding enlace = BindingOperations.GetBinding(origen, propiedadOrigen);
            object dataContext = origen.DataContext;
            if (enlace != null && enlace.Source == null && enlace.RelativeSource == null && string.IsNullOrEmpty(enlace.ElementName)
                && dataContext != null)
            {
                var copia = new Binding
                {
                    Path = enlace.Path,
                    Source = dataContext,
                    Mode = BindingMode.OneWay,
                    Converter = enlace.Converter,
                    ConverterParameter = enlace.ConverterParameter,
                    ConverterCulture = enlace.ConverterCulture,
                    StringFormat = enlace.StringFormat,
                    TargetNullValue = enlace.TargetNullValue
                };
                if (enlace.FallbackValue != DependencyProperty.UnsetValue)
                {
                    copia.FallbackValue = enlace.FallbackValue;
                }
                _ = BindingOperations.SetBinding(destino, propiedad, copia);
                return;
            }
            _ = BindingOperations.SetBinding(destino, propiedad, new Binding { Path = new PropertyPath(propiedadOrigen), Source = origen, Mode = BindingMode.OneWay });
        }
    }
}
