using Nesto.Infrastructure.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1): el menú de Nesto ya repartido por rol, listo para el panel lateral. En el
    /// tramo 1 lo saca el <c>LectorCinta</c> de la cinta ya fusionada (<see cref="IServicioCinta.Completar"/>), con los
    /// mismos comandos y ViewModels que la cinta; cuando los módulos declaren su menú, saldrá de ahí.
    /// </summary>
    public sealed class CatalogoMenu
    {
        private CatalogoMenu(IReadOnlyList<SeccionMenu> secciones, IReadOnlyList<ElementoMenuBase> elementos,
            IReadOnlyList<ElementoMenu> menuAplicacion, IReadOnlyList<string> sinMapear)
        {
            Secciones = secciones;
            Elementos = elementos;
            MenuAplicacion = menuAplicacion;
            SinMapear = sinMapear;
        }

        /// <summary>Un catálogo sin nada (antes de completar la cinta).</summary>
        public static CatalogoMenu Vacio { get; } = new CatalogoMenu(Array.Empty<SeccionMenu>(), Array.Empty<ElementoMenuBase>(),
            Array.Empty<ElementoMenu>(), Array.Empty<string>());

        /// <summary>Las secciones con algo dentro, en el orden del mapa («Otros», al final, solo si hace falta).</summary>
        public IReadOnlyList<SeccionMenu> Secciones { get; }

        /// <summary>Todos los elementos, en el orden en que estaban en la cinta.</summary>
        public IReadOnlyList<ElementoMenuBase> Elementos { get; }

        /// <summary>El menú de aplicación de la cinta (Parámetros y Novedades): el pie del panel.</summary>
        public IReadOnlyList<ElementoMenu> MenuAplicacion { get; }

        /// <summary>Las claves que no estaban en el mapa (han ido a «Otros»). Debería estar vacía.</summary>
        public IReadOnlyList<string> SinMapear { get; }

        /// <summary>
        /// Reparte los elementos por sección según <paramref name="destinos"/> (por defecto, <see cref="MapaMenuPorRol"/>).
        /// Lo que no está en el mapa va a «Otros» con el título del grupo de la cinta, detrás de todo.
        /// </summary>
        public static CatalogoMenu Construir(IEnumerable<ElementoMenuBase> elementos, IEnumerable<ElementoMenu> menuAplicacion,
            IReadOnlyDictionary<string, DestinoMenu> destinos = null, IEnumerable<DefinicionSeccion> secciones = null)
        {
            if (elementos == null)
            {
                throw new ArgumentNullException(nameof(elementos));
            }
            destinos ??= MapaMenuPorRol.Destinos;
            List<DefinicionSeccion> definiciones = (secciones ?? MapaMenuPorRol.Secciones).ToList();

            List<ElementoMenuBase> todos = elementos.ToList();
            var sinMapear = new List<string>();
            int posicion = 0;
            foreach (ElementoMenuBase elemento in todos)
            {
                posicion++;
                if (destinos.TryGetValue(elemento.Clave, out DestinoMenu destino) && definiciones.Any(d => d.Titulo == destino.Seccion))
                {
                    elemento.Seccion = destino.Seccion;
                    elemento.Grupo = destino.Grupo;
                    elemento.Orden = destino.Orden;
                    if (!string.IsNullOrEmpty(destino.Titulo))
                    {
                        // El título del mapa manda sobre el de la cinta (que se repite: «Ficha», «Pedidos»)
                        BindingOperations.ClearBinding(elemento, ElementoMenuBase.TituloProperty);
                        elemento.Titulo = destino.Titulo;
                    }
                }
                else
                {
                    sinMapear.Add(elemento.Clave);
                    elemento.Seccion = MapaMenuPorRol.OTROS;
                    elemento.Grupo = GrupoDeLaClave(elemento.Clave);
                    elemento.Orden = 10000 + posicion;
                }
            }

            var resultado = new List<SeccionMenu>();
            foreach (DefinicionSeccion definicion in definiciones.OrderBy(d => d.Orden))
            {
                List<ElementoMenuBase> deLaSeccion = todos.Where(e => e.Seccion == definicion.Titulo).OrderBy(e => e.Orden).ToList();
                if (deLaSeccion.Count == 0)
                {
                    continue;
                }
                var seccion = new SeccionMenu(definicion.Titulo, definicion.Icono, definicion.Orden);
                // Los grupos, en el orden de su primer elemento
                foreach (IGrouping<string, ElementoMenuBase> delGrupo in deLaSeccion.GroupBy(e => e.Grupo ?? string.Empty))
                {
                    var grupo = new GrupoMenu(delGrupo.Key);
                    foreach (ElementoMenuBase elemento in delGrupo)
                    {
                        grupo.Anadir(elemento);
                    }
                    seccion.Anadir(grupo);
                }
                resultado.Add(seccion);
            }

            return new CatalogoMenu(resultado, todos, (menuAplicacion ?? Enumerable.Empty<ElementoMenu>()).ToList(), sinMapear);
        }

        private static string GrupoDeLaClave(string clave)
        {
            string[] partes = clave.Split(new[] { MapaMenuPorRol.SEPARADOR }, StringSplitOptions.None);
            return partes.Length >= 2 ? partes[0] + MapaMenuPorRol.SEPARADOR + partes[1] : clave;
        }
    }
}
