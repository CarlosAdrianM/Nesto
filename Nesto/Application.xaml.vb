Imports System.Globalization
Imports System.Windows.Markup
Imports Azure.Identity
Imports ControlesUsuario
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Services
Imports ControlesUsuario.ViewModels
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modules.Producto
Imports Nesto.Modules.Producto.ViewModels
Imports Nesto.Modulos
Imports Nesto.Modulos.Cajas
Imports Nesto.Modulos.Cajas.Interfaces
Imports Nesto.Modulos.Cajas.Services
Imports Nesto.Modulos.CanalesExternos.Interfaces
Imports Nesto.Modulos.CanalesExternos.Services
Imports Nesto.Modulos.CarteraPagos
Imports Nesto.Modulos.Cliente
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PlantillaVenta
Imports Nesto.Modulos.Producto.Views
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels
Imports Prism
Imports Prism.Ioc
Imports Prism.Modularity
Imports Prism.Mvvm
Imports Prism.Regions
Imports Prism.RibbonRegionAdapter
Imports Unity

Partial Public Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        FrameworkElement.LanguageProperty.OverrideMetadata(GetType(FrameworkElement), New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))
        MyBase.OnStartup(e)
    End Sub

    Protected Overrides Sub RegisterTypes(containerRegistry As IContainerRegistry)
        Dim unused30 = containerRegistry.RegisterSingleton(GetType(RibbonRegionAdapter), GetType(RibbonRegionAdapter))
        Dim unused29 = containerRegistry.RegisterSingleton(GetType(IMainWindow), GetType(MainWindow))
        Dim unused28 = containerRegistry.RegisterSingleton(GetType(IMenuBar), GetType(MenuBarView))
        Dim unused27 = containerRegistry.RegisterSingleton(GetType(IConfiguracion), GetType(Configuracion))

        Dim clientId = "d287e79a-5e01-4642-ac29-9b568dd39f67"
        Dim interactiveBrowserCredentialOptions As New InteractiveBrowserCredentialOptions() With {
            .ClientId = clientId
        }
        Dim interactiveBrowserCredential As New InteractiveBrowserCredential(interactiveBrowserCredentialOptions)
        Dim unused26 = containerRegistry.RegisterInstance(GetType(InteractiveBrowserCredential), interactiveBrowserCredential)

        ' Registrar servicio de autenticación con la URL base de tu API
        Dim unused31 = containerRegistry.RegisterSingleton(Of IServicioAutenticacion)(
            Function(provider)
                Dim cfg = provider.Resolve(Of IConfiguracion)()
                Return New ServicioAutenticacion(cfg.servidorAPI)
            End Function)

        Dim unused25 = containerRegistry.Register(GetType(IPlantillaVenta), GetType(PlantillaVenta))
        Dim unused24 = containerRegistry.Register(GetType(IPlantillaVentaService), GetType(PlantillaVentaService))
        Dim unused23 = containerRegistry.Register(GetType(IInventario), GetType(Inventario))
        Dim unused22 = containerRegistry.Register(GetType(ICarteraPagosService), GetType(CarteraPagosService))
        Dim unused21 = containerRegistry.Register(GetType(ICarteraPagos), GetType(CarteraPagos))
        Dim unused20 = containerRegistry.Register(GetType(IPedidoVentaService), GetType(PedidoVentaService))
        Dim unused19 = containerRegistry.Register(GetType(IPedidoVenta), GetType(PedidoVenta))
        Dim unused18 = containerRegistry.Register(GetType(IRapportService), GetType(RapportService))
        Dim unused17 = containerRegistry.Register(GetType(IRapports), GetType(Rapports))
        Dim unused16 = containerRegistry.Register(GetType(ICanalesExternos), GetType(Nesto.Modulos.CanalesExternos.CanalesExternos))
        Dim unused15 = containerRegistry.Register(GetType(IAgenciaService), GetType(AgenciaService))
        Dim unused14 = containerRegistry.Register(GetType(IProducto), GetType(Nesto.Modulos.Producto.Producto))
        Dim unused13 = containerRegistry.Register(GetType(IProductoService), GetType(ProductoService))
        Dim unused12 = containerRegistry.Register(GetType(ICliente), GetType(Nesto.Modulos.Cliente.Cliente))
        Dim unused11 = containerRegistry.Register(GetType(IClienteService), GetType(ClienteService))
        Dim unused10 = containerRegistry.Register(GetType(IPedidoCompra), GetType(Nesto.Modulos.PedidoCompra.PedidoCompra))
        Dim unused9 = containerRegistry.Register(GetType(IClienteComercialService), GetType(ClienteComercialService))
        Dim unused8 = containerRegistry.Register(GetType(ISelectorClienteService), GetType(SelectorClienteService))
        Dim unused7 = containerRegistry.Register(GetType(ICajas), GetType(Cajas))
        Dim unused6 = containerRegistry.Register(GetType(IContabilidadService), GetType(ContabilidadService))
        Dim unused5 = containerRegistry.Register(GetType(IBancosService), GetType(BancosService))
        Dim unused4 = containerRegistry.Register(GetType(IClientesService), GetType(ClientesService))
        Dim unused3 = containerRegistry.Register(GetType(ISelectorProveedorService), GetType(SelectorProveedorService))
        Dim unused2 = containerRegistry.Register(GetType(IRecursosHumanosService), GetType(RecursosHumanosService))
        Dim unused1 = containerRegistry.Register(GetType(ICanalesExternosProductosService), GetType(CanalesExternosProductosService))
        Dim unused = containerRegistry.Register(GetType(ICanalesExternosPagosService), GetType(CanalesExternosPagosService))

        ' Carlos 20/11/24: FASE 3 - Registrar servicio de direcciones de entrega para SelectorDireccionEntrega
        Dim unused32 = containerRegistry.RegisterSingleton(GetType(IServicioDireccionesEntrega), GetType(ServicioDireccionesEntrega))

        ' Carlos 20/11/24: Registrar servicio de CCCs para SelectorCCC
        Dim unused33 = containerRegistry.RegisterSingleton(GetType(IServicioCCC), GetType(ServicioCCC))

        containerRegistry.RegisterDialog(Of ConfirmationDialog, ConfirmationDialogViewModel)
        containerRegistry.RegisterDialog(Of NotificationDialog, NotificationDialogViewModel)
        containerRegistry.RegisterDialog(Of InputAmountDialog, InputAmountDialogViewModel)
        containerRegistry.RegisterDialog(Of CorreccionVideoProductoView, CorreccionVideoProductoViewModel)
    End Sub

    Protected Overrides Function CreateShell() As Window
        Return Container.Resolve(Of IMainWindow)
    End Function

    Protected Overrides Sub ConfigureModuleCatalog(moduleCatalog As IModuleCatalog)
        MyBase.ConfigureModuleCatalog(moduleCatalog)
        ' Módulo que tiene los botones del Ribbon de todo lo viejo que no se hizo con módulos
        Dim unused10 = moduleCatalog.AddModule(GetType(IMenuBar)) ', InitializationMode.WhenAvailable

        ' Plantilla de Ventas - 17/07/15
        Dim unused9 = moduleCatalog.AddModule(GetType(IPlantillaVenta)) ', InitializationMode.WhenAvailable

        ' Inventarios - 09/11/15
        Dim unused8 = moduleCatalog.AddModule(GetType(IInventario)) ', InitializationMode.WhenAvailable

        ' Cartera de Pagos - 19/07/16
        Dim unused7 = moduleCatalog.AddModule(GetType(ICarteraPagos)) ', InitializationMode.WhenAvailable

        ' Pedido de Venta - 26/10/16
        Dim unused6 = moduleCatalog.AddModule(GetType(IPedidoVenta))

        ' Rapports - 14/03/17
        Dim unused5 = moduleCatalog.AddModule(GetType(IRapports))

        ' Canales Externos - 03/10/17
        Dim unused4 = moduleCatalog.AddModule(Of Nesto.Modulos.CanalesExternos.CanalesExternos)

        ' Producto - 29/05/18
        Dim unused3 = moduleCatalog.AddModule(GetType(IProducto))

        ' Cliente - 29/05/19
        Dim unused2 = moduleCatalog.AddModule(GetType(ICliente))

        ' Pedido de Compra - 17/11/21
        Dim unused1 = moduleCatalog.AddModule(GetType(IPedidoCompra))

        ' Cajas - 19/12/23
        Dim unused = moduleCatalog.AddModule(GetType(ICajas))
    End Sub

    Protected Overrides Sub ConfigureRegionAdapterMappings(regionAdapterMappings As RegionAdapterMappings)
        MyBase.ConfigureRegionAdapterMappings(regionAdapterMappings)
        regionAdapterMappings.RegisterMapping(GetType(RibbonRegionAdapter), Container.Resolve(Of RibbonRegionAdapter)())
    End Sub

    Protected Overrides Sub ConfigureViewModelLocator()
        MyBase.ConfigureViewModelLocator()
        ViewModelLocationProvider.Register(GetType(Remesas).ToString, GetType(RemesasViewModel))
        ViewModelLocationProvider.Register(GetType(Alquileres).ToString, GetType(AlquileresViewModel))
        ViewModelLocationProvider.Register(GetType(SelectorCliente).ToString, GetType(SelectorClienteViewModel))
        ViewModelLocationProvider.Register(GetType(SelectorProveedor).ToString, GetType(SelectorProveedorViewModel))
    End Sub

    Protected Overrides Sub OnInitialized()
        MyBase.OnInitialized()

        Dim authService = Container.Resolve(Of IServicioAutenticacion)()
        Dim unused = Task.Run(
            Async Function()
                Dim token = Await authService.ObtenerTokenWindowsAsync()
                If token IsNot Nothing Then
                    System.Diagnostics.Debug.WriteLine("Token obtenido exitosamente al iniciar la aplicación")
                End If
            End Function)
    End Sub
End Class
