using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using Prism.Events;
using Prism.Regions;
using Microsoft.VisualBasic;
using Nesto.Contratos;
using Nesto.Models.Nesto.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace Nesto.Modulos.Cliente
{
    public class CrearClienteViewModel: ViewModelBase, INavigationAware
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

        public CrearClienteViewModel(IRegionManager regionManager, IConfiguracion configuracion, IClienteService servicio, IEventAggregator eventAggregator)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            Servicio = servicio;
            EventAggregator = eventAggregator;

            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo);
            AnnadirPersonaContactoCommand = new DelegateCommand(OnAnnadirPersonaContacto);
            BorrarPersonaContactoCommand = new DelegateCommand<PersonaContactoDTO>(OnBorrarPersonaContacto);
            CrearClienteCommand = new DelegateCommand(OnCrearCliente);

            Titulo = "Crear Cliente";
            PersonasContacto = new ObservableCollection<PersonaContactoDTO>()
            {
                new PersonaContactoDTO()
            };

            NotificationRequest = new InteractionRequest<INotification>();
            ClienteTelefonoRequest = new InteractionRequest<INotification>();
        }

        #region "Propiedades Prism"
        public InteractionRequest<INotification> NotificationRequest { get; private set; }
        public InteractionRequest<INotification> ClienteTelefonoRequest { get; private set; }
        #endregion

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
            get { return clienteDireccion; }
            set { SetProperty(ref clienteDireccion, value); }
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
            get { return clienteEsContacto; }
            set { SetProperty(ref clienteEsContacto, value); }
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
                OnPropertyChanged(()=>NombreIsEnabled);
                OnPropertyChanged(() => SePuedeAvanzarADatosGenerales);
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
                OnPropertyChanged(() => SePuedeAvanzarADatosGenerales);
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
                OnPropertyChanged(() => SePuedeAvanzarADatosPago);
            }
        }
        private bool clienteTienePeluqueria;
        public bool ClienteTienePeluqueria {
            get { return clienteTienePeluqueria; }
            set {
                SetProperty(ref clienteTienePeluqueria, value);
                OnPropertyChanged(() => SePuedeAvanzarADatosPago);
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
                OnPropertyChanged(() => VendedorPeluqueriaMostrar);
            }
        }
        public bool EstaOcupado { get; set; }
        private bool EsUnaModificacion { get; set; } = false;
        private bool formaPagoEfectivo = true;
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
        private bool nifValidado = false;
        public bool NifValidado {
            get { return nifValidado; }
            set {
                SetProperty(ref nifValidado, value);
                OnPropertyChanged(() => NifSinValidar);
                OnPropertyChanged(() => NombreIsEnabled);
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
                            SetProperty(ref paginaActual, value);
                        }
                    });
                    
                } else if (paginaActual?.Name == DATOS_GENERALES && value?.Name == DATOS_COMISIONES)
                {
                    Task a = GoToDatosComisiones();
                    a.ContinueWith((t1) => {
                        if (ClienteDireccionValidada)
                        {
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
                            SetProperty(ref paginaActual, value);
                        }
                    });
                }
                else
                {
                    SetProperty(ref paginaActual, value);
                }
                
            }
        }
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
                    NotificationRequest.Raise(new Notification { Content = "Se ha modificado correctamente el cliente " + clienteCreado.Nº_Cliente.Trim() + "/" + clienteCreado.Contacto.Trim(), Title = "Cliente Modificado" });
                    EventAggregator.GetEvent<ClienteModificadoEvent>().Publish(clienteCreado);
                } else
                {
                    Clientes clienteCreado = await Servicio.CrearCliente(cliente);
                    NotificationRequest.Raise(new Notification { Content = "Se ha creado correctamente el cliente " + clienteCreado.Nº_Cliente.Trim() + "/" + clienteCreado.Contacto.Trim(), Title = "Cliente Creado" });
                }
                
                var view = RegionManager.Regions["MainRegion"].ActiveViews.FirstOrDefault();
                if (view != null)
                {
                    RegionManager.Regions["MainRegion"].Deactivate(view);
                    RegionManager.Regions["MainRegion"].Remove(view);
                }
            } catch (Exception ex)
            {
                NotificationRequest.Raise(new Notification { Content = ex.Message, Title = "Error" });
            }           
        }
        #endregion


        public async new void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (navigationContext.Parameters.Count() == 0)
            {
                return;
            }
            string empresa = navigationContext.Parameters["empresaParameter"].ToString();
            string cliente = navigationContext.Parameters["clienteParameter"].ToString();
            string contacto = navigationContext.Parameters["contactoParameter"].ToString();

            ClienteCrear clienteCrear = await Servicio.LeerClienteCrear(empresa, cliente, contacto);

            Titulo = String.Format("Editar Cliente {1}/{2}", empresa, cliente, contacto);

            EsUnaModificacion = true;

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
            ClienteIban = clienteCrear.Iban;
            PersonasContacto = new ObservableCollection<PersonaContactoDTO>();
            foreach (var persona in clienteCrear.PersonasContacto)
            {
                PersonasContacto.Add(persona);
            }
        }

        private async Task GoToDatosComisiones()
        {
            if (clienteTelefono == "undefined")
            {
                clienteTelefono = string.Empty;
            }

            try
            {
                RespuestaDatosGeneralesClientes respuesta = await Servicio.ValidarDatosGenerales(ClienteDireccionCalleNumero, ClienteCodigoPostal, ClienteTelefono);
                respuesta.ClientesMismoTelefono = respuesta.ClientesMismoTelefono.Where(c => c.Cliente != ClienteNumero).ToList();
                if (respuesta.ClientesMismoTelefono.Count > 0)
                {
                    ClienteTelefonoRequest.Raise(new Notification { Content = respuesta.ClientesMismoTelefono, Title = "Clientes con el mismo teléfono:" });
                }

                ClienteDireccion = respuesta.DireccionFormateada;

                if (!string.IsNullOrEmpty(ClienteDireccionAdicional))
                {
                    ClienteDireccion += ", " + ClienteDireccionAdicional.ToUpper();
                }
                ClienteDireccion = Strings.Left(ClienteDireccion, 50);
                ClientePoblacion = respuesta.Poblacion;
                ClienteProvincia = respuesta.Provincia;
                ClienteRuta = respuesta.Ruta;
                ClienteTelefono = respuesta.TelefonoFormateado;
                ClienteVendedorEstetica = respuesta.VendedorEstetica;
                ClienteVendedorPeluqueria = respuesta.VendedorPeluqueria;
                ClienteDireccionValidada = true;
            }
            catch (Exception ex)
            {
                NotificationRequest.Raise(new Notification { Content = ex.Message, Title = "Error" });
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
                    NotificationRequest.Raise(new Notification { Content = "El NIF "+ClienteNif+ " no es válido (o no corresponde a " + ClienteNombre + ")" , Title = "Error" });
                }
            }
            catch (Exception ex)
            {
                NotificationRequest.Raise(new Notification { Content = ex.Message, Title = "Error" });
            }
        }
        private async Task GoToDatosContacto()
        {
            try
            {
                ClienteFormaPago = FormaPagoRecibo ? "RCB" : "EFC";
                ClientePlazosPago = "CONTADO";
                RespuestaDatosBancoCliente respuesta = await Servicio.ValidarDatosPago(ClienteFormaPago, ClientePlazosPago, ClienteIban);
                ClienteIban = respuesta.IbanFormateado;
                if (!respuesta.DatosPagoValidos || (!respuesta.IbanValido && FormaPagoRecibo))
                {
                    ClienteDatosPagoValidados = false;
                    NotificationRequest.Raise(new Notification { Content = "Datos de Pago no son válidos", Title = "Error" });
                    return;
                } else
                {
                    ClienteDatosPagoValidados = true;
                }

            }
            catch (Exception ex)
            {
                NotificationRequest.Raise(new Notification { Content = ex.Message, Title = "Error" });
            }
        }
    }
}
