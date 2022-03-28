using Prism.Commands;
using Prism.Events;
using Prism.Regions;
using Microsoft.VisualBasic;
using Nesto.Models.Nesto.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;
using Prism.Services.Dialogs;
using ControlesUsuario.Dialogs;
using Prism.Mvvm;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;

namespace Nesto.Modulos.Cliente
{
    public class CrearClienteViewModel: BindableBase, INavigationAware
    {
        private const string DATOS_FISCALES = "DatosFiscales";
        public const string DATOS_GENERALES = "DatosGenerales";
        public const string DATOS_COMISIONES = "DatosComisiones";
        private const string DATOS_PAGO = "DatosPago";
        private const string DATOS_CONTACTO = "DatosContacto";
        private IRegionManager RegionManager { get; }
        public IConfiguracion Configuracion { get; set; }
        private IClienteService Servicio { get; }

        private IEventAggregator EventAggregator { get; }
        private IDialogService DialogService { get; }

        public CrearClienteViewModel(IRegionManager regionManager, IConfiguracion configuracion, IClienteService servicio, IEventAggregator eventAggregator, IDialogService dialogService)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            Servicio = servicio;
            EventAggregator = eventAggregator;
            DialogService = dialogService;

            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo);
            AnnadirPersonaContactoCommand = new DelegateCommand(OnAnnadirPersonaContacto);
            BorrarPersonaContactoCommand = new DelegateCommand<PersonaContactoDTO>(OnBorrarPersonaContacto);
            CrearClienteCommand = new DelegateCommand(OnCrearCliente);
            LimpiarDireccionCommand = new DelegateCommand(OnLimpiarDireccion);

