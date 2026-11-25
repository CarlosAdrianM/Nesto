using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#652 (NestoAPI#903): sección «Factura electrónica (FACe)» de la pestaña «Direcciones» de la ficha: los códigos DIR3 del
    /// organismo para el cliente y contacto (GET/PUT api/Clientes/FacturaElectronica). Oficina contable, órgano gestor y unidad
    /// tramitadora obligatorios; órgano proponente opcional; cada uno con su nombre opcional. Guardar con los cuatro vacíos =
    /// quitarlos. Solo la ven Administración, Dirección e Informática (la API da 403 a los demás). Nunca lanza.
    /// </summary>
    public class FacturaElectronicaClienteViewModel : ObservableObject
    {
        private readonly IServicioFacturae _servicio;
        private readonly IConfiguracion _configuracion;
        private int _version;

        public FacturaElectronicaClienteViewModel(IServicioFacturae servicio, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _configuracion = configuracion;
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => PuedeGuardar);
            QuitarCommand = new AsyncRelayCommand(QuitarAsync, () => PuedeGuardar && Guardado);
        }

        public AsyncRelayCommand GuardarCommand { get; }
        public AsyncRelayCommand QuitarCommand { get; }

        /// <summary>Los grupos que pueden ver y cambiar los DIR3 (los mismos que descargan para FACe).</summary>
        public bool PuedeVer => _configuracion != null &&
            (_configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) ||
             _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) ||
             _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        public string Empresa { get; private set; }
        public string Cliente { get; private set; }
        public string Contacto { get; private set; }

        private string _oficinaContable;
        public string OficinaContable { get => _oficinaContable; set => SetProperty(ref _oficinaContable, value); }
        private string _organoGestor;
        public string OrganoGestor { get => _organoGestor; set => SetProperty(ref _organoGestor, value); }
        private string _unidadTramitadora;
        public string UnidadTramitadora { get => _unidadTramitadora; set => SetProperty(ref _unidadTramitadora, value); }
        private string _organoProponente;
        public string OrganoProponente { get => _organoProponente; set => SetProperty(ref _organoProponente, value); }
        private string _nombreOficinaContable;
        public string NombreOficinaContable { get => _nombreOficinaContable; set => SetProperty(ref _nombreOficinaContable, value); }
        private string _nombreOrganoGestor;
        public string NombreOrganoGestor { get => _nombreOrganoGestor; set => SetProperty(ref _nombreOrganoGestor, value); }
        private string _nombreUnidadTramitadora;
        public string NombreUnidadTramitadora { get => _nombreUnidadTramitadora; set => SetProperty(ref _nombreUnidadTramitadora, value); }
        private string _nombreOrganoProponente;
        public string NombreOrganoProponente { get => _nombreOrganoProponente; set => SetProperty(ref _nombreOrganoProponente, value); }

        private bool _guardado;
        /// <summary>Hay códigos guardados para este cliente (o su única fila).</summary>
        public bool Guardado
        {
            get => _guardado;
            private set
            {
                if (SetProperty(ref _guardado, value))
                {
                    QuitarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    OnPropertyChanged(nameof(PuedeGuardar));
                    GuardarCommand.NotifyCanExecuteChanged();
                    QuitarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HayCliente => !string.IsNullOrWhiteSpace(Cliente);
        public bool PuedeGuardar => PuedeVer && HayCliente && !EstaOcupado && _servicio != null;

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

        private string _error;
        public string Error
        {
            get => _error;
            private set
            {
                if (SetProperty(ref _error, value))
                {
                    OnPropertyChanged(nameof(HayError));
                }
            }
        }
        public bool HayError => !string.IsNullOrWhiteSpace(Error);

        public const string AYUDA = "Los códigos DIR3 se los da el organismo (oficina contable, órgano gestor y unidad tramitadora; el órgano proponente, " +
            "solo si lo piden). Se usan al descargar una factura de este cliente para FACe («Descargar para FACe…»). Para quitarlos, use «Quitar».";
        public string Ayuda => AYUDA;

        /// <summary>Lee los DIR3 del cliente y contacto (la respuesta de uno anterior se descarta). Sin permiso, ni se pregunta.</summary>
        public async Task CargarAsync(string empresa, string cliente, string contacto)
        {
            Empresa = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
            Cliente = cliente?.Trim();
            Contacto = contacto?.Trim() ?? string.Empty;
            OnPropertyChanged(nameof(HayCliente));
            OnPropertyChanged(nameof(PuedeGuardar));
            GuardarCommand.NotifyCanExecuteChanged();
            int version = ++_version;
            Poner(null);
            Mensaje = null;
            Error = null;
            if (!PuedeVer || !HayCliente || _servicio == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                Dir3ClienteModel leido = await _servicio.LeerDir3(Empresa, Cliente, Contacto);
                if (version != _version)
                {
                    return;
                }
                Poner(leido);
                if (leido != null && !leido.TablaCreada)
                {
                    Mensaje = "El servidor todavía no guarda los códigos DIR3 de los clientes. Mientras tanto, se pueden escribir al descargar la factura.";
                }
                else if (leido != null && leido.Guardado && !string.IsNullOrWhiteSpace(leido.Usuario))
                {
                    Mensaje = $"Guardados por {leido.Usuario.Trim()}" + (leido.FechaModificacion.HasValue ? $" el {leido.FechaModificacion.Value:dd/MM/yyyy}." : ".");
                }
            }
            catch (Exception ex)
            {
                if (version == _version)
                {
                    Error = $"No se han podido leer los códigos DIR3: {ex.Message}";
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

        private void Poner(Dir3ClienteModel datos)
        {
            OficinaContable = datos?.OficinaContable?.Trim();
            OrganoGestor = datos?.OrganoGestor?.Trim();
            UnidadTramitadora = datos?.UnidadTramitadora?.Trim();
            OrganoProponente = datos?.OrganoProponente?.Trim();
            NombreOficinaContable = datos?.NombreOficinaContable?.Trim();
            NombreOrganoGestor = datos?.NombreOrganoGestor?.Trim();
            NombreUnidadTramitadora = datos?.NombreUnidadTramitadora?.Trim();
            NombreOrganoProponente = datos?.NombreOrganoProponente?.Trim();
            Guardado = datos?.Guardado == true;
        }

        /// <summary>«Guardar»: comprueba aquí lo mismo que la API y hace el PUT. Los cuatro vacíos = quitarlos.</summary>
        public async Task GuardarAsync()
        {
            if (!PuedeGuardar)
            {
                return;
            }
            List<string> motivos = ReglasFacturae.MotivosNoSePuedeGuardarDir3(OficinaContable, OrganoGestor, UnidadTramitadora, OrganoProponente);
            if (motivos.Any())
            {
                Mensaje = null;
                Error = string.Join(Environment.NewLine, motivos);
                return;
            }
            bool quitar = ReglasFacturae.SonTodosVacios(OficinaContable, OrganoGestor, UnidadTramitadora, OrganoProponente);
            await EnviarAsync(quitar ? Vacios() : Escritos(), quitar);
        }

        /// <summary>«Quitar»: deja los cuatro vacíos y guarda (la API los borra).</summary>
        public async Task QuitarAsync()
        {
            if (!PuedeGuardar || !Guardado)
            {
                return;
            }
            await EnviarAsync(Vacios(), true);
        }

        private async Task EnviarAsync(Dir3ClienteModel datos, bool quitar)
        {
            Mensaje = null;
            Error = null;
            EstaOcupado = true;
            try
            {
                Dir3ClienteModel guardado = await _servicio.GuardarDir3(datos);
                Poner(guardado ?? datos);
                if (guardado == null)
                {
                    Guardado = !quitar;
                }
                Mensaje = quitar
                    ? $"Se han quitado los códigos DIR3 del cliente {Cliente}/{Contacto}."
                    : $"Códigos DIR3 guardados para el cliente {Cliente}/{Contacto}.";
            }
            catch (Exception ex)
            {
                Error = $"No se han podido guardar los códigos DIR3: {ex.Message}";
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private Dir3ClienteModel Base() => new Dir3ClienteModel { Empresa = Empresa, Cliente = Cliente, Contacto = Contacto };

        private Dir3ClienteModel Vacios() => Base();

        private Dir3ClienteModel Escritos()
        {
            Dir3ClienteModel datos = Base();
            datos.OficinaContable = ReglasFacturae.LimpiarDir3(OficinaContable);
            datos.OrganoGestor = ReglasFacturae.LimpiarDir3(OrganoGestor);
            datos.UnidadTramitadora = ReglasFacturae.LimpiarDir3(UnidadTramitadora);
            datos.OrganoProponente = NuloSiVacio(ReglasFacturae.LimpiarDir3(OrganoProponente));
            datos.NombreOficinaContable = NuloSiVacio(NombreOficinaContable);
            datos.NombreOrganoGestor = NuloSiVacio(NombreOrganoGestor);
            datos.NombreUnidadTramitadora = NuloSiVacio(NombreUnidadTramitadora);
            datos.NombreOrganoProponente = NuloSiVacio(NombreOrganoProponente);
            return datos;
        }

        private static string NuloSiVacio(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }
}
