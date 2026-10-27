Imports Azure.Identity
Imports CommunityToolkit.Mvvm.Messaging
Imports ControlesUsuario
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Services
Imports ControlesUsuario.ViewModels
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modules.Producto
Imports Nesto.Modules.Producto.ViewModels
Imports Nesto.Modulos
Imports Nesto.Modulos.Cajas
Imports Nesto.Modulos.Cajas.Interfaces
Imports Nesto.Modulos.Cajas.Services
Imports Nesto.Modulos.Ganavisiones
Imports Nesto.Modulos.Ganavisiones.Interfaces
Imports Nesto.Modulos.Ganavisiones.Services
Imports Nesto.Modulos.OfertasCombinadas
Imports Nesto.Modulos.OfertasCombinadas.Interfaces
Imports Nesto.Modulos.OfertasCombinadas.Services
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

''' <summary>
''' Nesto#490 (4C.5): todo lo que registra la aplicación (lo que antes estaba en Application.RegisterTypes), sobre
''' <see cref="IRegistroNesto"/> y sin tipos de Prism. Desde el tramo 4 lo llama ArranqueNesto sobre MS.DI (hasta entonces, Application con un RegistroPrism); la prueba sombra
''' (Nesto.Tests) lo monta, con los módulos de <see cref="Modulos"/>, sobre Microsoft.Extensions.DependencyInjection.
''' Desde el tramo 4 de 4C.4 también registra las regiones y la navegación (la región propia, sin Prism.Regions).
''' </summary>
Public NotInheritable Class ComposicionNesto

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Los módulos de Nesto, en el orden en el que se cargan, por el tipo con el que se piden al contenedor (el
    ''' orden en que ArranqueNesto los inicializa; era el del catálogo de módulos de Prism, que no lo reordenaba). La
    ''' cinta depende de este orden: lo comprueba ArranqueSombraTests.
    ''' </summary>
    Public Shared ReadOnly Property Modulos As IReadOnlyList(Of Type) = New List(Of Type) From {
        GetType(IMenuBar),                                  ' Los botones de la cinta de todo lo viejo que no se hizo con módulos
        GetType(IPlantillaVenta),                           ' Plantilla de Ventas - 17/07/15
        GetType(IInventario),                               ' Inventarios - 09/11/15
        GetType(ICarteraPagos),                             ' Cartera de Pagos - 19/07/16
        GetType(IPedidoVenta),                              ' Pedido de Venta - 26/10/16
        GetType(IRapports),                                 ' Rapports - 14/03/17
        GetType(Nesto.Modulos.CanalesExternos.CanalesExternos), ' Canales Externos - 03/10/17
        GetType(IProducto),                                 ' Producto - 29/05/18
        GetType(ICliente),                                  ' Cliente - 29/05/19
        GetType(IPedidoCompra),                             ' Pedido de Compra - 17/11/21
        GetType(ICajas),                                    ' Cajas - 19/12/23
        GetType(IGanavisiones),                             ' Ganavisiones - 04/02/26 (Issue #281)
        GetType(IOfertasCombinadas)                         ' Ofertas Combinadas - 27/02/26 (Issue #299)
    }

    Public Shared Sub Registrar(registro As IRegistroNesto)
        If registro Is Nothing Then
            Throw New ArgumentNullException(NameOf(registro))
        End If

        registro.Singleton(GetType(IMainWindow), GetType(MainWindow))
        ' Nesto#490 (4C.4): cada módulo pone su menú en la cinta principal con IServicioCinta, sin el RibbonRegionAdapter
        ' de Prism (la misma fusión de pestañas, portada a Infrastructure). La cinta es la de MainWindow (el shell).
        registro.Singleton(Of IServicioCinta)(
            Function(r) New Nesto.Infrastructure.Cinta.ServicioCinta(r.Resolver(Of IMainWindow)().mainRibbon))
        registro.Singleton(GetType(IMenuBar), GetType(MenuBarView))
        registro.Singleton(GetType(IConfiguracion), GetType(Configuracion))
        ' Nesto#490 (4C.1): mensajería entre pantallas con CommunityToolkit en vez del IEventAggregator
        ' de Prism. Un único messenger para toda la aplicación (referencias débiles, como Prism).
        registro.Instancia(Of IMessenger)(WeakReferenceMessenger.Default)
        ' Nesto#490 (4C.2): servicio de diálogos propio, con la ventana propia de Nesto para todos (14/10/26, tras 14 días
        ' de piloto con el parámetro VentanaDialogosPropia sin ningún error). Los diálogos se registran con
        ' RegistrarDialogo (registro propio, ya no RegisterDialog de Prism) y la ventana saca de ahí la vista y su ViewModel.
        registro.Singleton(Of IServicioDialogos)(
            Function(r) New ServicioDialogosNesto(Function(nombre) RegistroDialogos.CrearDialogo(r, nombre)))

        Dim clientId = "d287e79a-5e01-4642-ac29-9b568dd39f67"
        ' Nesto#400: credencial con caché de tokens persistida y AuthenticationRecord rehidratado:
        ' el navegador de MS Graph solo se abre la primera vez, no en cada arranque de Nesto.
        Dim interactiveBrowserCredential As New CredencialGraphPersistente(clientId)
        registro.Instancia(GetType(InteractiveBrowserCredential), interactiveBrowserCredential)

        ' Registrar servicio de autenticación con la URL base de tu API
        registro.Singleton(Of IServicioAutenticacion)(
            Function(r)
                Dim cfg = r.Resolver(Of IConfiguracion)()
                Return New ServicioAutenticacion(cfg.servidorAPI)
            End Function)

        ' Registro centralizado de errores del cliente en ELMAH (vía NestoAPI)
        registro.Singleton(Of IServicioRegistroErrores)(
            Function(r)
                Dim cfg = r.Resolver(Of IConfiguracion)()
                Dim auth = r.Resolver(Of IServicioAutenticacion)()
                Return New ServicioRegistroErrores(cfg.servidorAPI, auth, cfg.usuario)
            End Function)

        ' Factory de HttpClient que adjunta el JWT automáticamente (para que el usuario
        ' salga en ELMAH). Usar IClienteApiFactory.Crear() en vez de 'New HttpClient'.
        registro.Singleton(Of IClienteApiFactory)(
            Function(r)
                Dim cfg = r.Resolver(Of IConfiguracion)()
                Dim auth = r.Resolver(Of IServicioAutenticacion)()
                Return New ClienteApiFactory(cfg.servidorAPI, auth)
            End Function)

        ' Nesto#372: changelog de novedades para el usuario al actualizar
        registro.Singleton(Of INovedadesService)(Function(r) New NovedadesService(r.Resolver(Of IClienteApiFactory)()))
        ' Nesto#519 (NestoAPI#616): adjuntos de las novedades (PDF e imágenes)
        registro.Singleton(Of IServicioAdjuntosNovedades)(Function(r) New ServicioAdjuntosNovedades(r.Resolver(Of IClienteApiFactory)()))
        ' NestoAPI#711: el vídeo de las novedades (como mucho uno por versión)
        registro.Singleton(Of IServicioVideosNovedades)(Function(r) New ServicioVideosNovedades(r.Resolver(Of IClienteApiFactory)()))
        ' Nesto#501: abre Novedades (menú de la cinta, botón junto a la campana y aviso de versión nueva)
        registro.Transitorio(Of IAbridorNovedades, AbridorNovedades)()
        ' NestoAPI#555: la campana abre el pedido de los avisos que hablan de uno.
        ' Nesto#490 (4C.5, tramo 3a): y todas las pantallas que abren un pedido, en lugar del IUnityContainer. No guarda
        ' estado: uno para toda la aplicación, con el resolutor del propio contenedor.
        registro.Singleton(Of IAbridorPedidos)(Function(r) New AbridorPedidos(r))
        ' Nesto#490 (4C.5, tramo 3b): el resolutor propio para lo que crea vistas por su tipo, en lugar del IUnityContainer o
        ' el IContainerProvider: PedidoVentaViewModel y PedidoCompraViewModel (la pestaña nueva) y sus vistas (la lista del
        ' maestro-detalle). Es el del propio contenedor (desde el tramo 4, el de MS.DI; antes, un ResolutorPrism sobre Unity).
        registro.Singleton(Of IResolutorNesto)(Function(r) r)
        ' Nesto#490 (4C.4, tramo 4): la región propia para todos, sin Prism.Regions ni el piloto RegionesPropias. Las
        ' regiones del shell (Application.CreateShell cuelga su gestor de MainWindow) y la navegación sobre ellas, que crea
        ' las vistas por el nombre con que se registran (Vista(Of XView)("XView")). Los registros de región y las
        ' navegaciones que fallan van a la traza y a ELMAH.
        registro.Singleton(Of Nesto.Infrastructure.Navegacion.RegionesDelShell)(
            Function(r) New Nesto.Infrastructure.Navegacion.RegionesDelShell(
                Sub(ex, contexto) r.Resolver(Of IServicioRegistroErrores)().RegistrarErrorAsync(ex, contexto)))
        registro.Singleton(Of IServicioNavegacion)(
            Function(r) r.Resolver(Of Nesto.Infrastructure.Navegacion.RegionesDelShell)().CrearNavegacion(AddressOf r.ResolverVista))

        ' Nesto#477: buzón de notificaciones (campana de la cinta)
        registro.Singleton(Of IBuzonNotificacionesService)(Function(r) New BuzonNotificacionesService(r.Resolver(Of IClienteApiFactory)()))
        ' NestoAPI#536: la campana (Nesto#477) recibe al momento, por SignalR, el aviso de notificaciones nuevas.
        ' La conexión se arranca en OnInitialized (tras tener token) salvo que el parámetro AvisosTiempoReal
        ' sea "NO": entonces no se conecta nunca y se comporta como el antiguo AvisosEnTiempoRealNulo.
        registro.Singleton(Of IAvisosEnTiempoReal)(
            Function(r)
                Dim cfg = r.Resolver(Of IConfiguracion)()
                Dim auth = r.Resolver(Of IServicioAutenticacion)()
                Dim errores = r.Resolver(Of IServicioRegistroErrores)()
                Return New AvisosEnTiempoRealSignalR(cfg.servidorAPI, auth,
                    Sub(ex) errores.RegistrarErrorAsync(ex, "AvisosEnTiempoReal"))
            End Function)

        ' Nesto#340: mantenimiento de agencias de transporte (alta/edición + fuel + cuarentena)
        registro.Singleton(Of IServicioAgenciasMantenimiento)(Function(r) New AgenciasMantenimientoService(r.Resolver(Of IClienteApiFactory)()))

        ' Etiquetas de hueco del almacén: las imprime la API por la ImpresoraCodBarras del usuario
        registro.Singleton(Of IServicioEtiquetasHueco)(Function(r) New ServicioEtiquetasHueco(r.Resolver(Of IClienteApiFactory)()))

        ' Nesto#534 (NestoAPI#654): la etiqueta de código de barras de producto, también por la ImpresoraCodBarras del usuario
        registro.Singleton(Of IServicioEtiquetasProducto)(Function(r) New ServicioEtiquetasProducto(r.Resolver(Of IClienteApiFactory)()))

        ' NestoAPI#553: la tienda recibe la reposición con la misma API que Ariadna (api/Almacen/Recepciones, REPO)
        registro.Singleton(Of IServicioRecepcionReposiciones)(Function(r) New ServicioRecepcionReposiciones(r.Resolver(Of IClienteApiFactory)()))

        ' NestoAPI#553: la tienda prepara y termina la reposición que manda a Algete (api/Reposiciones)
        registro.Singleton(Of IServicioEnvioReposiciones)(Function(r) New ServicioEnvioReposiciones(r.Resolver(Of IClienteApiFactory)()))

        ' NestoAPI#577: el calendario de reposiciones (api/Reposiciones/Calendario), que antes se cambiaba con scripts
        registro.Singleton(Of IServicioCalendarioReposiciones)(Function(r) New ServicioCalendarioReposiciones(r.Resolver(Of IClienteApiFactory)()))

        ' Nesto#526 (NestoAPI#634): las diferencias de reposición (api/Reposiciones/Diferencias): la ventana y el contador de la cinta
        registro.Singleton(Of IServicioDiferenciasReposicion)(Function(r) New ServicioDiferenciasReposicion(r.Resolver(Of IClienteApiFactory)()))

        ' NestoAPI#593 (c5): el cheque regalo del cliente (api/ChequesRegalo/Cliente) para ofrecerlo al meter el pedido
        registro.Singleton(Of IServicioChequesRegalo)(Function(r) New ServicioChequesRegalo(r.Resolver(Of IClienteApiFactory)()))

        ' Nesto#544: los productos que el vendedor puede ofrecer (api/SugerenciasProductos) para la cinta del rapport
        registro.Singleton(Of IServicioSugerenciasProductos)(Function(r) New ServicioSugerenciasProductos(r.Resolver(Of IClienteApiFactory)()))

        ' Nesto#518 (NestoAPI#613): los cupones de descuento de los eventos (plantilla, pedido, ficha del cliente y CanalesExternos)
        registro.Singleton(Of IServicioCupones)(Function(r) New ServicioCupones(r.Resolver(Of IClienteApiFactory)()))

        ' NestoAPI#406: mantenimiento de familias (marcar "público igual que profesional")
        registro.Singleton(Of IServicioFamiliasMantenimiento)(Function(r) New FamiliasMantenimientoService(r.Resolver(Of IClienteApiFactory)()))

        registro.Transitorio(GetType(IPlantillaVenta), GetType(PlantillaVenta))
        registro.Transitorio(GetType(IPlantillaVentaService), GetType(PlantillaVentaService))
        ' Issue #286: Borradores de PlantillaVenta
        registro.Transitorio(GetType(IBorradorPlantillaVentaService), GetType(BorradorPlantillaVentaService))
        registro.Transitorio(GetType(IInventario), GetType(Inventario))
        registro.Transitorio(GetType(ICarteraPagosService), GetType(CarteraPagosService))
        registro.Transitorio(GetType(ICarteraPagos), GetType(CarteraPagos))
        registro.Transitorio(GetType(IPedidoVentaService), GetType(PedidoVentaService))
        registro.Transitorio(GetType(IPedidoVenta), GetType(PedidoVenta))
        registro.Transitorio(GetType(IRapportService), GetType(RapportService))
        registro.Transitorio(GetType(IRapports), GetType(Rapports))
        registro.Transitorio(GetType(ICanalesExternos), GetType(Nesto.Modulos.CanalesExternos.CanalesExternos))
        registro.Transitorio(GetType(IAgenciaService), GetType(AgenciaService))
        registro.Transitorio(GetType(IProducto), GetType(Nesto.Modulos.Producto.Producto))
        registro.Transitorio(GetType(IProductoService), GetType(ProductoService))
        registro.Transitorio(GetType(ICliente), GetType(Nesto.Modulos.Cliente.Cliente))
        registro.Transitorio(GetType(IClienteService), GetType(ClienteService))
        registro.Transitorio(GetType(IPedidoCompra), GetType(Nesto.Modulos.PedidoCompra.PedidoCompra))
        registro.Transitorio(GetType(IClienteComercialService), GetType(ClienteComercialService))
        registro.Transitorio(GetType(ISelectorClienteService), GetType(SelectorClienteService))
        registro.Transitorio(GetType(ICajas), GetType(Cajas))
        registro.Transitorio(GetType(IContabilidadService), GetType(ContabilidadService))
        registro.Transitorio(GetType(IBancosService), GetType(BancosService))

        ' Carlos 04/02/26: Issue #281 - Ganavisiones
        registro.Transitorio(GetType(IGanavisiones), GetType(Ganavisiones))
        registro.Transitorio(GetType(IGanavisionesService), GetType(GanavisionesService))

        ' Carlos 27/02/26: Issue #299 - Ofertas Combinadas
        registro.Transitorio(GetType(IOfertasCombinadas), GetType(OfertasCombinadas))
        registro.Transitorio(GetType(IOfertasCombinadasService), GetType(OfertasCombinadasService))
        registro.Transitorio(GetType(IClientesService), GetType(ClientesService))
        registro.Transitorio(GetType(ISelectorProveedorService), GetType(SelectorProveedorService))
        registro.Transitorio(GetType(IRecursosHumanosService), GetType(RecursosHumanosService))
        registro.Transitorio(GetType(ICanalesExternosProductosService), GetType(CanalesExternosProductosService))
        registro.Transitorio(GetType(ICanalesExternosPagosService), GetType(CanalesExternosPagosService))

        ' Carlos 20/11/24: FASE 3 - Registrar servicio de direcciones de entrega para SelectorDireccionEntrega
        registro.Singleton(GetType(IServicioDireccionesEntrega), GetType(ServicioDireccionesEntrega))

        ' Carlos 20/11/24: Registrar servicio de CCCs para SelectorCCC
        registro.Singleton(GetType(IServicioCCC), GetType(ServicioCCC))

        ' Carlos 04/12/24: Issue #252 - Registrar servicio de formas de venta para SelectorFormaVenta
        registro.Singleton(GetType(IServicioFormaVenta), GetType(ServicioFormaVenta))

        ' Carlos 09/12/25: Issue #253/#52 - Registrar servicio de almacenes para SelectorAlmacen
        registro.Singleton(GetType(IServicioAlmacenes), GetType(ServicioAlmacenes))

        ' Carlos 09/12/25: Issue #245 - Registrar servicio de series para SelectorSerie
        registro.Singleton(GetType(IServicioSeries), GetType(ServicioSeries))

        ' Caso real 20/08/26: parámetros que el propio usuario puede cambiarse (Tienda Online
        ' alterna AMZ/ALG según facture FBA o rutas). El catálogo vive en NestoAPI.
        registro.Singleton(GetType(IServicioParametrosEditables), GetType(ServicioParametrosEditables))

        ' Carlos 11/12/25: Issue #258 - Registrar servicio de cuentas contables para CuentaContableBehavior
        registro.Transitorio(GetType(IServicioCuentaContable), GetType(ServicioCuentaContable))

        ' Carlos 11/12/25: Issue #258 - Registrar servicio de productos para ProductoBehavior
        registro.Transitorio(GetType(IServicioProducto), GetType(ServicioProducto))

        ' Carlos 04/02/26: Issue #281 - Registrar servicio de búsqueda para AutocompleteBehavior
        registro.Transitorio(Of ServicioBusquedaProductos)()
        registro.Transitorio(Of ServicioBusquedaCuentas)()

        registro.RegistrarDialogo(Of ConfirmationDialog, ConfirmationDialogViewModel)
        registro.RegistrarDialogo(Of NotificationDialog, NotificationDialogViewModel)
        registro.RegistrarDialogo(Of InputAmountDialog, InputAmountDialogViewModel)
        registro.RegistrarDialogo(Of InputTextDialog, InputTextDialogViewModel)
        registro.RegistrarDialogo(Of InputDateDialog, InputDateDialogViewModel) ' NestoAPI#582
        registro.RegistrarDialogo(Of CorreccionVideoProductoView, CorreccionVideoProductoViewModel)
        registro.RegistrarDialogo(Of ActualizarControlesStockPopupView, ActualizarControlesStockPopupViewModel)
        ' Carlos 08/06/26: Nesto#368 - Selector cuando un código de barras corresponde a varios productos (API devuelve 409)
        registro.RegistrarDialogo(Of SelectorProductoDuplicadoDialog, SelectorProductoDuplicadoDialogViewModel)
        ' Nesto#372: diálogo "Qué hay de nuevo" (popup tras actualizar + Herramientas → Ayuda → Novedades)
        registro.RegistrarDialogo(Of NovedadesDialog, NovedadesDialogViewModel)
        ' Caso real 20/08/26: ventana de parámetros de usuario (sustituye al MessageBox del menú)
        registro.RegistrarDialogo(Of ParametrosUsuarioDialog, ParametrosUsuarioDialogViewModel)
        ' NestoAPI#609: «¿Quisiste decir…?» con la corrección del concepto antes de crear un enlace de pago
        registro.RegistrarDialogo(Of RevisionConceptoDialog, RevisionConceptoDialogViewModel)

        ' Nesto#490 (4C.5): lo que Unity construía sin registrar y se pide por su tipo concreto
        ' La campana de la ventana principal (la recibe MainWindow por constructor)
        registro.Transitorio(Of ControlesUsuario.Notificaciones.CampanaNotificacionesViewModel)()
        ' Los selectores de cliente y de proveedor sacan su ViewModel del contenedor (Contenedor.Actual)
        registro.Transitorio(Of SelectorClienteViewModel)()
        registro.Transitorio(Of SelectorProveedorViewModel)()
    End Sub
End Class
