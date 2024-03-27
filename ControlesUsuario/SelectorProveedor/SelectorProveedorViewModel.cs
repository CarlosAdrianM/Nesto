using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ControlesUsuario.Models.SelectorProveedorModel;

namespace ControlesUsuario.ViewModels
{
    public class SelectorProveedorViewModel : ViewModelBase
    {
        private readonly ISelectorProveedorService _servicio;

        public SelectorProveedorViewModel(ISelectorProveedorService servicio)
        {
            _servicio = servicio;

            ListaProveedores = new()
            {
                VaciarAlSeleccionar = true
            };
        }

        #region Propiedades

        private bool _estaCargando;
        public bool EstaCargando
        {
            get => _estaCargando;
            set => SetProperty(ref _estaCargando, value);
        }

        private ColeccionFiltrable _listaProveedores;
        public ColeccionFiltrable ListaProveedores
        {
            get => _listaProveedores;
            set
            {
                SetProperty(ref _listaProveedores, value);
                RaisePropertyChanged(nameof(VisibilidadListaProveedores));
            }
        }

        public bool VisibilidadListaProveedores
        {
            get => !(ListaProveedores is null || ListaProveedores.Lista is null || !ListaProveedores.Lista.Any());
        }



        #endregion

        #region Funciones auxiliares

        public void ActualizarPropertyChanged()
        {
            RaisePropertyChanged(string.Empty);
        }

        private async Task BuscarProveedores(string empresa, string filtro)
        {
            if (ListaProveedores == null || empresa == null)
            {
                return;
            }
                        
            if (string.IsNullOrEmpty(filtro))
            {
                return;
            }
            
            try
            {
                EstaCargando = true;
                var listaDevuelta = await _servicio.BuscarProveedores(empresa, filtro);
                if (listaDevuelta != null)
                {
                    ListaProveedores.Lista = new ObservableCollection<IFiltrableItem>(listaDevuelta);
                    RaisePropertyChanged(nameof(VisibilidadListaProveedores));
                }
                else
                {
                    if (ListaProveedores.ListaOriginal == null || !ListaProveedores.ListaOriginal.Any())
                    {
                        ListaProveedores.FiltrosPuestos.Clear();
                    }
                }
            }
            catch
            {
                throw new Exception("No se encontró ningún proveedor con el texto " + ListaProveedores.Filtro);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        public async Task CargarDatos(string empresa, string filtro, string contacto)
        {
            if (filtro == null || empresa == null)
            {
                return;
            }
            string proveedor = ListaProveedores.ElementoSeleccionado != null && ListaProveedores.Lista.Any() ? (ListaProveedores.ElementoSeleccionado as ProveedorDTO).Proveedor : filtro;
            ListaProveedores.Lista = new();

            try
            {
                ProveedorDTO proveedorLeido = await _servicio.CargarProveedor(empresa, proveedor, contacto);

                if (proveedorLeido != null)
                {
                    if ((ListaProveedores.ElementoSeleccionado as ProveedorDTO)?.Proveedor == proveedorLeido.Proveedor &&
                    (ListaProveedores.ElementoSeleccionado as ProveedorDTO)?.Contacto == proveedorLeido.Contacto)
                    {
                        return;
                    }
                    ListaProveedores.ElementoSeleccionado = proveedorLeido;                    
                }
                else
                {
                    if (filtro == (ListaProveedores.ElementoSeleccionado as ProveedorDTO)?.Proveedor)
                    {
                        return; // se ha buscado un proveedor que no existe
                    }
                    await BuscarProveedores(empresa, filtro);
                    //}                    
                }
            }
            catch (Exception)
            {
                await BuscarProveedores(empresa, filtro);
            }
        }
        #endregion
    }
}
