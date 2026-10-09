using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Nesto.Modules.Producto
{
    public class ProductoMenuBarViewModel : ViewModelBase
    {
        private IServicioNavegacion Navegacion { get; }
        private IConfiguracion Configuracion { get; }
        public ProductoMenuBarViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion)
        {
            Navegacion = navegacion;
            Configuracion = configuracion;

            AbrirModuloFichaCommand = new RelayCommand(OnAbrirModuloFicha, CanAbrirModuloFicha);
            AbrirModuloReposicionCommand = new RelayCommand(OnAbrirModuloReposicion, CanAbrirModuloReposicion);
            AbrirEtiquetasHuecoCommand = new RelayCommand(OnAbrirEtiquetasHueco, CanAbrirEtiquetasHueco);
            AbrirRecibirReposicionCommand = new RelayCommand(OnAbrirRecibirReposicion, CanAbrirRecibirReposicion);
            AbrirEnviarReposicionCommand = new RelayCommand(OnAbrirEnviarReposicion, CanAbrirRecibirReposicion);
            AbrirCalendarioReposicionesCommand = new RelayCommand(OnAbrirCalendarioReposiciones);
        }

        /// <summary>
        /// NestoAPI#577: el calendario de reposiciones (días, hora de cierre y de llegada de cada ruta). Lo ve cualquiera;
        /// cambiarlo solo pueden quienes rellenan reposiciones a mano (lo decide la API y la ventana queda en solo lectura).
        /// </summary>
        public ICommand AbrirCalendarioReposicionesCommand { get; private set; }
        private void OnAbrirCalendarioReposiciones()
        {
            Navegacion.RequestNavigate("MainRegion", "CalendarioReposicionesView");
        }

        /// <summary>
        /// NestoAPI#553: la tienda prepara y termina la reposición que manda a Algete (antes en Nesto viejo). Escribir solo
        /// puede quien tiene como AlmacénPedidoVta el almacén de origen, o Almacén o Dirección (lo comprueba la API).
        /// </summary>
        public ICommand AbrirEnviarReposicionCommand { get; private set; }
        private void OnAbrirEnviarReposicion()
        {
            Navegacion.RequestNavigate("MainRegion", "EnviarReposicionView");
        }

        /// <summary>
        /// NestoAPI#553: recibir en la tienda la reposición que sale de Algete. Terminarla solo puede quien tiene como
        /// AlmacénPedidoVta el almacén de destino (lo comprueba la API).
        /// </summary>
        public ICommand AbrirRecibirReposicionCommand { get; private set; }
        private bool CanAbrirRecibirReposicion()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)
                || Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                || Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        }
        private void OnAbrirRecibirReposicion()
        {
            Navegacion.RequestNavigate("MainRegion", "RecibirReposicionView");
        }

        /// <summary>Etiquetas de hueco del almacén (la API comprueba además que sea de Almacén o Dirección).</summary>
        public ICommand AbrirEtiquetasHuecoCommand { get; private set; }
        private bool CanAbrirEtiquetasHueco()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                || Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        }
        private void OnAbrirEtiquetasHueco()
        {
            Navegacion.RequestNavigate("MainRegion", "EtiquetasHuecoView");
        }

        public ICommand AbrirModuloFichaCommand { get; private set; }
        private bool CanAbrirModuloFicha()
        {
            return true;
        }
        private void OnAbrirModuloFicha()
        {
            Navegacion.RequestNavigate("MainRegion", "ProductoView");
        }

        public ICommand AbrirModuloReposicionCommand { get; private set; }
        private bool CanAbrirModuloReposicion()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN);
        }
        private void OnAbrirModuloReposicion()
        {
            Navegacion.RequestNavigate("MainRegion", "ReposicionView");
        }
    }
}
