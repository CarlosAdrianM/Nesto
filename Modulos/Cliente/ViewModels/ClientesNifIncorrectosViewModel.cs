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
            MarcarExtranjeroCommand = new DelegateCommand(async () => await MarcarExtranjeroAsync(), CanMarcarExtranjero);
            _ = CargarAsync(); // carga inicial al abrir la ventana
        }

        /// <summary>NestoAPI#339: catálogo L7 de la AEAT para identificaciones sin NIF español.</summary>
        public List<TipoIdentificacionExtranjera> TiposIdentificacion { get; } = new List<TipoIdentificacionExtranjera>
        {
            new TipoIdentificacionExtranjera("03", "Pasaporte"),
            new TipoIdentificacionExtranjera("02", "NIF-IVA intracomunitario"),
            new TipoIdentificacionExtranjera("04", "Documento oficial del país"),
            new TipoIdentificacionExtranjera("05", "Certificado de residencia"),
            new TipoIdentificacionExtranjera("06", "Otro documento probatorio"),
            new TipoIdentificacionExtranjera("07", "No censado")
        };

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
                    MarcarExtranjeroCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private TipoIdentificacionExtranjera _tipoIdentificacionSeleccionado;
        public TipoIdentificacionExtranjera TipoIdentificacionSeleccionado
        {
            get => _tipoIdentificacionSeleccionado;
            set
            {
                if (SetProperty(ref _tipoIdentificacionSeleccionado, value))
                {
                    MarcarExtranjeroCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _paisIdentificacion;
        public string PaisIdentificacion
        {
            get => _paisIdentificacion;
            set
            {
                if (SetProperty(ref _paisIdentificacion, value))
                {
                    MarcarExtranjeroCommand.RaiseCanExecuteChanged();
                    // Al indicar país, el cliente es EXTRANJERO: "Corregir NIF" (que valida contra
                    // la AEAT española) deja de tener sentido y se deshabilita.
                    CorregirCommand.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(EsClienteEspanol));
                }
            }
        }

        /// <summary>Sin país indicado, el cliente se trata como español (se corrige el NIF y se
        /// valida contra la AEAT). Con país, es extranjero (se marca con tipo + país, sin censo).</summary>
        public bool EsClienteEspanol => string.IsNullOrWhiteSpace(PaisIdentificacion);

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
        // Solo el camino ESPAÑOL: hay cliente, NIF nuevo y NO se ha indicado país (si hay país,
        // es extranjero → se usa "Marcar como extranjero"). Así los dos botones son excluyentes.
        private bool CanCorregir() => ClienteSeleccionado != null
            && !string.IsNullOrWhiteSpace(NifNuevo)
            && string.IsNullOrWhiteSpace(PaisIdentificacion);

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

        // NestoAPI#339: pasaportes y demás — dejan de validarse contra el censo (no aplica)
        // y las facturas se declaran con IDOtro (tipo + país) en vez de NIF.
        public DelegateCommand MarcarExtranjeroCommand { get; }
        private bool CanMarcarExtranjero() => ClienteSeleccionado != null
            && TipoIdentificacionSeleccionado != null
            && !string.IsNullOrWhiteSpace(PaisIdentificacion);

        public async Task MarcarExtranjeroAsync()
        {
            if (!CanMarcarExtranjero())
            {
                return;
            }
            ClienteNifIncorrectoModel cliente = ClienteSeleccionado;
            string pais = PaisIdentificacion.Trim().ToUpper();
            // NestoAPI#356: si el usuario escribió el NIF-IVA completo en "NIF correcto", se envía
            // para corregir el que se truncó a 9 caracteres (IT+11 dígitos no cabía en el char(9)).
            string nifNuevo = string.IsNullOrWhiteSpace(NifNuevo) ? null : NifNuevo.Trim().ToUpper();

            string textoNif = nifNuevo != null
                ? $"con el NIF-IVA '{nifNuevo}'"
                : $"con la identificación '{cliente.Nif?.Trim()}'";
            bool confirmado = _dialogService.ShowConfirmationAnswer("Identificación extranjera",
                $"¿Marcar el cliente {cliente.Cliente} - {cliente.Nombre?.Trim()} como " +
                $"{TipoIdentificacionSeleccionado.Descripcion} de {pais} {textoNif}?" + Environment.NewLine +
                "Dejará de validarse contra el censo de la AEAT (no aplica a identificaciones " +
                "extranjeras) y las facturas se declararán a Verifactu con ese tipo y país." +
                (nifNuevo != null ? Environment.NewLine + "El NIF se corregirá en la ficha y en las facturas pendientes de declarar." : string.Empty));
            if (!confirmado)
            {
                return;
            }

            try
            {
                EstaOcupado = true;
                ResultadoCorreccionNifModel resultado = await _servicio.MarcarIdentificacionExtranjera(
                    cliente.Cliente, TipoIdentificacionSeleccionado.Codigo, pais, nifNuevo);
                _dialogService.ShowNotification("Identificación extranjera", resultado.Motivo);
                PaisIdentificacion = string.Empty;
                NifNuevo = string.Empty;
                await CargarAsync(); // el cliente desaparece de la lista
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }

    /// <summary>NestoAPI#339: entrada del catálogo L7 para el combo.</summary>
    public class TipoIdentificacionExtranjera
    {
        public TipoIdentificacionExtranjera(string codigo, string descripcion)
        {
            Codigo = codigo;
            Descripcion = descripcion;
        }
        public string Codigo { get; }
        public string Descripcion { get; }
    }
}
