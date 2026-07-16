using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.ViewModels
{
    public class SelectorClienteViewModel : BindableBase
    {
        
        private readonly ISelectorClienteService Servicio;
        public SelectorClienteViewModel(IConfiguracion configuracion, ISelectorClienteService servicio)
        {
            Configuracion = configuracion;
            Servicio = servicio;

            listaClientes = new()
            {
                VaciarAlSeleccionar = true
            };
        }

        private string empresaPorDefecto = "1";
        private string vendedor;
        private string _empresaParametros;
        private bool _preferenciasCargadas;
        
        #region "Propiedades"
        private bool _cargando;
        public bool cargando
        {
            get { return _cargando; }
            set
            {
                if (_cargando != value)
                {
                    _cargando = value;
                    RaisePropertyChanged(nameof(cargando));
                    RaisePropertyChanged(nameof(visibilidadCargando));
                }
            }
        }
        public IConfiguracion Configuracion { get; set; }

        //private string _contactoSeleccionado;
        //public string contactoSeleccionado
        //{
        //    get
        //    {
        //        return _contactoSeleccionado;
        //    }
        //    set
        //    {
        //        if (SetProperty(ref _contactoSeleccionado, value))
        //        {
        //            if (listaClientes != null && listaClientes.ElementoSeleccionado != null && contactoSeleccionado != null && (listaClientes.ElementoSeleccionado as ClienteDTO).contacto.Trim() != contactoSeleccionado.Trim())
        //            {
        //                // Esto no debería ejecutarlo si estamos cambiando de cliente (o cargando cliente)
        //                (listaClientes.ElementoSeleccionado as ClienteDTO).contacto = contactoSeleccionado;
        //                cargarCliente((listaClientes.ElementoSeleccionado as ClienteDTO).empresa, (listaClientes.ElementoSeleccionado as ClienteDTO).cliente, contactoSeleccionado);
        //            }
        //        }                
        //    }
        //}

        public void ActualizarPropertyChanged()
        {
            RaisePropertyChanged(string.Empty);
        }

        private ColeccionFiltrable _listaClientes;
        public ColeccionFiltrable listaClientes
        {
            get
            {
                return _listaClientes;
            }
            set
            {
                _listaClientes = value;
                RaisePropertyChanged(nameof(listaClientes));
                RaisePropertyChanged(nameof(visibilidadListaClientes));
            }
        }

        public bool visibilidadCargando
        {
            get
            {
                return cargando;
            }
        }
        public bool visibilidadDatosCliente
        {
            get
            {
                return listaClientes?.ElementoSeleccionado != null;
            }
        }
        public bool visibilidadListaClientes
        {
            get
            {
                return !(listaClientes == null || listaClientes.Lista == null || !listaClientes.Lista.Any());
            }
        }

        private bool _visibilidadSelectorEntrega = false;
        public bool visibilidadSelectorEntrega
        {
            get
            {
                return _visibilidadSelectorEntrega;
            }
            set
            {
                _visibilidadSelectorEntrega = value;
                RaisePropertyChanged(nameof(visibilidadSelectorEntrega));
                RaisePropertyChanged(nameof(MostrarBadgeContactos));
            }
        }

        // Nesto#388: preferencia por usuario (parámetro SelectorClienteContactosExpandidos).
        // true = el panel de contactos se abre solo al seleccionar un cliente.
        private bool _contactosExpandidosPorDefecto;
        public bool ContactosExpandidosPorDefecto
        {
            get => _contactosExpandidosPorDefecto;
            set
            {
                if (SetProperty(ref _contactosExpandidosPorDefecto, value) && _preferenciasCargadas)
                {
                    // Solo persiste cuando lo cambia el usuario (la carga inicial no debe reescribirlo)
                    _ = Configuracion.GuardarParametro(_empresaParametros ?? empresaPorDefecto,
                        Parametros.Claves.SelectorClienteContactosExpandidos, value ? "1" : "0");
                    if (value && listaClientes?.ElementoSeleccionado != null)
                    {
                        visibilidadSelectorEntrega = true;
                    }
                }
            }
        }

        // Nesto#388: nº de contactos/direcciones que se verían al desplegar (badge del header).
        private int _numeroContactos;
        public int NumeroContactos
        {
            get => _numeroContactos;
            set
            {
                if (SetProperty(ref _numeroContactos, value))
                {
                    RaisePropertyChanged(nameof(MostrarBadgeContactos));
                }
            }
        }

        // Badge discreto solo cuando el panel está colapsado (desplegado ya se ven los contactos).
        public bool MostrarBadgeContactos => NumeroContactos > 0 && !visibilidadSelectorEntrega && visibilidadDatosCliente;
        #endregion


        #region "Funciones Auxiliares"

        public async Task CargarVendedor(string empresa)
        {
            if (empresa != null)
            {
                vendedor = await Configuracion.leerParametro(empresa, "Vendedor");
            }
            else
            {
                vendedor = await Configuracion.leerParametro(empresaPorDefecto, "Vendedor");
            }
        }

        // Nesto#388: lee la preferencia del usuario ANTES de que se pinte el estado inicial del
        // panel de contactos. Si ya había un cliente seleccionado (carga por binding más rápida
        // que la lectura del parámetro), aplica el estado ahora.
        public async Task CargarPreferencias(string empresa)
        {
            _empresaParametros = empresa ?? empresaPorDefecto;
            string valor = await Configuracion.leerParametro(_empresaParametros,
                Parametros.Claves.SelectorClienteContactosExpandidos);
            _contactosExpandidosPorDefecto = valor?.Trim() == "1";
            RaisePropertyChanged(nameof(ContactosExpandidosPorDefecto));
            _preferenciasCargadas = true;
            if (_contactosExpandidosPorDefecto && listaClientes?.ElementoSeleccionado != null)
            {
                visibilidadSelectorEntrega = true;
            }
        }
        private async Task buscarClientes(string empresa, string filtro)
        {
            if (listaClientes == null || empresa == null || Configuracion == null)
            {
                return;
            }

            //listaClientes.ElementoSeleccionado = null;

            if (string.IsNullOrEmpty(filtro))
            {
                return;
            }
            try
            {
                mostrarCargando(true);
                var listaDevuelta = await Servicio.BuscarClientes(empresa, vendedor, filtro);
                if (listaDevuelta != null)
                {
                    listaClientes.ListaFijada = new ObservableCollection<IFiltrableItem>(listaDevuelta);
                    RaisePropertyChanged(nameof(visibilidadListaClientes));
                }
                else
                {
                    if (listaClientes.ListaOriginal == null || !listaClientes.ListaOriginal.Any())
                    {
                        listaClientes.FiltrosPuestos.Clear();
                    }
                }
            }
            catch
            {
                throw new Exception("No se encontró ningún cliente con el texto " + listaClientes.Filtro);
            }
            finally
            {
                mostrarCargando(false);
            }
        }

        public async void cargarCliente(string empresa, string filtro, string contactoSeleccionado)
        {
            if (filtro == null || empresa == null || Configuracion == null)
            {
                return;
            }
            visibilidadSelectorEntrega = false;
            string cliente = listaClientes.ElementoSeleccionado != null && listaClientes.Lista.Any() ? (listaClientes.ElementoSeleccionado as ClienteDTO).cliente : filtro;
            listaClientes.Lista = new();

            try
            {
                //if (listaClientes.ElementoSeleccionado != null && (listaClientes.ElementoSeleccionado as ClienteDTO).cliente.Trim() != cliente.Trim())
                //{
                //    contactoSeleccionado = null;
                //}

                ClienteDTO clienteLeido = await Servicio.CargarCliente(empresa, cliente, contactoSeleccionado);

                if (clienteLeido != null)
                {
                    if ((listaClientes.ElementoSeleccionado as ClienteDTO)?.cliente == clienteLeido.cliente &&
                    (listaClientes.ElementoSeleccionado as ClienteDTO)?.contacto == clienteLeido.contacto)
                    {
                        AbrirContactosSiProcede();
                        return;
                    }
                    listaClientes.ElementoSeleccionado = clienteLeido;
                    //(listaClientes.ElementoSeleccionado as ClienteDTO).contacto = contactoSeleccionado;
                    //this.contactoSeleccionado = clienteLeido.contacto;
                    AbrirContactosSiProcede();
                }
                else
                {
                    //ClienteDTO clienteLeidoSinContacto = await Servicio.CargarCliente(empresa, cliente, null);

                    //if (clienteLeidoSinContacto != null)
                    //{
                    //    if ((listaClientes.ElementoSeleccionado as ClienteDTO)?.cliente == clienteLeidoSinContacto.cliente &&
                    //    (listaClientes.ElementoSeleccionado as ClienteDTO)?.contacto == clienteLeidoSinContacto.contacto)
                    //    {
                    //        return;
                    //    }
                    //    listaClientes.ElementoSeleccionado = clienteLeidoSinContacto;
                    //    //(listaClientes.ElementoSeleccionado as ClienteDTO).contacto = contactoSeleccionado;
                    //    //this.contactoSeleccionado = clienteLeido.contacto;
                    //}
                    //else
                    //{
                    if (filtro == (listaClientes.ElementoSeleccionado as ClienteDTO)?.cliente)
                    {
                        return; // se ha buscado un cliente que no existe
                    }
                    await buscarClientes(empresa, filtro);
                    //}                    
                }
            }
            catch (Exception)
            {
                await buscarClientes(empresa, filtro);
            }
        }

        // Nesto#388: con la preferencia "desplegado por defecto", el panel de contactos se abre
        // solo al quedar un cliente seleccionado (cargarCliente lo cierra al empezar a cambiar).
        private void AbrirContactosSiProcede()
        {
            if (ContactosExpandidosPorDefecto)
            {
                visibilidadSelectorEntrega = true;
            }
        }

        // Si creamos la propiedad Cargando, entonces se podría usar este método para mostrar/ocultar el cargando
        private void mostrarCargando(bool estado)
        {
            cargando = estado;
            RaisePropertyChanged(nameof(cargando));
        }




        #endregion


    }
}
