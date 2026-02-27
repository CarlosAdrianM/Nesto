using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.OfertasCombinadas.ViewModels
{
    public delegate void NuevaOfertaCombinadaCreadaHandler(OfertaCombinadaWrapper nuevoItem);
    public delegate void NuevaOfertaFamiliaCreadaHandler(OfertaPermitidaFamiliaWrapper nuevoItem);

    public class OfertasCombinadasViewModel : ViewModelBase
    {
        private readonly IOfertasCombinadasService _service;
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        private readonly IRegionManager _regionManager;

        public OfertasCombinadasViewModel(IOfertasCombinadasService service, IConfiguracion configuracion, IDialogService dialogService, IRegionManager regionManager)
        {
            _service = service;
            _configuracion = configuracion;
            _dialogService = dialogService;
            _regionManager = regionManager;

            OfertasCombinadas = new ObservableCollection<OfertaCombinadaWrapper>();
            OfertasFamilia = new ObservableCollection<OfertaPermitidaFamiliaWrapper>();

            CargarCommand = new DelegateCommand(async () => await OnCargar());
            NuevaOfertaCombinadaCommand = new DelegateCommand(OnNuevaOfertaCombinada);
            GuardarOfertaCombinadaCommand = new DelegateCommand<object>(async (o) => await OnGuardarOfertaCombinada(o as OfertaCombinadaWrapper));
            EliminarOfertaCombinadaCommand = new DelegateCommand<object>(async (o) => await OnEliminarOfertaCombinada(o as OfertaCombinadaWrapper));

            NuevaOfertaFamiliaCommand = new DelegateCommand(OnNuevaOfertaFamilia);
            GuardarOfertaFamiliaCommand = new DelegateCommand<object>(async (o) => await OnGuardarOfertaFamilia(o as OfertaPermitidaFamiliaWrapper));
            EliminarOfertaFamiliaCommand = new DelegateCommand<object>(async (o) => await OnEliminarOfertaFamilia(o as OfertaPermitidaFamiliaWrapper));

            NuevoDetalleCommand = new DelegateCommand(OnNuevoDetalle, () => OfertaCombinadaSeleccionada != null);
            EliminarDetalleCommand = new DelegateCommand<object>(OnEliminarDetalle);

            Titulo = "Ofertas Combinadas";
            Empresa = Constantes.Empresas.EMPRESA_DEFECTO;
            _soloActivas = true;

            _ = CargarDatosIniciales();
        }

        private async Task CargarDatosIniciales()
        {
            await OnCargar(mostrarConfirmacion: false);
        }

        #region Propiedades

        private string _empresa;
        public string Empresa
        {
            get => _empresa;
            set => SetProperty(ref _empresa, value);
        }

        private bool _estaCargando;
        public bool EstaCargando
        {
            get => _estaCargando;
            set => SetProperty(ref _estaCargando, value);
        }

        private bool _soloActivas;
        public bool SoloActivas
        {
            get => _soloActivas;
            set
            {
                if (SetProperty(ref _soloActivas, value))
                {
                    _ = OnCargar(mostrarConfirmacion: true);
                }
            }
        }

        // Ofertas Combinadas (tab 1)
        private ObservableCollection<OfertaCombinadaWrapper> _ofertasCombinadas;
        public ObservableCollection<OfertaCombinadaWrapper> OfertasCombinadas
        {
            get => _ofertasCombinadas;
            set => SetProperty(ref _ofertasCombinadas, value);
        }

        private OfertaCombinadaWrapper _ofertaCombinadaSeleccionada;
        public OfertaCombinadaWrapper OfertaCombinadaSeleccionada
        {
            get => _ofertaCombinadaSeleccionada;
            set
            {
                if (SetProperty(ref _ofertaCombinadaSeleccionada, value))
                {
                    ((DelegateCommand)NuevoDetalleCommand).RaiseCanExecuteChanged();
                    CargarDetalles();
                }
            }
        }

        private ObservableCollection<DetalleOfertaCombinadaWrapper> _detallesOfertaSeleccionada;
        public ObservableCollection<DetalleOfertaCombinadaWrapper> DetallesOfertaSeleccionada
        {
            get => _detallesOfertaSeleccionada;
            set => SetProperty(ref _detallesOfertaSeleccionada, value);
        }

        // Ofertas por Familia (tab 2)
        private ObservableCollection<OfertaPermitidaFamiliaWrapper> _ofertasFamilia;
        public ObservableCollection<OfertaPermitidaFamiliaWrapper> OfertasFamilia
        {
            get => _ofertasFamilia;
            set => SetProperty(ref _ofertasFamilia, value);
        }

        #endregion

        #region Comandos

        public ICommand CargarCommand { get; }
        public ICommand NuevaOfertaCombinadaCommand { get; }
        public ICommand GuardarOfertaCombinadaCommand { get; }
        public ICommand EliminarOfertaCombinadaCommand { get; }
        public ICommand NuevoDetalleCommand { get; }
        public ICommand EliminarDetalleCommand { get; }

        public ICommand NuevaOfertaFamiliaCommand { get; }
        public ICommand GuardarOfertaFamiliaCommand { get; }
        public ICommand EliminarOfertaFamiliaCommand { get; }

        public event NuevaOfertaCombinadaCreadaHandler NuevaOfertaCombinadaCreada;
        public event NuevaOfertaFamiliaCreadaHandler NuevaOfertaFamiliaCreada;

        #endregion

        #region Ofertas Combinadas

        private async Task OnCargar(bool mostrarConfirmacion = true)
        {
            if (mostrarConfirmacion && TieneCambiosSinGuardar())
            {
                var confirmacion = _dialogService.ShowConfirmationAnswer(
                    "Cambios sin guardar",
                    "Hay cambios sin guardar que se perderan. Desea continuar?");
                if (!confirmacion) return;
            }

            try
            {
                EstaCargando = true;
                OfertasCombinadas.Clear();
                OfertasFamilia.Clear();
                DetallesOfertaSeleccionada = null;

                var ofertas = await _service.GetOfertasCombinadas(Empresa, SoloActivas);
                foreach (var item in ofertas.OrderByDescending(o => o.Id))
                {
                    OfertasCombinadas.Add(new OfertaCombinadaWrapper(item));
                }

                var ofertasFamilia = await _service.GetOfertasPermitidasFamilia(Empresa);
                foreach (var item in ofertasFamilia.OrderBy(o => o.Familia).ThenBy(o => o.FiltroProducto))
                {
                    OfertasFamilia.Add(new OfertaPermitidaFamiliaWrapper(item));
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private bool TieneCambiosSinGuardar()
        {
            return OfertasCombinadas.Any(o => o.HaCambiado || o.Id == 0)
                || OfertasFamilia.Any(o => o.HaCambiado || o.NOrden == 0);
        }

        private void OnNuevaOfertaCombinada()
        {
            var nuevo = new OfertaCombinadaWrapper();
            OfertasCombinadas.Add(nuevo);
            OfertaCombinadaSeleccionada = nuevo;
            NuevaOfertaCombinadaCreada?.Invoke(nuevo);
        }

        private void CargarDetalles()
        {
            if (OfertaCombinadaSeleccionada == null)
            {
                DetallesOfertaSeleccionada = null;
                return;
            }
            DetallesOfertaSeleccionada = OfertaCombinadaSeleccionada.Detalles;
        }

        private void OnNuevoDetalle()
        {
            if (OfertaCombinadaSeleccionada == null) return;

            var detalle = new DetalleOfertaCombinadaWrapper();
            OfertaCombinadaSeleccionada.Detalles.Add(detalle);
            OfertaCombinadaSeleccionada.HaCambiado = true;
        }

        private void OnEliminarDetalle(object parameter)
        {
            if (parameter is not DetalleOfertaCombinadaWrapper detalle) return;
            if (OfertaCombinadaSeleccionada == null) return;

            OfertaCombinadaSeleccionada.Detalles.Remove(detalle);
            OfertaCombinadaSeleccionada.HaCambiado = true;
        }

        private async Task OnGuardarOfertaCombinada(OfertaCombinadaWrapper oferta)
        {
            if (oferta == null) return;

            if (string.IsNullOrWhiteSpace(oferta.Nombre))
            {
                _dialogService.ShowError("Debe introducir un nombre para la oferta.");
                return;
            }

            if (oferta.Detalles.Count < 2)
            {
                _dialogService.ShowError("Una oferta combinada debe tener al menos 2 productos.");
                return;
            }

            try
            {
                EstaCargando = true;

                var createModel = new OfertaCombinadaCreateModel
                {
                    Empresa = Empresa,
                    Nombre = oferta.Nombre,
                    ImporteMinimo = oferta.ImporteMinimo,
                    FechaDesde = oferta.FechaDesde,
                    FechaHasta = oferta.FechaHasta,
                    Detalles = oferta.Detalles.Select(d => new OfertaCombinadaDetalleCreateModel
                    {
                        Id = d.Id,
                        Producto = d.Producto?.Trim(),
                        Cantidad = d.Cantidad,
                        Precio = d.Precio
                    }).ToList()
                };

                OfertaCombinadaModel resultado;
                if (oferta.Id == 0)
                {
                    resultado = await _service.CreateOfertaCombinada(createModel);
                    _dialogService.ShowNotification($"Oferta combinada '{resultado.Nombre}' creada");
                }
                else
                {
                    resultado = await _service.UpdateOfertaCombinada(oferta.Id, createModel);
                    _dialogService.ShowNotification($"Oferta combinada '{resultado.Nombre}' actualizada");
                }

                oferta.ActualizarDesdeServidor(resultado);
                CargarDetalles();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task OnEliminarOfertaCombinada(OfertaCombinadaWrapper oferta)
        {
            if (oferta == null) return;

            if (oferta.Id == 0)
            {
                OfertasCombinadas.Remove(oferta);
                return;
            }

            var confirmacion = _dialogService.ShowConfirmationAnswer(
                "Eliminar oferta combinada",
                $"Se eliminara la oferta '{oferta.Nombre}' y todos sus productos. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                await _service.DeleteOfertaCombinada(oferta.Id);
                OfertasCombinadas.Remove(oferta);
                _dialogService.ShowNotification("Oferta combinada eliminada");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion

        #region Ofertas por Familia

        private void OnNuevaOfertaFamilia()
        {
            var nuevo = new OfertaPermitidaFamiliaWrapper { CantidadConPrecio = 6, CantidadRegalo = 1 };
            OfertasFamilia.Add(nuevo);
            NuevaOfertaFamiliaCreada?.Invoke(nuevo);
        }

        private async Task OnGuardarOfertaFamilia(OfertaPermitidaFamiliaWrapper oferta)
        {
            if (oferta == null) return;

            if (string.IsNullOrWhiteSpace(oferta.Familia))
            {
                _dialogService.ShowError("Debe introducir una familia.");
                return;
            }

            try
            {
                EstaCargando = true;

                var createModel = new OfertaPermitidaFamiliaCreateModel
                {
                    Empresa = Empresa,
                    Familia = oferta.Familia?.Trim(),
                    CantidadConPrecio = oferta.CantidadConPrecio,
                    CantidadRegalo = oferta.CantidadRegalo,
                    FiltroProducto = oferta.FiltroProducto
                };

                OfertaPermitidaFamiliaModel resultado;
                if (oferta.NOrden == 0)
                {
                    resultado = await _service.CreateOfertaPermitidaFamilia(createModel);
                    _dialogService.ShowNotification($"Oferta por familia '{resultado.Familia}' creada");
                }
                else
                {
                    resultado = await _service.UpdateOfertaPermitidaFamilia(oferta.NOrden, createModel);
                    _dialogService.ShowNotification($"Oferta por familia '{resultado.Familia}' actualizada");
                }

                oferta.ActualizarDesdeServidor(resultado);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task OnEliminarOfertaFamilia(OfertaPermitidaFamiliaWrapper oferta)
        {
            if (oferta == null) return;

            if (oferta.NOrden == 0)
            {
                OfertasFamilia.Remove(oferta);
                return;
            }

            var confirmacion = _dialogService.ShowConfirmationAnswer(
                "Eliminar oferta por familia",
                $"Se eliminara la oferta de familia '{oferta.Familia}'. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                await _service.DeleteOfertaPermitidaFamilia(oferta.NOrden);
                OfertasFamilia.Remove(oferta);
                _dialogService.ShowNotification("Oferta por familia eliminada");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion
    }

    #region Wrappers

    public class OfertaCombinadaWrapper : BindableBase
    {
        private bool _rastreandoCambios = true;

        public OfertaCombinadaWrapper()
        {
            Detalles = new ObservableCollection<DetalleOfertaCombinadaWrapper>();
        }

        public OfertaCombinadaWrapper(OfertaCombinadaModel model)
        {
            _rastreandoCambios = false;
            Id = model.Id;
            Nombre = model.Nombre;
            ImporteMinimo = model.ImporteMinimo;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            Detalles = new ObservableCollection<DetalleOfertaCombinadaWrapper>(
                (model.Detalles ?? new List<OfertaCombinadaDetalleModel>())
                    .Select(d => new DetalleOfertaCombinadaWrapper(d)));
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public void ActualizarDesdeServidor(OfertaCombinadaModel model)
        {
            _rastreandoCambios = false;
            Id = model.Id;
            Nombre = model.Nombre;
            ImporteMinimo = model.ImporteMinimo;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            Detalles = new ObservableCollection<DetalleOfertaCombinadaWrapper>(
                (model.Detalles ?? new List<OfertaCombinadaDetalleModel>())
                    .Select(d => new DetalleOfertaCombinadaWrapper(d)));
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public int Id { get; set; }

        private string _nombre;
        public string Nombre
        {
            get => _nombre;
            set { if (SetProperty(ref _nombre, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private decimal _importeMinimo;
        public decimal ImporteMinimo
        {
            get => _importeMinimo;
            set { if (SetProperty(ref _importeMinimo, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaDesde;
        public DateTime? FechaDesde
        {
            get => _fechaDesde;
            set { if (SetProperty(ref _fechaDesde, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaHasta;
        public DateTime? FechaHasta
        {
            get => _fechaHasta;
            set { if (SetProperty(ref _fechaHasta, value) && _rastreandoCambios) HaCambiado = true; }
        }

        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }

        private ObservableCollection<DetalleOfertaCombinadaWrapper> _detalles;
        public ObservableCollection<DetalleOfertaCombinadaWrapper> Detalles
        {
            get => _detalles;
            set => SetProperty(ref _detalles, value);
        }

        private bool _haCambiado;
        public bool HaCambiado
        {
            get => _haCambiado;
            set => SetProperty(ref _haCambiado, value);
        }
    }

    public class DetalleOfertaCombinadaWrapper : BindableBase
    {
        public DetalleOfertaCombinadaWrapper() { }

        public DetalleOfertaCombinadaWrapper(OfertaCombinadaDetalleModel model)
        {
            Id = model.Id;
            Producto = model.Producto;
            ProductoNombre = model.ProductoNombre;
            Cantidad = model.Cantidad;
            Precio = model.Precio;
        }

        public int Id { get; set; }

        private string _producto;
        public string Producto
        {
            get => _producto;
            set => SetProperty(ref _producto, value);
        }

        private string _productoNombre;
        public string ProductoNombre
        {
            get => _productoNombre;
            set
            {
                if (SetProperty(ref _productoNombre, value))
                {
                    RaisePropertyChanged(nameof(texto));
                }
            }
        }

        public string texto
        {
            get => ProductoNombre;
            set => ProductoNombre = value;
        }

        private short _cantidad;
        public short Cantidad
        {
            get => _cantidad;
            set => SetProperty(ref _cantidad, value);
        }

        private decimal _precio;
        public decimal Precio
        {
            get => _precio;
            set => SetProperty(ref _precio, value);
        }
    }

    public class OfertaPermitidaFamiliaWrapper : BindableBase
    {
        private bool _rastreandoCambios = true;

        public OfertaPermitidaFamiliaWrapper() { }

        public OfertaPermitidaFamiliaWrapper(OfertaPermitidaFamiliaModel model)
        {
            _rastreandoCambios = false;
            NOrden = model.NOrden;
            Familia = model.Familia;
            FamiliaDescripcion = model.FamiliaDescripcion;
            CantidadConPrecio = model.CantidadConPrecio;
            CantidadRegalo = model.CantidadRegalo;
            FiltroProducto = model.FiltroProducto;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public void ActualizarDesdeServidor(OfertaPermitidaFamiliaModel model)
        {
            _rastreandoCambios = false;
            NOrden = model.NOrden;
            Familia = model.Familia;
            FamiliaDescripcion = model.FamiliaDescripcion;
            CantidadConPrecio = model.CantidadConPrecio;
            CantidadRegalo = model.CantidadRegalo;
            FiltroProducto = model.FiltroProducto;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public int NOrden { get; set; }

        private string _familia;
        public string Familia
        {
            get => _familia;
            set { if (SetProperty(ref _familia, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private string _familiaDescripcion;
        public string FamiliaDescripcion
        {
            get => _familiaDescripcion;
            set => SetProperty(ref _familiaDescripcion, value);
        }

        private short _cantidadConPrecio;
        public short CantidadConPrecio
        {
            get => _cantidadConPrecio;
            set { if (SetProperty(ref _cantidadConPrecio, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private short _cantidadRegalo;
        public short CantidadRegalo
        {
            get => _cantidadRegalo;
            set { if (SetProperty(ref _cantidadRegalo, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private string _filtroProducto;
        public string FiltroProducto
        {
            get => _filtroProducto;
            set { if (SetProperty(ref _filtroProducto, value) && _rastreandoCambios) HaCambiado = true; }
        }

        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }

        private bool _haCambiado;
        public bool HaCambiado
        {
            get => _haCambiado;
            set => SetProperty(ref _haCambiado, value);
        }
    }

    #endregion
}