            Titulo = "Crear Cliente";
            PersonasContacto = new ObservableCollection<PersonaContactoDTO>()
            {
                new PersonaContactoDTO()
            };

        }

        #region "Propiedades"
        private string clienteCodigoPostal;
        public string ClienteCodigoPostal {
            get { return clienteCodigoPostal; }
            set { SetProperty(ref clienteCodigoPostal, value); }
        }
        public string ClienteContacto { get; set; }
        public bool ClienteDatosPagoValidados { get; set; }
        private string clienteDireccion;
        public string ClienteDireccion {
            get => clienteDireccion; 
            set { 
                SetProperty(ref clienteDireccion, value);
                RaisePropertyChanged(nameof(TieneDireccion));
                RaisePropertyChanged(nameof(NoTieneDireccion));
            }
        }
        private string clienteDireccionAdicional;
        public string ClienteDireccionAdicional {
            get { return clienteDireccionAdicional; }
            set { SetProperty(ref clienteDireccionAdicional, value); }
        }
        private string clienteDireccionCalleNumero;
        public string ClienteDireccionCalleNumero
        {
            get { return clienteDireccionCalleNumero; }
            set { SetProperty(ref clienteDireccionCalleNumero, value); }
        }
        private bool clienteDireccionValidada;
        public bool ClienteDireccionValidada {
            get { return clienteDireccionValidada; }
            set { SetProperty(ref clienteDireccionValidada, value); }
        }
        public string ClienteEmpresa { get; set; }
        private bool clienteEsContacto;
        public bool ClienteEsContacto {
            get => clienteEsContacto;
            set { 
                SetProperty(ref clienteEsContacto, value);
                RaisePropertyChanged(nameof(EsCreandoContacto));
            }
        }
        public short? ClienteEstado { get; set; }
        private string clienteFormaPago;
        public string ClienteFormaPago
        {
            get { return clienteFormaPago; }
            set { SetProperty(ref clienteFormaPago, value); }
        }
        private string clienteIban;
        public string ClienteIban
        {
            get { return clienteIban; }
            set { SetProperty(ref clienteIban, value); }
        }
        private string clienteNif;
        public string ClienteNif {
            get {
                return clienteNif;
            }
            set {
                SetProperty(ref clienteNif, value);
                RaisePropertyChanged(nameof(NombreIsEnabled));
                RaisePropertyChanged(nameof(SePuedeAvanzarADatosGenerales));
            }
        }
        private string clienteNombre;
        public string ClienteNombre {
            get {
                return clienteNombre;
            }
            set
            {
                SetProperty(ref clienteNombre, value);
                RaisePropertyChanged(nameof(SePuedeAvanzarADatosGenerales));
            }
        }
        private string clienteNumero;
        public string ClienteNumero {
            get { return clienteNumero; }
            set { SetProperty(ref clienteNumero, value); }
        }
        private string clientePlazosPago;
        public string ClientePlazosPago
        {
            get { return clientePlazosPago; }
            set { SetProperty(ref clientePlazosPago, value); }
        }
        private string clientePoblacion;
        public string ClientePoblacion {
            get { return clientePoblacion; }
            set { SetProperty(ref clientePoblacion, value); }
        }
        private string clienteProvincia;
        public string ClienteProvincia {
            get { return clienteProvincia; }
            set { SetProperty(ref clienteProvincia, value); }
        }
        public string ClienteRuta { get; set; }
        private string clienteTelefono;
        public string ClienteTelefono {
            get { return clienteTelefono; }
            set { SetProperty(ref clienteTelefono, value); }
        }
        private bool clienteTieneEstetica;
        public bool ClienteTieneEstetica {
            get { return clienteTieneEstetica; }
            set {
                SetProperty(ref clienteTieneEstetica, value);
                RaisePropertyChanged(nameof(SePuedeAvanzarADatosPago));
            }
        }
        private bool clienteTienePeluqueria;
        public bool ClienteTienePeluqueria {
            get { return clienteTienePeluqueria; }
            set {
                SetProperty(ref clienteTienePeluqueria, value);
                RaisePropertyChanged(nameof(SePuedeAvanzarADatosPago));
            }
        }
        private string clienteVendedorEstetica;
        public string ClienteVendedorEstetica
        {
            get { return clienteVendedorEstetica; }
            set { SetProperty(ref clienteVendedorEstetica, value); }
        }
        private string clienteVendedorPeluqueria;
        public string ClienteVendedorPeluqueria
        {
            get { return clienteVendedorPeluqueria; }
            set {
                SetProperty(ref clienteVendedorPeluqueria, value);
                RaisePropertyChanged(nameof(VendedorPeluqueriaMostrar));
            }
        }
        public bool EsCreandoContacto
        {
            get => ClienteEsContacto && !EsUnaModificacion;
        }
        public bool EstaOcupado { get; set; }
        private bool esUnaModificacion = false;
        public bool EsUnaModificacion { 
            get => esUnaModificacion;
            set { 
                SetProperty(ref esUnaModificacion, value);
                RaisePropertyChanged(nameof(EsCreandoContacto));
            }
        }
        private bool formaPagoEfectivo;
        public bool FormaPagoEfectivo
        {
            get { return formaPagoEfectivo; }
            set { SetProperty(ref formaPagoEfectivo, value); }
        }
        private bool formaPagoRecibo;
        public bool FormaPagoRecibo
        {
            get { return formaPagoRecibo; }
            set { SetProperty(ref formaPagoRecibo, value); }
        }
        private bool formaPagoTarjeta = true;
        public bool FormaPagoTarjeta
        {
            get { return formaPagoTarjeta; }
            set { SetProperty(ref formaPagoTarjeta, value); }
        }
        private bool nifValidado = false;
        public bool NifValidado {
            get { return nifValidado; }
            set {
                SetProperty(ref nifValidado, value);
                RaisePropertyChanged(nameof(NifSinValidar));
                RaisePropertyChanged(nameof(NombreIsEnabled));
            }
        }
        public bool NifSinValidar
        {
            get { return !NifValidado; }
        }
        public bool NombreIsEnabled
        {
            get
            {
                return NifValidado || string.IsNullOrWhiteSpace(ClienteNif) || (!string.IsNullOrWhiteSpace(ClienteNif) && "0123456789YX".Contains(ClienteNif.Trim().ToUpper()[0]));
            }
        }
        public bool NoTieneDireccion
        {
            get => !TieneDireccion;
        }
        private WizardPage paginaActual;
        public WizardPage PaginaActual {
            get
            {
                return paginaActual;
            }
            set
            {
                if (paginaActual?.Name == DATOS_FISCALES && value?.Name == DATOS_GENERALES)
                {
                    Task a = GoToDatosGenerales();
                    a.ContinueWith((t1)=>{
                        if (NifValidado)
                        {
                            PaginaAnterior = paginaActual;
                            SetProperty(ref paginaActual, value);
                        }
                    });
                    
                } else if (paginaActual?.Name == DATOS_GENERALES && value?.Name == DATOS_COMISIONES)
                {
                    Task a = GoToDatosComisiones();
                    a.ContinueWith((t1) => {
                        if (ClienteDireccionValidada)
                        {
                            PaginaAnterior = paginaActual;
                            SetProperty(ref paginaActual, value);
                        }
                    });
                } else if (paginaActual?.Name == DATOS_PAGO && value?.Name==DATOS_CONTACTO)
                {
                    Task a = GoToDatosContacto();
                    a.ContinueWith((t1) =>
                    {
                        if (ClienteDatosPagoValidados)
                        {
                            PaginaAnterior = paginaActual;
                            SetProperty(ref paginaActual, value);
                        }
                    });
                }
                else
                {
                    PaginaAnterior = paginaActual;
                    SetProperty(ref paginaActual, value);
                }
                
            }
        }
        public WizardPage PaginaAnterior { get; set; }
        private ObservableCollection<PersonaContactoDTO> personasContacto;
        public ObservableCollection<PersonaContactoDTO> PersonasContacto
        {
            get { return personasContacto; }
            set { SetProperty(ref personasContacto, value); }
        }
        public bool SePuedeAvanzarADatosGenerales
        {
            get
            {
                return (!NombreIsEnabled && !string.IsNullOrWhiteSpace(ClienteNif)) || (NombreIsEnabled && !string.IsNullOrWhiteSpace(ClienteNombre));
            }
        }
        public bool TieneDireccion
        {
            get => !string.IsNullOrEmpty(ClienteDireccion);
        }
        private string _titulo;
        public string Titulo
        {
            get => _titulo;
            set => SetProperty(ref _titulo, value);
        }
        public string VendedorPeluqueriaMostrar
        {
            get {
                return ClienteVendedorPeluqueria ?? ClienteVendedorEstetica + "*";
            }
        }
        public bool SePuedeAvanzarADatosPago
        {
            get
            {
                return ClienteTieneEstetica || ClienteTienePeluqueria;
            }
        }
        #endregion

        #region "Comandos"
        public ICommand AbrirModuloCommand { get; private set; }
        private void OnAbrirModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "CrearClienteView");
        }

        public ICommand AnnadirPersonaContactoCommand { get; private set; }
        private void OnAnnadirPersonaContacto()
        {
            PersonasContacto.Add(new PersonaContactoDTO());
        }
        public ICommand BorrarPersonaContactoCommand { get; private set; }
        private void OnBorrarPersonaContacto(PersonaContactoDTO persona)
        {
            PersonasContacto.Remove(persona);
        }
        public ICommand CrearClienteCommand { get; private set; }
        private async void OnCrearCliente()
        {
            ClienteCrear cliente = new ClienteCrear
            {
                Cliente = ClienteNumero,
                CodigoPostal = ClienteCodigoPostal,
                Direccion = ClienteDireccion,
                Empresa = ClienteEmpresa,
                EsContacto = ClienteEsContacto,
                Estado = ClienteEstado,
                Estetica = ClienteTieneEstetica,
                Peluqueria = ClienteTienePeluqueria,
                FormaPago = ClienteFormaPago,
                Iban = FormaPagoRecibo ? ClienteIban : "",
                Nif = ClienteNif,
                Nombre = ClienteNombre,
                PersonasContacto = PersonasContacto,
                PlazosPago = ClientePlazosPago,
                Poblacion = ClientePoblacion,
                Provincia = ClienteProvincia,
                Ruta = ClienteRuta,
                Telefono = ClienteTelefono,
                VendedorEstetica = ClienteVendedorEstetica,
                VendedorPeluqueria = ClienteVendedorPeluqueria,
                Usuario = Configuracion.usuario
            };

            try
            {
                if (EsUnaModificacion)
                {
                    cliente.Contacto = ClienteContacto;
                    Clientes clienteCreado = await Servicio.ModificarCliente(cliente);
                    if (clienteCreado != null)
                    {
                        DialogService.ShowNotification("Cliente Modificado", "Se ha modificado correctamente el cliente " + clienteCreado.Nº_Cliente.Trim() + "/" + clienteCreado.Contacto.Trim());
                        EventAggregator.GetEvent<ClienteModificadoEvent>().Publish(clienteCreado);
                    }
                } else
                {
                    Clientes clienteCreado = await Servicio.CrearCliente(cliente);
                    if (clienteCreado!=null)
                    {
                        DialogService.ShowNotification("Cliente Creado", "Se ha creado correctamente el cliente " + clienteCreado.Nº_Cliente.Trim() + "/" + clienteCreado.Contacto.Trim());
                    }
                }
                
                var view = RegionManager.Regions["MainRegion"].ActiveViews.FirstOrDefault();
                if (view != null)
                {
                    RegionManager.Regions["MainRegion"].Deactivate(view);
                    RegionManager.Regions["MainRegion"].Remove(view);
                }
            } catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }           
        }
        #endregion
        public ICommand LimpiarDireccionCommand { get; private set; }
        private void OnLimpiarDireccion()
        {
            ClienteDireccion = String.Empty;
        }

        public async new void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (!navigationContext.Parameters.Any())
            {
                return;
            }
            
            if (navigationContext.Parameters["empresaParameter"] != null &&
                navigationContext.Parameters["clienteParameter"] != null &&
                navigationContext.Parameters["contactoParameter"] != null)
            {
                string empresa = navigationContext.Parameters["empresaParameter"].ToString();
                string cliente = navigationContext.Parameters["clienteParameter"].ToString();
                string contacto = navigationContext.Parameters["contactoParameter"].ToString();
                EsUnaModificacion = true;

                ClienteCrear clienteCrear = await Servicio.LeerClienteCrear(empresa, cliente, contacto);

                Titulo = String.Format("Editar Cliente {1}/{2}", empresa, cliente, contacto);

                ClienteEmpresa = clienteCrear.Empresa;
                ClienteNumero = clienteCrear.Cliente;
                ClienteContacto = clienteCrear.Contacto;

                ClienteNif = clienteCrear.Nif;
                ClienteNombre = clienteCrear.Nombre;
                ClienteDireccion = clienteCrear.Direccion;
                ClienteCodigoPostal = clienteCrear.CodigoPostal;
                ClienteTelefono = clienteCrear.Telefono;
                ClienteVendedorEstetica = clienteCrear.VendedorEstetica;
                ClienteVendedorPeluqueria = clienteCrear.VendedorPeluqueria;
                ClienteTieneEstetica = clienteCrear.Estetica;
                ClienteTienePeluqueria = clienteCrear.Peluqueria;
                ClienteFormaPago = clienteCrear.FormaPago;
                ClientePlazosPago = clienteCrear.PlazosPago;
                FormaPagoEfectivo = clienteFormaPago == Constantes.FormasPago.EFECTIVO;
                FormaPagoTarjeta = clienteFormaPago == Constantes.FormasPago.TARJETA;
                FormaPagoRecibo = clienteFormaPago == Constantes.FormasPago.RECIBO;
                ClienteIban = clienteCrear.Iban;
                PersonasContacto = new ObservableCollection<PersonaContactoDTO>();
                foreach (var persona in clienteCrear.PersonasContacto)
                {
                    PersonasContacto.Add(persona);
                }
                await GoToDatosGenerales();
            }

            if (navigationContext.Parameters["nifParameter"] != null &&
                navigationContext.Parameters["nombreParameter"] != null)
            {
                ClienteNif = navigationContext.Parameters["nifParameter"].ToString();
                ClienteNombre = navigationContext.Parameters["nombreParameter"].ToString();
                await GoToDatosGenerales();
            }
        }

        private async Task GoToDatosComisiones()
        {
            if (clienteTelefono == "undefined")
            {
                clienteTelefono = string.Empty;
            }

            if (!string.IsNullOrEmpty(ClienteDireccion) && string.IsNullOrEmpty(clienteDireccionCalleNumero)) 
            {
                ClienteDireccionValidada = true;
                return;
            }

            try
            {
                RespuestaDatosGeneralesClientes respuesta = await Servicio.ValidarDatosGenerales(ClienteDireccionCalleNumero, ClienteCodigoPostal, ClienteTelefono);
                respuesta.ClientesMismoTelefono = respuesta.ClientesMismoTelefono.Where(c => c.Cliente != ClienteNumero).ToList();
                if (respuesta.ClientesMismoTelefono.Count > 0)
                {
                    DialogService.ShowDialog("NotificacionTelefonoView", new DialogParameters { { "clientesMismoTelefono", respuesta.ClientesMismoTelefono } }, null);
                }

                ClienteDireccion = respuesta.DireccionFormateada;

                if (!string.IsNullOrEmpty(ClienteDireccionAdicional))
                {
                    ClienteDireccion += ", " + ClienteDireccionAdicional.ToUpper();
                }
                ClienteDireccion = Strings.Left(ClienteDireccion, 50);
                ClientePoblacion = Strings.Left(respuesta.Poblacion, 30);
                ClienteProvincia = respuesta.Provincia;
                ClienteRuta = respuesta.Ruta;
                ClienteTelefono = respuesta.TelefonoFormateado;
                ClienteVendedorEstetica = respuesta.VendedorEstetica;
                ClienteVendedorPeluqueria = respuesta.VendedorPeluqueria;
                ClienteDireccionValidada = true;
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
        }
        private async Task GoToDatosGenerales()
        {
            if (NifValidado)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ClienteNif))
            {
                ClienteEstado = 5;
                ClienteNombre = ClienteNombre?.ToUpper().Trim();
                NifValidado = true;
                return;
            }

            if (!NombreIsEnabled)
            {
                ClienteNombre = "UNDEFINED";
            }

            try
            {
                RespuestaNifNombreCliente respuesta = await Servicio.ValidarNif(ClienteNif, ClienteNombre);
                if (respuesta.NifFormateado != "UNDEFINED")
                {
                    ClienteNif = respuesta.NifFormateado;
                }
                ClienteNombre = respuesta.NombreFormateado;
                ClienteEsContacto = respuesta.ExisteElCliente;
                if (respuesta.ExisteElCliente)
                {
                    ClienteEmpresa = respuesta.Empresa;
                    ClienteNumero = respuesta.NumeroCliente;
                    ClienteContacto = respuesta.Contacto;
                    if (respuesta.EstadoCliente < 0)
                    {
                        ClienteEstado = 0;
                    }
                }
                ClienteEstado = respuesta.EstadoCliente;
                NifValidado = respuesta.NifValidado;
                if (!NifValidado)
                {
                    DialogService.ShowError("El NIF " + ClienteNif + " no es válido (o no corresponde a " + ClienteNombre + ")");
                }
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
        }
        private async Task GoToDatosContacto()
        {
            try
            {
                ClienteFormaPago = FormaPagoRecibo ? Constantes.FormasPago.RECIBO : FormaPagoTarjeta ? Constantes.FormasPago.TARJETA : Constantes.FormasPago.EFECTIVO;
                ClientePlazosPago = FormaPagoTarjeta ? Constantes.PlazosPago.PREPAGO : Constantes.PlazosPago.CONTADO;
                RespuestaDatosBancoCliente respuesta = await Servicio.ValidarDatosPago(ClienteFormaPago, ClientePlazosPago, ClienteIban);
                ClienteIban = respuesta.IbanFormateado;
                if (!respuesta.DatosPagoValidos || (!respuesta.IbanValido && FormaPagoRecibo))
                {
                    ClienteDatosPagoValidados = false;
                    DialogService.ShowError("Datos de Pago no son válidos");
                    return;
                } else
                {
                    ClienteDatosPagoValidados = true;
                }

            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
        }

        public void CrearContacto(string cifNif, string nombre)
        {
            throw new NotImplementedException();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            
        }
    }
}
