using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Regions;
using Nesto.Contratos;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace Nesto.Modulos.Cliente
{
    public class CrearClienteViewModel: ViewModelBase
    {
        private const string DATOS_FISCALES = "DatosFiscales";
        private const string DATOS_GENERALES = "DatosGenerales";
        private const string DATOS_COMISIONES = "DatosComisiones";
        private const string DATOS_PAGO = "DatosPago";
        private const string DATOS_CONTACTO = "DatosContacto";
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }

        public CrearClienteViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo);

            Titulo = "Crear Cliente";
        }

        #region "Propiedades"
        private string clienteCodigoPostal;
        public string ClienteCodigoPostal {
            get { return clienteCodigoPostal; }
            set { SetProperty(ref clienteCodigoPostal, value); }
        }
        private string clienteDireccion;
        public string ClienteDireccion {
            get { return clienteDireccion; }
            set { SetProperty(ref clienteDireccion, value); }
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
        private string clienteTelefono;
        public string ClienteTelefono {
            get { return clienteTelefono; }
            set { SetProperty(ref clienteTelefono, value); }
        }
        public bool EstaOcupado { get; set; }
        public bool NifValidado { get; set; } = false;
        public bool NifSinValidar
        {
            get { return !NifValidado; }
        }
        public bool NombreIsEnabled
        {
            get
            {
                return !(!NifValidado && !string.IsNullOrEmpty(ClienteNif) && !"0123456789YX".Contains(ClienteNif.ToUpper()[0]));
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
                    GoToDatosGenerales();
                } else if (paginaActual?.Name == DATOS_GENERALES && value?.Name == DATOS_COMISIONES)
                {
                    GoToDatosComisiones();
                }
                SetProperty(ref paginaActual, value);
            }
        }

        private void GoToDatosComisiones()
        {
            //throw new NotImplementedException();
        }

        private void GoToDatosGenerales()
        {
            //throw new NotImplementedException();
        }

        public bool SePuedeAvanzarADatosGenerales
        {
            get
            {
                return !(string.IsNullOrEmpty(ClienteNif) && string.IsNullOrEmpty(ClienteNombre));
            }
        }
        #endregion

        #region "Comandos"
        public ICommand AbrirModuloCommand { get; private set; }

        private void OnAbrirModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "CrearClienteView");
        }
        #endregion
    }
}
