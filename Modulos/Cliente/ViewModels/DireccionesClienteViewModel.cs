using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#879 (corte 3): una fila de la pestaña «Direcciones».</summary>
    public class DireccionClienteFila
    {
        public const string FACTURACION = "Facturación";
        public const string ENTREGA_POR_DEFECTO = "Entrega por defecto";

        public DireccionClienteFila(DireccionClienteModel modelo)
        {
            Contacto = modelo.contacto?.Trim();
            Nombre = modelo.nombre?.Trim();
            Direccion = modelo.DireccionMostrar;
            Poblacion = string.Format("{0} {1} ({2})", modelo.codigoPostal?.Trim(), modelo.poblacion?.Trim(), modelo.provincia?.Trim()).Trim();
            EsFacturacion = modelo.clientePrincipal;
            EsEntregaPorDefecto = modelo.esDireccionPorDefecto;
        }

        public string Contacto { get; }
        public string Nombre { get; }
        /// <summary>La dirección completa (con la Dirección 2) o, con la API vieja, la de siempre.</summary>
        public string Direccion { get; }
        public string Poblacion { get; }
        public bool EsFacturacion { get; }
        public bool EsEntregaPorDefecto { get; }

        /// <summary>«Facturación», «Entrega por defecto», las dos o nada.</summary>
        public string Marcas => string.Join(" · ", new[] { EsFacturacion ? FACTURACION : null, EsEntregaPorDefecto ? ENTREGA_POR_DEFECTO : null }
            .Where(m => m != null));

        public bool PuedeSerEntregaPorDefecto => !EsEntregaPorDefecto;
    }

    /// <summary>
    /// NestoAPI#879 (corte 3, Carlos 19/11/26): pestaña «Direcciones» de la ficha del cliente. Enseña todos sus contactos activos con la
    /// dirección completa y marca la de FACTURACIÓN (la ficha principal) y la de ENTREGA POR DEFECTO (el ContactoDefecto de la
    /// principal); permite elegir otra como entrega por defecto (PUT api/Clientes/ContactoDefecto). No cambia la de facturación.
    /// </summary>
    public class DireccionesClienteViewModel : ObservableObject
    {
        private readonly IDireccionesClienteService _servicio;
        private readonly IServicioDialogos _dialogos;
        private int _version;

        public DireccionesClienteViewModel(IDireccionesClienteService servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            HacerEntregaPorDefectoCommand = new AsyncRelayCommand<DireccionClienteFila>(HacerEntregaPorDefectoAsync, PuedeHacerEntregaPorDefecto);
        }

        public AsyncRelayCommand<DireccionClienteFila> HacerEntregaPorDefectoCommand { get; }

        public string Empresa { get; private set; }
        public string Cliente { get; private set; }
        public bool HayCliente => !string.IsNullOrWhiteSpace(Cliente);

        private ObservableCollection<DireccionClienteFila> _direcciones = new();
        public ObservableCollection<DireccionClienteFila> Direcciones
        {
            get => _direcciones;
            private set => SetProperty(ref _direcciones, value);
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    HacerEntregaPorDefectoCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje
        {
            get => _mensaje;
            private set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                }
            }
        }

        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        /// <summary>Lee las direcciones del cliente (la respuesta de uno anterior se descarta).</summary>
        public async Task CargarAsync(string empresa, string cliente)
        {
            Empresa = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
            Cliente = cliente?.Trim();
            OnPropertyChanged(nameof(HayCliente));
            int version = ++_version;
            Mensaje = null;
            Direcciones = new ObservableCollection<DireccionClienteFila>();
            if (!HayCliente || _servicio == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                var leidas = await _servicio.Leer(Empresa, Cliente);
                if (version == _version)
                {
                    Direcciones = new ObservableCollection<DireccionClienteFila>((leidas ?? new()).Where(d => d != null).Select(d => new DireccionClienteFila(d)));
                    if (!Direcciones.Any(d => d.EsEntregaPorDefecto))
                    {
                        Mensaje = "Este cliente no tiene ninguna dirección de entrega por defecto.";
                    }
                }
            }
            catch (Exception ex)
            {
                if (version == _version)
                {
                    Mensaje = ex.Message;
                }
            }
            finally
            {
                if (version == _version)
                {
                    EstaOcupado = false;
                }
            }
        }

        private bool PuedeHacerEntregaPorDefecto(DireccionClienteFila fila) => fila != null && fila.PuedeSerEntregaPorDefecto && HayCliente && !EstaOcupado;

        internal static string TextoConfirmacion(string cliente, DireccionClienteFila fila) =>
            $"¿Quiere que la dirección de entrega por defecto del cliente {cliente} sea la del contacto {fila.Contacto} ({fila.Direccion})?" +
            Environment.NewLine + Environment.NewLine + "La dirección de facturación no cambia.";

        public async Task HacerEntregaPorDefectoAsync(DireccionClienteFila fila)
        {
            if (!PuedeHacerEntregaPorDefecto(fila))
            {
                return;
            }
            if (_dialogos != null && !await _dialogos.ShowConfirmationAsync("Dirección de entrega por defecto", TextoConfirmacion(Cliente, fila)))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                await _servicio.CambiarEntregaPorDefecto(Empresa, Cliente, fila.Contacto);
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                _dialogos?.ShowError($"No se ha podido cambiar la dirección de entrega por defecto: {ex.Message}");
                return;
            }
            EstaOcupado = false;
            await CargarAsync(Empresa, Cliente);
            Mensaje = $"La dirección de entrega por defecto es ahora la del contacto {fila.Contacto}.";
        }
    }
}
