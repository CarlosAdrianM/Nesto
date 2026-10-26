using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modules.Producto
{
    public class ProductoMenuBarViewModel : ViewModelBase
    {
        private IServicioNavegacion Navegacion { get; }
        private IConfiguracion Configuracion { get; }
        private IServicioDiferenciasReposicion Diferencias { get; }
        public ProductoMenuBarViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion, IServicioDiferenciasReposicion diferencias)
        {
            Navegacion = navegacion;
            Configuracion = configuracion;
            Diferencias = diferencias;

            AbrirModuloFichaCommand = new RelayCommand(OnAbrirModuloFicha, CanAbrirModuloFicha);
            AbrirModuloReposicionCommand = new RelayCommand(OnAbrirModuloReposicion, CanAbrirModuloReposicion);
            AbrirEtiquetasHuecoCommand = new RelayCommand(OnAbrirEtiquetasHueco, CanAbrirEtiquetasHueco);
            AbrirRecibirReposicionCommand = new RelayCommand(OnAbrirRecibirReposicion, CanAbrirRecibirReposicion);
            AbrirEnviarReposicionCommand = new RelayCommand(OnAbrirEnviarReposicion, CanAbrirRecibirReposicion);
            AbrirCalendarioReposicionesCommand = new RelayCommand(OnAbrirCalendarioReposiciones);
            AbrirPropuestasStockCommand = new RelayCommand(OnAbrirPropuestasStock, () => PuedeVerPropuestasStock);
            AbrirDiferenciasReposicionCommand = new RelayCommand(OnAbrirDiferenciasReposicion, CanAbrirRecibirReposicion);
            AbrirFacturacionTiendaCommand = new RelayCommand(OnAbrirFacturacionTienda, CanAbrirFacturacionTienda);

            if (Diferencias != null && CanAbrirRecibirReposicion())
            {
                Diferencias.DiferenciasCambiadas += (_, _) => _ = ActualizarDiferenciasPendientesAsync();
                _ = ActualizarDiferenciasPendientesAsync();
            }
        }

        public const string ETIQUETA_DIFERENCIAS = "Diferencias";
        public const string VISTA_FACTURACION_TIENDA = "FacturacionTiendaView";

        /// <summary>
        /// Sugerencia 648 (Paloma, NestoAPI#658): el Excel con lo facturado en la tienda para el informe de cifras de la semana.
        /// Las tiendas sacan la suya; Dirección, Administración e Informática, cualquiera (lo comprueba la API).
        /// </summary>
        public ICommand AbrirFacturacionTiendaCommand { get; private set; }
        internal bool CanAbrirFacturacionTienda()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)
                || Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                || Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA);
        }
        private void OnAbrirFacturacionTienda()
        {
            Navegacion.RequestNavigate("MainRegion", VISTA_FACTURACION_TIENDA);
        }

        /// <summary>
        /// Nesto#526 (NestoAPI#634): lo recibido que no coincide con lo enviado, pendiente de comprobar en el origen. La ven
        /// los mismos que reciben reposiciones; la API decide cuáles ve cada uno (Almacén y Dirección todas; el resto, las de
        /// su almacén) y quién resuelve.
        /// </summary>
        public ICommand AbrirDiferenciasReposicionCommand { get; private set; }
        private void OnAbrirDiferenciasReposicion()
        {
            Navegacion.RequestNavigate("MainRegion", "DiferenciasReposicionView");
            _ = ActualizarDiferenciasPendientesAsync();
        }

        private int _diferenciasPendientes;
        /// <summary>Las pendientes que puede ver el usuario (0 si no se ha podido leer).</summary>
        public int DiferenciasPendientes
        {
            get => _diferenciasPendientes;
            private set
            {
                if (SetProperty(ref _diferenciasPendientes, value))
                {
                    RaisePropertyChanged(nameof(EtiquetaDiferencias));
                    RaisePropertyChanged(nameof(AyudaDiferencias));
                }
            }
        }

        /// <summary>«Diferencias» o «Diferencias (2)».</summary>
        public string EtiquetaDiferencias => DiferenciasPendientes > 0 ? $"{ETIQUETA_DIFERENCIAS} ({DiferenciasPendientes})" : ETIQUETA_DIFERENCIAS;

        public string AyudaDiferencias => DiferenciasPendientes switch
        {
            0 => "Diferencias entre lo enviado y lo recibido en las reposiciones",
            1 => "1 diferencia de reposición pendiente de comprobar",
            _ => $"{DiferenciasPendientes} diferencias de reposición pendientes de comprobar"
        };

        /// <summary>Lee el contador de la API; si falla (sin permiso, sin conexión…) se queda como estaba, sin molestar.</summary>
        internal async Task ActualizarDiferenciasPendientesAsync()
        {
            try
            {
                ContadorDiferenciasReposicion contador = await Diferencias.ContarPendientes(Constantes.Empresas.EMPRESA_DEFECTO, null).ConfigureAwait(true);
                DiferenciasPendientes = contador?.Pendientes ?? 0;
            }
            catch (Exception)
            {
                // El contador es una ayuda: la ventana enseña el error si lo hay
            }
        }

        /// <summary>
        /// Nesto#529 (NestoAPI#631): las propuestas mensuales de máximo y mínimo. Solo las ven y deciden Compras y Dirección
        /// (la API contesta 403 al resto); los demás no ven la opción en la cinta.
        /// </summary>
        public bool PuedeVerPropuestasStock => Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)
            || Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        public ICommand AbrirPropuestasStockCommand { get; private set; }
        private void OnAbrirPropuestasStock()
        {
            Navegacion.RequestNavigate("MainRegion", NotificacionBuzon.VISTA_PROPUESTAS_STOCK);
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
