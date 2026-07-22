using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#417: clientes con NIF incorrecto para Verifactu, con corrección rápida.
    /// El usuario mete el NIF bueno y el servidor hace TODO (revalida contra la AEAT,
    /// propaga a los contactos, corrige las facturas sin declarar y el job de Verifactu
    /// las subsana solo). Permisos v1: administración y dirección ven todos los clientes;
    /// el resto, los de su vendedor (parámetro Vendedor del usuario). El filtro fino por
    /// equipos de venta (jefes) queda para una iteración posterior.
    /// </summary>
    public class ClientesNifIncorrectosViewModel : BindableBase
    {
        private readonly INifIncorrectosService _servicio;
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;

        public ClientesNifIncorrectosViewModel(INifIncorrectosService servicio,
            IConfiguracion configuracion, IDialogService dialogService)
        {
            _servicio = servicio;
            _configuracion = configuracion;
            _dialogService = dialogService;
            Titulo = "Clientes con NIF incorrecto";
            CargarCommand = new DelegateCommand(async () => await CargarAsync());
            CorregirCommand = new DelegateCommand(async () => await CorregirAsync(), CanCorregir);
            _ = CargarAsync(); // carga inicial al abrir la ventana
        }

        public string Titulo { get; }

        private ObservableCollection<ClienteNifIncorrectoModel> _clientes = new ObservableCollection<ClienteNifIncorrectoModel>();
        public ObservableCollection<ClienteNifIncorrectoModel> Clientes
        {
            get => _clientes;
            private set => SetProperty(ref _clientes, value);
        }

        private ClienteNifIncorrectoModel _clienteSeleccionado;
        public ClienteNifIncorrectoModel ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                if (SetProperty(ref _clienteSeleccionado, value))
                {
                    NifNuevo = string.Empty;
                    CorregirCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _nifNuevo;
        public string NifNuevo
        {
            get => _nifNuevo;
            set
            {
                if (SetProperty(ref _nifNuevo, value))
                {
                    CorregirCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set => SetProperty(ref _estaOcupado, value);
        }

        public DelegateCommand CargarCommand { get; }

        // Function As Task para poder esperarla en los tests (patrón Fase 1C).
        public async Task CargarAsync()
        {
            try
            {
                EstaOcupado = true;
                string vendedor = await VendedorFiltro();
                if (SinPermisoNiVendedor)
                {
                    Clientes = new ObservableCollection<ClienteNifIncorrectoModel>();
                    return;
                }
                List<ClienteNifIncorrectoModel> lista = await _servicio.LeerNifIncorrectos(vendedor);
                Clientes = new ObservableCollection<ClienteNifIncorrectoModel>(lista);
            }
            catch (Exception ex)
            {
                Clientes = new ObservableCollection<ClienteNifIncorrectoModel>();
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>Sin grupo privilegiado y sin vendedor asociado: no puede ver nada.</summary>
        private bool SinPermisoNiVendedor { get; set; }

        // Null = sin filtro (ve todos); si no, el vendedor del usuario.
        internal async Task<string> VendedorFiltro()
        {
            SinPermisoNiVendedor = false;
            if (_configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION))
            {
                return null;
            }
            string vendedor = await _configuracion.leerParametro(
                Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.Vendedor);
            if (string.IsNullOrWhiteSpace(vendedor))
            {
                SinPermisoNiVendedor = true;
                return null;
            }
            return vendedor.Trim();
        }

        public DelegateCommand CorregirCommand { get; }
        private bool CanCorregir() => ClienteSeleccionado != null && !string.IsNullOrWhiteSpace(NifNuevo);

        public async Task CorregirAsync()
        {
            if (!CanCorregir())
            {
                return;
            }
            ClienteNifIncorrectoModel cliente = ClienteSeleccionado;
            string nif = NifNuevo.Trim().ToUpper();

            bool confirmado = _dialogService.ShowConfirmationAnswer("Corregir NIF",
                $"¿Corregir el NIF del cliente {cliente.Cliente} - {cliente.Nombre?.Trim()} " +
                $"de '{cliente.Nif?.Trim()}' a '{nif}'?" + Environment.NewLine +
                "Se validará contra la AEAT y, si es correcto, se actualizarán la ficha, " +
                "todos sus contactos y las facturas pendientes de declarar a Verifactu.");
            if (!confirmado)
            {
                return;
            }

            try
            {
                EstaOcupado = true;
                ResultadoCorreccionNifModel resultado = await _servicio.CorregirNif(cliente.Cliente, nif);
                _dialogService.ShowNotification("NIF corregido",
                    $"Hacienda reconoce el NIF {resultado.Nif} como '{resultado.NombreAeat?.Trim()}'." + Environment.NewLine +
                    $"Contactos actualizados: {resultado.ContactosActualizados}. " +
                    $"Facturas sin declarar corregidas: {resultado.FacturasActualizadas} " +
                    "(Verifactu las declarará solo en la próxima pasada).");
                NifNuevo = string.Empty;
                await CargarAsync(); // el cliente corregido desaparece de la lista
            }
            catch (Exception ex)
            {
                // Los BadRequest traen el motivo legible ("La AEAT no reconoce el NIF X...")
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
