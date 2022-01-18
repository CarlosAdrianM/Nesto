using ControlesUsuario.Dialogs;
using Microsoft.Reporting.NETCore;
using Nesto.Informes;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoCompra.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.PedidoCompra.ViewModels
{
    public class DetallePedidoCompraViewModel : BindableBase, INavigationAware
    {
        public IPedidoCompraService Servicio { get; }
        public IDialogService DialogService { get; }
        public IRegionManager RegionManager { get; }

        public DetallePedidoCompraViewModel(IPedidoCompraService servicio, IDialogService dialogService, IRegionManager regionManager)
        {
            Servicio = servicio;
            DialogService = dialogService;
            RegionManager = regionManager;

            AmpliarHastaStockMaximoCommand = new DelegateCommand(OnAmpliarHastaStockMaximo);
            CargarPedidoCommand = new DelegateCommand<PedidoCompraLookup>(OnCargarPedido);
            CargarProductoCommand = new DelegateCommand<LineaPedidoCompraWrapper>(OnCargarProducto);
            ImprimirPedidoCommand = new DelegateCommand<PedidoCompraWrapper>(OnImprimirPedido); 
            InsertarLineaCommand = new DelegateCommand(OnInsertarLinea);
            PedidoAmpliarCommand = new DelegateCommand<string>(OnPedidoAmpliar, CanPedidoAmpliar);
        }

        bool ampliadoHastaStockMaximo;

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set => SetProperty(ref _estaOcupado, value);
        }

        private LineaPedidoCompraWrapper _lineaSeleccionada;
        public LineaPedidoCompraWrapper LineaSeleccionada
        {
            get => _lineaSeleccionada;
            set => SetProperty(ref _lineaSeleccionada, value);
        }

        private bool _mostrarLineasCantidadCero;
        public bool MostrarLineasCantidadCero
        {
            get => _mostrarLineasCantidadCero;
            set => SetProperty(ref _mostrarLineasCantidadCero, value);
        }
        
        private PedidoCompraWrapper _pedido;
        public PedidoCompraWrapper Pedido {
            get => _pedido;
            set => SetProperty(ref _pedido, value);
        }

        public ICommand AmpliarHastaStockMaximoCommand { get; private set; }
        private async void OnAmpliarHastaStockMaximo()
        {
            try
            {
                EstaOcupado = true;
                var pedidoAmpliado = await Servicio.AmpliarHastaStockMaximo(Pedido.Model);
                Pedido = new PedidoCompraWrapper(pedidoAmpliado, Servicio);
                ampliadoHastaStockMaximo = true;
                PedidoAmpliarCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public ICommand CargarPedidoCommand { get; private set; }
        private async void OnCargarPedido(PedidoCompraLookup pedidoCompraLookup)
        {
            if (pedidoCompraLookup == null)
            {
                return;
            }
            try
            {
                PedidoCompraDTO pedidoDTO = await Servicio.CargarPedido(pedidoCompraLookup.Empresa, pedidoCompraLookup.Pedido);
                Pedido = new PedidoCompraWrapper(pedidoDTO, Servicio);
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
        }

        public ICommand CargarProductoCommand { get; private set; }
        private void OnCargarProducto(LineaPedidoCompraWrapper linea)
        {
            NavigationParameters parameters = new()
            {
                { "numeroProductoParameter", linea.Producto }
            };
            RegionManager.RequestNavigate("MainRegion", "ProductoView", parameters);
        }


        public ICommand ImprimirPedidoCommand { get; private set; }
        private async void OnImprimirPedido(PedidoCompraWrapper pedido)
        {
            if (pedido == null)
            {
                return;
            }
            Stream reportDefinition = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.PedidoCompra.rdlc");
            PedidoCompraModel dataSource = await PedidoCompraModel.CargarDatos(pedido.Model.Empresa, pedido.Id);
            List<PedidoCompraModel> listaDataSource = new();
            listaDataSource.Add(dataSource);
            LocalReport report = new();
            report.LoadReportDefinition(reportDefinition);
            report.DataSources.Add(new ReportDataSource("PedidoCompraDataSet", listaDataSource));
            report.DataSources.Add(new ReportDataSource("PedidoCompraLineasDataSet", dataSource.Lineas));
            var pdf = report.Render("PDF");
            string fileName = Path.GetTempPath() + $"PedidoCompra{pedido.Id}.pdf";
            File.WriteAllBytes(fileName, pdf);
            Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
        }

        public ICommand InsertarLineaCommand { get; private set; }
        private void OnInsertarLinea()
        {
            int posicion = Pedido.Lineas.IndexOf(LineaSeleccionada);
            LineaPedidoCompraDTO nuevaLineaDTO = new()
            {
                Id = -1,
                Estado = Constantes.LineasPedido.ESTADO_SIN_FACTURAR,
                TipoLinea = Pedido.UltimoTipoLinea,
                FechaRecepcion = Pedido.UltimaFechaRecepcion,
                Cantidad = 1
            };
            LineaPedidoCompraWrapper nuevaLinea = new(nuevaLineaDTO, Servicio)
            {
                Pedido = Pedido
            };
            ((List<LineaPedidoCompraDTO>)Pedido.Model.Lineas).Insert(posicion, nuevaLineaDTO);
            Pedido.Lineas.Insert(posicion, nuevaLinea);
        }

        public DelegateCommand<string> PedidoAmpliarCommand { get; private set; }
        private bool CanPedidoAmpliar(string arg)
        {
            return ampliadoHastaStockMaximo;
        }

        private async void OnPedidoAmpliar(string ampliarReducir)
        {
            try
            {
                var lineaMaximaCantidad = Pedido.Lineas
                    .Where(l => l.Model.Subgrupo != Constantes.Productos.Grupos.MUESTRAS)
                    .OrderByDescending(l => l.Model.StockMaximo).FirstOrDefault();
                if (lineaMaximaCantidad.Model.StockMaximo <= 0)
                {
                    return;
                }
                decimal ratioIncremento = (decimal)(lineaMaximaCantidad.Model.CantidadBruta - lineaMaximaCantidad.CantidadOriginal + lineaMaximaCantidad.Model.Multiplos) / lineaMaximaCantidad.Model.StockMaximo;

                if (ampliarReducir == "reducir")
                {
                    ratioIncremento = (decimal)(lineaMaximaCantidad.Model.CantidadBruta - lineaMaximaCantidad.CantidadOriginal - lineaMaximaCantidad.Model.Multiplos) / lineaMaximaCantidad.Model.StockMaximo;
                }

                foreach (var linea in Pedido.Lineas)
                {
                    linea.Model.CantidadBruta = (int)Math.Round((decimal)linea.Model.StockMaximo * ratioIncremento) + linea.CantidadOriginal;
                    linea.Cantidad = linea.Model.CantidadBruta > 0 ? linea.Model.CantidadBruta : 0;
                    if (linea.Model.Multiplos != 0)
                    {
                        linea.Cantidad = (int)(linea.Cantidad % linea.Model.Multiplos == 0 ? linea.Cantidad : Math.Ceiling((double)linea.Cantidad / linea.Model.Multiplos) * linea.Model.Multiplos);
                    }
                }
                RaisePropertyChanged(string.Empty);
                RaisePropertyChanged(nameof(Pedido));
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
        }
        /*
        public ICommand PedidoReducirCommand { get; private set; }
        private async void OnPedidoReducir()
        {
            try
            {
                var lineaMaximaCantidad = Pedido.Model.Lineas.OrderByDescending(l => l.Cantidad).FirstOrDefault();
                var ratioIncremento = lineaMaximaCantidad.Multiplos / lineaMaximaCantidad.Cantidad;
                
                foreach (var linea in Pedido.Lineas)
                {
                    linea.Cantidad = (int)Math.Round((decimal)linea.Cantidad / (1 + ratioIncremento));
                    linea.Cantidad = (int)(linea.Cantidad % linea.Model.Multiplos == 0 ? linea.Cantidad : Math.Ceiling((double)(linea.Cantidad / linea.Model.Multiplos)) * linea.Model.Multiplos);
                }
                RaisePropertyChanged(string.Empty);
                RaisePropertyChanged(nameof(Pedido));
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
        }
        */

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            var pedidoLookup = navigationContext.Parameters["PedidoLookupParameter"] as PedidoCompraLookup;
            if (pedidoLookup != null)
            {
                CargarPedidoCommand.Execute(pedidoLookup);
            }
            else
            {
                var pedido = navigationContext.Parameters["PedidoParameter"] as PedidoCompraDTO;
                Pedido = new PedidoCompraWrapper(pedido, Servicio);
                if (Pedido.Model != null)
                {
                    CargarOfertasYDescuentos(Pedido);
                }
            }
        }

        private async Task CargarOfertasYDescuentos(PedidoCompraWrapper pedido)
        {
            for (var i = 0; i < pedido.Lineas.Count; i++)
            {
                var linea = pedido.Lineas[i];
                var producto = await Servicio.LeerProducto(pedido.Model.Empresa, linea.Producto, pedido.Model.Proveedor, pedido.Model.CodigoIvaProveedor);
                linea.Model.Ofertas = producto.Ofertas;
                linea.Model.Descuentos = producto.Descuentos;
                linea.Cantidad = linea.Cantidad; // para que actualice descuentos y ofertas
                if (linea.TipoLinea == Constantes.LineasPedido.TiposLinea.PRODUCTO && linea.Cantidad != 0 && linea.BaseImponible == 0)
                {
                    var lineaMismoProducto = pedido.Lineas.FirstOrDefault(l => l.Producto == linea.Producto);
                    if (lineaMismoProducto != null && lineaMismoProducto != linea && lineaMismoProducto.Model.Ofertas.Any())
                    {
                        //lineaMismoProducto.Model.AplicarDescuentos = false;
                        lineaMismoProducto.Cantidad += linea.Cantidad;
                        pedido.Lineas.Remove(linea);
                        i--;
                    }
                }

            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return false;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            
        }

        private DelegateCommand guardarPedidoCommand;
        public ICommand GuardarPedidoCommand => guardarPedidoCommand ??= new DelegateCommand(GuardarPedido);

        private async void GuardarPedido()
        {
            bool continuar = DialogService.ShowConfirmationAnswer("Guardar pedido", "Se va a guardar el pedido. ¿Desea continuar?");
            if (!continuar)
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                Pedido.Id = await Servicio.CrearPedido(Pedido.Model);
                DialogService.ShowNotification($"Pedido {Pedido.Id} guardado correctamente");
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
