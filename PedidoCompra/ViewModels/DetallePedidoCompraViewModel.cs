using Azure.Identity;
using ControlesUsuario.Dialogs;
using Microsoft.Graph;
using Microsoft.Reporting.NETCore;
using Nesto.Informes;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoCompra.Events;
using Nesto.Modulos.PedidoCompra.Models;
using Prism.Commands;
using Prism.Events;
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
        public IConfiguracion Configuracion { get; }
        private IEventAggregator EventAggregator { get; }

        public DetallePedidoCompraViewModel(IPedidoCompraService servicio, IDialogService dialogService, IRegionManager regionManager, InteractiveBrowserCredential interactiveBrowserCredential, IConfiguracion configuracion, IEventAggregator eventAggregator)
        {
            Servicio = servicio;
            DialogService = dialogService;
            RegionManager = regionManager;
            InteractiveBrowserCredential = interactiveBrowserCredential;
            Configuracion = configuracion;
            EventAggregator = eventAggregator;

            AmpliarHastaStockMaximoCommand = new DelegateCommand(OnAmpliarHastaStockMaximo);
            CargarPedidoCommand = new DelegateCommand<PedidoCompraLookup>(OnCargarPedido);
            CargarProductoCommand = new DelegateCommand<LineaPedidoCompraWrapper>(OnCargarProducto);
            EnviarPedidoCommand = new DelegateCommand<PedidoCompraWrapper>(OnEnviarPedido, CanEnviarPedido);
            GuardarPedidoCommand = new DelegateCommand(OnGuardarPedido, CanGuardarPedido);
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

        public InteractiveBrowserCredential InteractiveBrowserCredential { get; }

        private LineaPedidoCompraWrapper _lineaMaximaCantidad;
        public LineaPedidoCompraWrapper LineaMaximaCantidad
        {
            get => _lineaMaximaCantidad;
            set => SetProperty(ref _lineaMaximaCantidad, value);
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
            set { 
                SetProperty(ref _pedido, value);
                ((DelegateCommand<PedidoCompraWrapper>)EnviarPedidoCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)GuardarPedidoCommand).RaiseCanExecuteChanged();
            }
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

        public ICommand EnviarPedidoCommand { get; private set; }
        private bool CanEnviarPedido(PedidoCompraWrapper pedido)
        {
            return pedido != null && pedido.Id != 0;
        }

        private async void OnEnviarPedido(PedidoCompraWrapper pedido)
        {
            if (pedido == null || !DialogService.ShowConfirmationAnswer("Enviar pedido", $"Se va a enviar el pedido por correo electrónico a {pedido.Model.CorreoRecepcionPedidos}. ¿Desea continuar?"))
            {
                return;
            }
            EstaOcupado = true;
            LocalReport report = await CrearInforme(pedido);
            var pdf = report.Render("PDF");
            var xlsx = report.Render("EXCELOPENXML");

            string[] scopes = new string[] { "Mail.Send", "Mail.ReadWrite" };
            GraphServiceClient graphClient = new GraphServiceClient(InteractiveBrowserCredential, scopes);
            string cuerpoCorreo;
            if (!string.IsNullOrEmpty(pedido.Model.Comentarios))
            {
                cuerpoCorreo = pedido.Model.Comentarios + "\n\n";
            }
            else
            {
                cuerpoCorreo = string.Empty;
            }
            cuerpoCorreo += "Adjuntamos nueva orden de compra en formato PDF y formato Excel (son el mismo pedido, para que ustedes puedan elegir entre ambas opciones a la hora de gestionar el pedido).";
            cuerpoCorreo += "\n\nRogamos nos informen a la mayor brevedad de la fecha estimada de recepción en nuestros almacenes y nos envíen el enlace de seguimiento de la agencia de transportes tan pronto como obre en su poder.";            
            cuerpoCorreo += "\n\nMuchas gracias.";
            var message = new Message
            {
                Subject = $"[Nueva Visión] Pedido {pedido.Id}",
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = cuerpoCorreo
                },
                ToRecipients = new List<Recipient>()
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = !string.IsNullOrEmpty(pedido.Model.CorreoRecepcionPedidos) ? pedido.Model.CorreoRecepcionPedidos : Constantes.CorreosEmpresa.COMPRAS
                        }
                    }
                },
                Attachments = new MessageAttachmentsCollectionPage()
                {
                    new FileAttachment
                    {
                        Name = $"Pedido_{pedido.Id}.pdf",
                        ContentType = "application/pdf",
                        ContentBytes = pdf
                    },
                    new FileAttachment
                    {
                        Name = $"Pedido_{pedido.Id}.xlsx",
                        ContentType = "application/vnd.ms-excel",
                        ContentBytes = xlsx
                    }
                }
            };

            var saveToSentItems = true;

            try
            {
                await graphClient.Me
                .SendMail(message, saveToSentItems)
                .Request()
                .PostAsync();
                DialogService.ShowNotification("Pedido enviado con éxito");
            } catch
            {
                DialogService.ShowError("No se ha podido enviar el correo");
                EstaOcupado = false;
                return;
            }

            try
            {
                // Copiar el PDF en F:
                string rutaPDF = await Configuracion.leerParametro(pedido.Model.Empresa, Parametros.Claves.RutaPedidosCmp);
                string rutaCompleta = $"{rutaPDF}\\{pedido.Id}.pdf";
                System.IO.File.WriteAllBytes(rutaCompleta, pdf);

                // Actualizar cabecera PathPedido = {pedido.Id}.pdf y las líneas con enviado = 1 && FechaRecepción
                pedido.Model.PathPedido = $"{pedido.Id}.pdf";
                await Servicio.ModificarPedido(pedido.Model);
            }
            catch
            {
                DialogService.ShowError("No se han podido actualizar los datos del pedido");
                EstaOcupado = false;
                return;
            }

            EstaOcupado = false;
            ((DelegateCommand<PedidoCompraWrapper>)EnviarPedidoCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)GuardarPedidoCommand).RaiseCanExecuteChanged();
        }


        //private DelegateCommand guardarPedidoCommand;
        //public ICommand GuardarPedidoCommand => guardarPedidoCommand ??= new DelegateCommand(GuardarPedido, CanGuardarPedido);
        public ICommand GuardarPedidoCommand { get; private set; }
        private bool CanGuardarPedido()
        {
            return Pedido != null && Pedido.Id == 0;
        }
        private async void OnGuardarPedido()
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
                EventAggregator.GetEvent<PedidoCompraModificadoEvent>().Publish(Pedido.Model);
                ((DelegateCommand<PedidoCompraWrapper>)EnviarPedidoCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)GuardarPedidoCommand).RaiseCanExecuteChanged();
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

        public ICommand ImprimirPedidoCommand { get; private set; }
        private async void OnImprimirPedido(PedidoCompraWrapper pedido)
        {
            if (pedido == null)
            {
                return;
            }
            LocalReport report = await CrearInforme(pedido);
            var pdf = report.Render("PDF");
            string fileName = Path.GetTempPath() + $"PedidoCompra{pedido.Id}.pdf";
            System.IO.File.WriteAllBytes(fileName, pdf);
            System.Diagnostics.Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
        }

        private static async Task<LocalReport> CrearInforme(PedidoCompraWrapper pedido)
        {
            Stream reportDefinition = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.PedidoCompra.rdlc");
            PedidoCompraModel dataSource = await PedidoCompraModel.CargarDatos(pedido.Model.Empresa, pedido.Id);
            List<PedidoCompraModel> listaDataSource = new();
            listaDataSource.Add(dataSource);
            LocalReport report = new();
            report.LoadReportDefinition(reportDefinition);
            report.DataSources.Add(new ReportDataSource("PedidoCompraDataSet", listaDataSource));
            report.DataSources.Add(new ReportDataSource("PedidoCompraLineasDataSet", dataSource.Lineas));
            return report;
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
                EstaOcupado = true;
                await Task.Run(() =>
                {
                    LineaMaximaCantidad = Pedido.Lineas
                        .Where(l => l.Model.Subgrupo != Constantes.Productos.Grupos.MUESTRAS)
                        .OrderByDescending(l => l.Model.StockMaximo).FirstOrDefault();
                    if (LineaMaximaCantidad.Model.StockMaximo <= 0)
                    {
                        return;
                    }
                    decimal ratioIncremento = (decimal)(LineaMaximaCantidad.Model.CantidadBruta - LineaMaximaCantidad.CantidadOriginal + LineaMaximaCantidad.Model.Multiplos) / LineaMaximaCantidad.Model.StockMaximo;

                    if (ampliarReducir == "reducir")
                    {
                        ratioIncremento = (decimal)(LineaMaximaCantidad.Model.CantidadBruta - LineaMaximaCantidad.CantidadOriginal - LineaMaximaCantidad.Model.Multiplos) / LineaMaximaCantidad.Model.StockMaximo;
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
                });
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

    }
}
