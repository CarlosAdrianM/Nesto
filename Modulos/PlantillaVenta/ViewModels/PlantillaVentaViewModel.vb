Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.ComponentModel.DataAnnotations
Imports System.Net.Http
Imports System.Text
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Models
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Mvvm
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports Unity
Imports Xceed.Wpf.Toolkit

Public Class PlantillaVentaViewModel
    Inherits BindableBase
    Implements INavigationAware, ITabCloseConfirmation

    Public Property configuracion As IConfiguracion
    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly servicio As IPlantillaVentaService
    Private ReadOnly servicioPedidosVenta As IPedidoVentaService
    Private ReadOnly servicioBorradores As IBorradorPlantillaVentaService
    Private ReadOnly eventAggregator As IEventAggregator
    Private ReadOnly dialogService As IDialogService
    Private Const ESTADO_LINEA_CURSO As Integer = 1
    Private Const ESTADO_LINEA_PRESUPUESTO As Integer = -3

    ''' <summary>
    ''' Cache de IDs de productos bonificables (tabla Ganavisiones).
    ''' Issue #94: Sistema Ganavisiones - FASE 6
    ''' </summary>
    Private _productosBonificablesIds As HashSet(Of String)

    Private Const PAGINA_SELECCION_CLIENTE As String = "SeleccionCliente"
    Private Const PAGINA_SELECCION_PRODUCTOS As String = "SeleccionProductos"
    Private Const PAGINA_SELECCION_ENTREGA As String = "SeleccionEntrega"
    Private Const PAGINA_FINALIZAR As String = "Finalizar"


    Private formaVentaPedido, delegacionUsuario, almacenRutaUsuario, iva, vendedorUsuario As String
    Private ultimoClienteAbierto As String = ""

    ''' <summary>
    ''' Estado completo de la plantilla de venta.
    ''' Issue #287: Modelo unificado para el estado.
    ''' </summary>
    Private _estado As PlantillaVentaState
    Public Property Estado As PlantillaVentaState
        Get
            If _estado Is Nothing Then
                _estado = New PlantillaVentaState()
            End If
            Return _estado
        End Get
        Set(value As PlantillaVentaState)
            SetProperty(_estado, value)
        End Set
    End Property

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IPlantillaVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService, servicioPedidosVenta As IPedidoVentaService, servicioBorradores As IBorradorPlantillaVentaService)
        Me.configuracion = configuracion
        Me.container = container
        Me.regionManager = regionManager
        Me.servicio = servicio
        Me.eventAggregator = eventAggregator
        Me.dialogService = dialogService
        Me.servicioPedidosVenta = servicioPedidosVenta
        Me.servicioBorradores = servicioBorradores

        Titulo = "Plantilla Ventas"

        cmdAbrirPlantillaVenta = New DelegateCommand(Of Object)(AddressOf OnAbrirPlantillaVenta, AddressOf CanAbrirPlantillaVenta)
        cmdActualizarPrecioProducto = New DelegateCommand(Of Object)(AddressOf OnActualizarPrecioProducto, AddressOf CanActualizarPrecioProducto)
        cmdActualizarProductosPedido = New DelegateCommand(Of LineaPlantillaVenta)(AddressOf OnActualizarProductosPedido, AddressOf CanActualizarProductosPedido)
        BuscarContextualCommand = New DelegateCommand(Of String)(AddressOf OnBuscarContextual, AddressOf CanBuscarContextual)
        cmdBuscarEnTodosLosProductos = New DelegateCommand(Of String)(AddressOf OnBuscarEnTodosLosProductos, AddressOf CanBuscarEnTodosLosProductos)
        cmdCargarClientesVendedor = New DelegateCommand(AddressOf OnCargarClientesVendedor, AddressOf CanCargarClientesVendedor)
        cmdCargarFormasVenta = New DelegateCommand(Of Object)(AddressOf OnCargarFormasVenta, AddressOf CanCargarFormasVenta)
        CargarProductoCommand = New DelegateCommand(Of Object)(AddressOf OnCargarProducto, AddressOf CanCargarProducto)
        cmdCargarProductosPlantilla = New DelegateCommand(AddressOf OnCargarProductosPlantilla)
        cmdCargarStockProducto = New DelegateCommand(Of LineaPlantillaVenta)(AddressOf OnCargarStockProducto)
        cmdCargarUltimasVentas = New DelegateCommand(Of Object)(AddressOf OnCargarUltimasVentas, AddressOf CanCargarUltimasVentas)
        cmdComprobarPendientes = New DelegateCommand(AddressOf OnComprobarPendientes)
        cmdCrearPedido = New DelegateCommand(AddressOf OnCrearPedido, AddressOf CanCrearPedido)
        cmdInsertarProducto = New DelegateCommand(Of Object)(AddressOf OnInsertarProducto, AddressOf CanInsertarProducto)
        cmdCalcularSePuedeServirPorGlovo = New DelegateCommand(AddressOf OnCalcularSePuedeServirPorGlovo)
        CambiarIvaCommand = New DelegateCommand(AddressOf OnCambiarIva)
        CargarCorreoYMovilTarjeta = New DelegateCommand(AddressOf OnCargarCorreoYMovilTarjeta)
        NoAmpliarPedidoCommand = New DelegateCommand(AddressOf OnNoAmpliarPedido, AddressOf CanNoAmpliarPedido)
        SoloConStockCommand = New DelegateCommand(AddressOf OnSoloConStock, AddressOf CanSoloConStock)
        CopiarClientePortapapelesCommand = New DelegateCommand(AddressOf OnCopiarClientePortapapeles)
        MostrarGanavisionesCommand = New DelegateCommand(AddressOf OnMostrarGanavisiones, AddressOf CanMostrarGanavisiones)
        cmdCargarProductosBonificables = New DelegateCommand(AddressOf OnCargarProductosBonificables, AddressOf CanCargarProductosBonificables)
        cmdActualizarRegalo = New DelegateCommand(Of LineaRegalo)(AddressOf OnActualizarRegalo)
        cmdValidarServirJunto = New DelegateCommand(AddressOf OnValidarServirJunto)

        ' Issue #286: Borradores de PlantillaVenta
        GuardarBorradorCommand = New DelegateCommand(AddressOf OnGuardarBorrador, AddressOf CanGuardarBorrador)
        CargarBorradorCommand = New DelegateCommand(Of BorradorPlantillaVenta)(AddressOf OnCargarBorrador)
        CopiarBorradorJsonCommand = New DelegateCommand(Of BorradorPlantillaVenta)(AddressOf OnCopiarBorradorJson)
        EliminarBorradorCommand = New DelegateCommand(Of BorradorPlantillaVenta)(AddressOf OnEliminarBorrador)
        ActualizarListaBorradoresCommand = New DelegateCommand(AddressOf OnActualizarListaBorradores)
        CrearBorradorDesdeJsonCommand = New DelegateCommand(AddressOf OnCrearBorradorDesdeJson)
        ' Cargar lista de borradores inmediatamente
        OnActualizarListaBorradores()
        ' Issue #288: Comprobar si hay JSON válido en el portapapeles
        ComprobarPortapapeles()

        ListaFiltrableRegalos = New ColeccionFiltrable(New ObservableCollection(Of LineaRegalo)) With {
            .TieneDatosIniciales = True
        }

        ' Al leer los clientes lo lee del parámetro PermitirVerClientesTodosLosVendedores
        todosLosVendedores = False

        listaAlmacenes = New ObservableCollection(Of Sede)(Constantes.Sedes.ListaSedes)
        Dim almacenRuta As String = configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)
        almacenSeleccionado = listaAlmacenes.Single(Function(a) a.Codigo = almacenRuta)

        ListaFiltrableProductos = New ColeccionFiltrable(New ObservableCollection(Of LineaPlantillaVenta)) With {
            .TieneDatosIniciales = True
        }
        AddHandler ListaFiltrableProductos.FiltroChanged, Sub(sender As Object, args As EventArgs)
                                                              cmdBuscarEnTodosLosProductos.RaiseCanExecuteChanged()
                                                          End Sub
        AddHandler ListaFiltrableProductos.HayQueCargarDatos, Async Sub()
                                                                  Await Task.Delay(400)
                                                                  cmdBuscarEnTodosLosProductos.Execute(ListaFiltrableProductos.Filtro)
                                                              End Sub
        AddHandler ListaFiltrableProductos.ListaChanged, Sub()
                                                             RaisePropertyChanged(NameOf(listaProductosPedido))
                                                             RaisePropertyChanged(NameOf(baseImponiblePedido))
                                                             RaisePropertyChanged(NameOf(totalPedido))
                                                             RaisePropertyChanged(NameOf(HayGanavisionesDisponibles))
                                                             SoloConStockCommand.RaiseCanExecuteChanged()
                                                         End Sub
        AddHandler ListaFiltrableProductos.ElementoSeleccionadoChanged, Sub(sender As Object, args As EventArgs)
                                                                            cmdCargarUltimasVentas.Execute(ListaFiltrableProductos.ElementoSeleccionado)
                                                                        End Sub

        EsBusquedaConAND = configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.UsarBusquedaContextualAND) = "1"
        EsBusquedaConOR = Not EsBusquedaConAND

        Dim unused = eventAggregator.GetEvent(Of ClienteCreadoEvent).Subscribe(AddressOf ActualizarCliente)

    End Sub



#Region "Propiedades"
    '*** Propiedades de Nesto
    Private vendedor As String
    Private ultimaOferta As Integer = 0

    ''' <summary>
    ''' Flag que indica que estamos cargando desde un borrador.
    ''' Issue #286: Se usa para evitar que otros procesos sobrescriban los datos del borrador.
    ''' </summary>
    Private _cargandoDesdeBorrador As Boolean = False

    ''' <summary>
    ''' Borrador que se está restaurando actualmente.
    ''' Issue #286: Contiene los datos originales para restaurar después de procesos async.
    ''' </summary>
    Private _borradorEnRestauracion As BorradorPlantillaVenta

    ''' <summary>
    ''' Flag que indica si ya se restauraron los valores del borrador en OnCargarFormasVenta.
    ''' Issue #286: Evita aplicar los valores múltiples veces si el usuario navega varias veces.
    ''' </summary>
    Private _borradorRestauradoEnFormasVenta As Boolean = False

    Public Property AlmacenAnterior As String

    Private _almacenEntregaUrgente As String
    Public Property AlmacenEntregaUrgente As String
        Get
            Return _almacenEntregaUrgente
        End Get
        Set(value As String)
            Dim unused = SetProperty(_almacenEntregaUrgente, value)
        End Set
    End Property

    ''' <summary>
    ''' Almacén seleccionado para el pedido. Sincroniza código con Estado.AlmacenCodigo.
    ''' </summary>
    Private _almacenSeleccionado As Sede
    Public Property almacenSeleccionado As Sede
        Get
            Return _almacenSeleccionado
        End Get
        Set(value As Sede)
            Dim unused = SetProperty(_almacenSeleccionado, value)
            ' Sincronizar código con Estado
            Estado.AlmacenCodigo = If(value IsNot Nothing, value.Codigo, Nothing)
            If Not IsNothing(ListaFiltrableProductos) AndAlso Not IsNothing(ListaFiltrableProductos.Lista) Then
                Application.Current.Dispatcher.Invoke(New Action(Async Sub()
                                                                     estaOcupado = True
                                                                     Dim listaCast As New ObservableCollection(Of LineaPlantillaVenta)
                                                                     For Each linea In ListaFiltrableProductos.ListaOriginal
                                                                         listaCast.Add(linea)
                                                                     Next
                                                                     Dim nuevosStocks As ObservableCollection(Of LineaPlantillaVenta) = Await servicio.PonerStocks(listaCast, value.Codigo, Constantes.Almacenes.ALMACENES_STOCK)
                                                                     ListaFiltrableProductos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(nuevosStocks)
                                                                     For Each linea In nuevosStocks
                                                                         If linea.StockDisponibleTodosLosAlmacenes = 0 AndAlso linea.stocks IsNot Nothing AndAlso linea.stocks.Count > 0 Then
                                                                             linea.StockDisponibleTodosLosAlmacenes = linea.stocks.Sum(Function(s) s.cantidadDisponible)
                                                                         End If
                                                                         linea.stockActualizado = True
                                                                     Next
                                                                     estaOcupado = False
                                                                     Dim fechaPuente = Await ObtenerFechaMinimaEntregaAsync()
                                                                     fechaMinimaEntrega = fechaPuente
                                                                     fechaEntrega = fechaPuente
                                                                 End Sub))
            End If
        End Set
    End Property


    Public ReadOnly Property baseImponiblePedido As Decimal
        Get
            Dim baseImponible As Decimal = 0
            If Not IsNothing(listaProductosPedido) AndAlso listaProductosPedido.Count > 0 Then
                baseImponible = listaProductosPedido.Sum(Function(l) (l.cantidad * l.precio) - Math.Round(l.cantidad * l.precio * l.descuento, 2, MidpointRounding.AwayFromZero))
            End If
            RaisePropertyChanged(NameOf(baseImponibleParaPortes))
            Return baseImponible
        End Get
    End Property

    Public ReadOnly Property baseImponibleParaPortes As Decimal
        Get
            Dim baseImponible As Decimal = 0
            If Not IsNothing(listaProductosPedido) AndAlso listaProductosPedido.Count > 0 Then
                baseImponible = listaProductosPedido.Where(Function(l) l.esSobrePedido = False).Sum(Function(l) (l.cantidad * l.precio) - Math.Round(l.precio * l.descuento * l.cantidad, 2, MidpointRounding.AwayFromZero))
            End If
            Return baseImponible
        End Get
    End Property

#Region "Ganavisiones - FASE 7"
    ''' <summary>
    ''' Grupos de productos que generan Ganavisiones (COS = Cosmetica, ACC = Accesorios, PEL = Peluqueria).
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Private Shared ReadOnly GRUPOS_BONIFICABLES As String() = {"COS", "ACC", "PEL"}

    ''' <summary>
    ''' Valor en EUR de cada Ganavision (1 Ganavision = 10 EUR).
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Private Const VALOR_GANAVISION_EN_EUROS As Decimal = 10D

    ''' <summary>
    ''' Base imponible de las lineas del pedido cuyos productos pertenecen a grupos bonificables (COS, ACC, PEL).
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public ReadOnly Property BaseImponibleBonificable As Decimal
        Get
            If IsNothing(listaProductosPedido) OrElse listaProductosPedido.Count = 0 Then
                Return 0
            End If
            Return listaProductosPedido _
                .Where(Function(l) Not String.IsNullOrEmpty(l.grupo) AndAlso GRUPOS_BONIFICABLES.Contains(l.grupo.Trim().ToUpper())) _
                .Sum(Function(l) (l.cantidad * l.precio) - Math.Round(l.cantidad * l.precio * l.descuento, 2, MidpointRounding.AwayFromZero))
        End Get
    End Property

    ''' <summary>
    ''' Ganavisiones disponibles segun la base imponible bonificable (truncado, no redondeado).
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public ReadOnly Property GanavisionesDisponibles As Integer
        Get
            Return CInt(Math.Truncate(BaseImponibleBonificable / VALOR_GANAVISION_EN_EUROS))
        End Get
    End Property

    ''' <summary>
    ''' Ganavisiones consumidos por los regalos seleccionados.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public ReadOnly Property GanavisionesUsados As Integer
        Get
            If IsNothing(ListaRegalosSeleccionados) OrElse ListaRegalosSeleccionados.Count = 0 Then
                Return 0
            End If
            Return ListaRegalosSeleccionados.Sum(Function(r) r.cantidad * r.ganavisiones)
        End Get
    End Property

    ''' <summary>
    ''' Porcentaje de Ganavisiones usados sobre disponibles (para barra de progreso).
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public ReadOnly Property PorcentajeGanavisionesUsados As Integer
        Get
            If GanavisionesDisponibles = 0 Then Return 0
            Return Math.Min(100, CInt((GanavisionesUsados / GanavisionesDisponibles) * 100))
        End Get
    End Property

    ''' <summary>
    ''' Indica si hay Ganavisiones disponibles para canjear regalos.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' Debe haber puntos disponibles Y productos bonificables en la tabla Ganavisiones.
    ''' </summary>
    Public ReadOnly Property HayGanavisionesDisponibles As Boolean
        Get
            Return GanavisionesDisponibles > 0 AndAlso
                   _productosBonificablesIds IsNot Nothing AndAlso
                   _productosBonificablesIds.Count > 0
        End Get
    End Property

    ''' <summary>
    ''' Indica si se han excedido los Ganavisiones disponibles.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public ReadOnly Property GanavisionesExcedidos As Boolean
        Get
            Return GanavisionesUsados > GanavisionesDisponibles
        End Get
    End Property

    ''' <summary>
    ''' Indica si se puede pasar de la página de regalos (no se han excedido los Ganavisiones).
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public ReadOnly Property PuedePasarDePaginaRegalos As Boolean
        Get
            Return Not GanavisionesExcedidos
        End Get
    End Property

    Private _listaProductosBonificables As ObservableCollection(Of LineaRegalo)
    ''' <summary>
    ''' Lista de productos que se pueden seleccionar como regalo.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public Property ListaProductosBonificables As ObservableCollection(Of LineaRegalo)
        Get
            Return _listaProductosBonificables
        End Get
        Set(value As ObservableCollection(Of LineaRegalo))
            SetProperty(_listaProductosBonificables, value)
        End Set
    End Property

    ''' <summary>
    ''' Lista de regalos seleccionados (productos bonificables con cantidad > 0).
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public ReadOnly Property ListaRegalosSeleccionados As List(Of LineaRegalo)
        Get
            If IsNothing(ListaProductosBonificables) Then Return New List(Of LineaRegalo)()
            Return ListaProductosBonificables.Where(Function(r) r.cantidad > 0).ToList()
        End Get
    End Property

    Private _listaFiltrableRegalos As ColeccionFiltrable
    ''' <summary>
    ''' Colección filtrable de productos bonificables para el BarraFiltro.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public Property ListaFiltrableRegalos As ColeccionFiltrable
        Get
            Return _listaFiltrableRegalos
        End Get
        Set(value As ColeccionFiltrable)
            SetProperty(_listaFiltrableRegalos, value)
        End Set
    End Property

    Private _cmdCargarProductosBonificables As DelegateCommand
    ''' <summary>
    ''' Comando para cargar los productos bonificables desde la API.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public Property cmdCargarProductosBonificables As DelegateCommand
        Get
            Return _cmdCargarProductosBonificables
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCargarProductosBonificables, value)
        End Set
    End Property
    Private Function CanCargarProductosBonificables() As Boolean
        Return Not IsNothing(clienteSeleccionado)
    End Function
    Private Async Sub OnCargarProductosBonificables()
        Await CargarProductosBonificablesAsync()
    End Sub

    ''' <summary>
    ''' Carga los productos bonificables de forma awaitable.
    ''' Issue #286: Separado para poder usar desde OnCargarBorrador.
    ''' </summary>
    Private Async Function CargarProductosBonificablesAsync() As Task
        Try
            estaOcupado = True

            ' Notificar los indicadores al entrar en la página
            RaisePropertyChanged(NameOf(BaseImponibleBonificable))
            RaisePropertyChanged(NameOf(GanavisionesDisponibles))

            ' Issue #286: Guardar los regalos del borrador antes de cargar la lista completa
            Dim regalosBorrador As List(Of LineaRegalo) = Nothing
            If _borradorEnRestauracion IsNot Nothing AndAlso _borradorEnRestauracion.LineasRegalo IsNot Nothing Then
                regalosBorrador = _borradorEnRestauracion.LineasRegalo.ToList()
            End If

            ' Si no hay Ganavisiones disponibles, no cargar productos
            If GanavisionesDisponibles <= 0 Then
                ListaProductosBonificables = New ObservableCollection(Of LineaRegalo)()
                ListaFiltrableRegalos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)()
                ActualizarIndicadoresGanavisiones()
                Return
            End If

            Dim servirJunto As Boolean = If(direccionEntregaSeleccionada?.servirJunto, True)
            Dim almacen As String = If(almacenSeleccionado?.Codigo, "ALG")

            Dim respuesta = Await servicio.CargarProductosBonificablesParaPedido(
                Constantes.Empresas.EMPRESA_DEFECTO,
                BaseImponibleBonificable,
                almacen,
                servirJunto,
                clienteSeleccionado.cliente).ConfigureAwait(True)

            If Not IsNothing(respuesta) AndAlso Not IsNothing(respuesta.Productos) Then
                ListaProductosBonificables = New ObservableCollection(Of LineaRegalo)(
                    respuesta.Productos.Select(Function(p) New LineaRegalo With {
                        .producto = p.ProductoId,
                        .texto = p.ProductoNombre,
                        .precio = p.PVP,
                        .ganavisiones = p.Ganavisiones,
                        .iva = p.Iva,
                        .stocks = p.Stocks
                    }))

                ' Issue #286: Aplicar cantidades del borrador a los productos cargados
                If regalosBorrador IsNot Nothing AndAlso regalosBorrador.Any() Then
                    For Each regaloBorrador In regalosBorrador
                        Dim regaloExistente = ListaProductosBonificables.FirstOrDefault(
                            Function(r) r.producto?.Trim() = regaloBorrador.producto?.Trim())
                        If regaloExistente IsNot Nothing Then
                            regaloExistente.cantidad = regaloBorrador.cantidad
                        End If
                    Next
                End If

                ' Actualizar la colección filtrable con los productos
                ListaFiltrableRegalos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(ListaProductosBonificables)

                ' Suscribirse a cambios en cantidad para actualizar los indicadores
                For Each linea In ListaProductosBonificables
                    AddHandler linea.PropertyChanged, AddressOf OnRegaloPropertyChanged
                Next
            Else
                ListaProductosBonificables = New ObservableCollection(Of LineaRegalo)()
                ListaFiltrableRegalos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)()
            End If

            ActualizarIndicadoresGanavisiones()
        Catch ex As Exception
            dialogService.ShowError("Error al cargar productos bonificables: " & ex.Message)
        Finally
            estaOcupado = False
        End Try
    End Function

    Private Sub OnRegaloPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(LineaRegalo.cantidad) Then
            ActualizarIndicadoresGanavisiones()
        End If
    End Sub

    Private Sub ActualizarIndicadoresGanavisiones()
        RaisePropertyChanged(NameOf(GanavisionesUsados))
        RaisePropertyChanged(NameOf(PorcentajeGanavisionesUsados))
        RaisePropertyChanged(NameOf(GanavisionesExcedidos))
        RaisePropertyChanged(NameOf(PuedePasarDePaginaRegalos))
        RaisePropertyChanged(NameOf(ListaRegalosSeleccionados))
    End Sub

    Private _cmdActualizarRegalo As DelegateCommand(Of LineaRegalo)
    ''' <summary>
    ''' Comando para actualizar un regalo cuando cambia la cantidad.
    ''' Carga la imagen del producto desde la API.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Public Property cmdActualizarRegalo As DelegateCommand(Of LineaRegalo)
        Get
            Return _cmdActualizarRegalo
        End Get
        Private Set(value As DelegateCommand(Of LineaRegalo))
            SetProperty(_cmdActualizarRegalo, value)
        End Set
    End Property

    Private Async Sub OnActualizarRegalo(regalo As LineaRegalo)
        If IsNothing(regalo) OrElse IsNothing(clienteSeleccionado) Then
            Return
        End If

        ' Si tiene cantidad y no tiene imagen, cargar la imagen
        If regalo.cantidad > 0 AndAlso String.IsNullOrEmpty(regalo.urlImagen) Then
            Try
                Using client As New HttpClient
                    client.BaseAddress = New Uri(configuracion.servidorAPI)
                    Dim response As HttpResponseMessage

                    Dim almacen As String = If(almacenSeleccionado?.Codigo, "ALG")
                    response = Await client.GetAsync("PlantillaVentas/CargarStocks?empresa=" + clienteSeleccionado.empresa + "&almacen=" + almacen + "&productoStock=" + regalo.producto)

                    If response.IsSuccessStatusCode Then
                        Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                        Dim datosStock = JsonConvert.DeserializeObject(Of StockProductoDTO)(cadenaJson)
                        If datosStock IsNot Nothing Then
                            regalo.urlImagen = datosStock.urlImagen
                        End If
                    End If
                End Using
            Catch ex As Exception
                ' Ignorar errores de carga de imagen, no es crítico
            End Try
        End If

        ActualizarIndicadoresGanavisiones()
    End Sub

    Private _cmdValidarServirJunto As DelegateCommand
    ''' <summary>
    ''' Comando para validar si se puede desmarcar ServirJunto con productos bonificados.
    ''' Issue #94: Sistema Ganavisiones - FASE 9
    ''' </summary>
    Public Property cmdValidarServirJunto As DelegateCommand
        Get
            Return _cmdValidarServirJunto
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdValidarServirJunto, value)
        End Set
    End Property

    Private Async Sub OnValidarServirJunto()
        ' Issue #286: Si direccionEntregaSeleccionada es Nothing (durante restauración de borrador), salir
        If direccionEntregaSeleccionada Is Nothing Then
            Return
        End If

        ' Si se está marcando (no desmarcando), no validar
        If direccionEntregaSeleccionada.servirJunto Then
            Return
        End If

        ' Si no hay regalos seleccionados, no validar
        Dim regalosSeleccionados = ListaRegalosSeleccionados
        If regalosSeleccionados Is Nothing OrElse Not regalosSeleccionados.Any() Then
            Return
        End If

        Try
            Dim almacen As String = If(almacenSeleccionado?.Codigo, "ALG")
            Dim productosIds = regalosSeleccionados.Select(Function(r) r.producto).ToList()

            Dim respuesta = Await servicio.ValidarServirJunto(almacen, productosIds).ConfigureAwait(True)

            If Not respuesta.PuedeDesmarcar Then
                ' Revertir el cambio - volver a marcar ServirJunto
                direccionEntregaSeleccionada.servirJunto = True
                RaisePropertyChanged(NameOf(direccionEntregaSeleccionada))

                ' Mostrar mensaje al usuario
                dialogService.ShowError(respuesta.Mensaje)
            End If
        Catch ex As Exception
            ' Si hay error, permitir desmarcar (fail-safe)
        End Try
    End Sub
#End Region

    Private _clienteSeleccionado As ClienteJson
    Public Property clienteSeleccionado As ClienteJson
        Get
            Return _clienteSeleccionado
        End Get
        Set(ByVal value As ClienteJson)
            If Not IsNothing(value) AndAlso String.IsNullOrWhiteSpace(value.cifNif) Then
                If ultimoClienteAbierto = value.cliente Then
                    Dim mensajeError As String = String.Format("A este cliente le faltan datos. Si continua es{0}posible que no pueda finalizar el pedido. Elija entre rellenar los{0}datos que faltan (Cancel) o continuar con el pedido (OK)", Environment.NewLine)
                    Dim continuar As Boolean
                    dialogService.ShowConfirmation("Faltan datos en el cliente", mensajeError, Sub(r)
                                                                                                   continuar = r.Result = ButtonResult.OK
                                                                                               End Sub)
                    If continuar Then
                        SeleccionarElCliente(value)
                    Else
                        NavegarAClienteCrear(value)
                    End If
                Else
                    ultimoClienteAbierto = value.cliente
                    NavegarAClienteCrear(value)
                End If
            ElseIf Not IsNothing(value) Then
                SeleccionarElCliente(value)
            End If
            If _clienteSeleccionado.cliente = Constantes.Clientes.Especiales.EL_EDEN Then
                fechaMinimaEntrega = Date.Today
                fechaEntrega = Date.Today
            End If
            RaisePropertyChanged(NameOf(SePuedeFinalizar))
        End Set
    End Property

    ''' <summary>
    ''' Email para enviar cobro por tarjeta. Delegado a Estado.
    ''' </summary>
    Public Property CobroTarjetaCorreo As String
        Get
            Return Estado.CobroTarjetaCorreo
        End Get
        Set(value As String)
            If Estado.CobroTarjetaCorreo <> value Then
                Estado.CobroTarjetaCorreo = value
                RaisePropertyChanged()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Móvil para enviar cobro por tarjeta. Delegado a Estado.
    ''' </summary>
    Public Property CobroTarjetaMovil As String
        Get
            Return Estado.CobroTarjetaMovil
        End Get
        Set(value As String)
            If Estado.CobroTarjetaMovil <> value Then
                Estado.CobroTarjetaMovil = value
                RaisePropertyChanged()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Comentario para la ruta de entrega. Delegado a Estado.
    ''' </summary>
    Public Property ComentarioRuta As String
        Get
            Return Estado.ComentarioRuta
        End Get
        Set(value As String)
            If Estado.ComentarioRuta <> value Then
                Estado.ComentarioRuta = value
                RaisePropertyChanged()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Comentario de picking introducido por el usuario. Delegado a Estado.
    ''' Diferente de ComentarioPickingCliente que viene del cliente.
    ''' </summary>
    Public Property ComentarioPicking As String
        Get
            Return Estado.ComentarioPicking
        End Get
        Set(value As String)
            If Estado.ComentarioPicking <> value Then
                Estado.ComentarioPicking = value
                RaisePropertyChanged()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Código del contacto/dirección de entrega seleccionado. Delegado a Estado.
    ''' Usado para binding con SelectorDireccionEntrega.Seleccionada
    ''' </summary>
    Public Property ContactoSeleccionado As String
        Get
            Return Estado.Contacto
        End Get
        Set(value As String)
            If Estado.Contacto <> value Then
                Estado.Contacto = value
                RaisePropertyChanged()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Dirección de entrega seleccionada. Sincroniza datos con Estado.
    ''' </summary>
    Private _direccionEntregaSeleccionada As DireccionesEntregaCliente
    Public Property direccionEntregaSeleccionada As DireccionesEntregaCliente
        Get
            Return _direccionEntregaSeleccionada
        End Get
        Set(ByVal value As DireccionesEntregaCliente)
            If ComentarioRuta = _direccionEntregaSeleccionada?.comentarioRuta Then
                ComentarioRuta = String.Empty
            End If
            Dim codigoPostalAnterior = _direccionEntregaSeleccionada?.codigoPostal
            ' Si estamos restaurando un borrador, aplicar valores ANTES de SetProperty
            ' para que PropertyChanged notifique los bindings con los valores correctos
            If value IsNot Nothing AndAlso _borradorEnRestauracion IsNot Nothing Then
                value.mantenerJunto = _borradorEnRestauracion.MantenerJunto
                value.servirJunto = _borradorEnRestauracion.ServirJunto
            End If
            Dim unused = SetProperty(_direccionEntregaSeleccionada, value)

            ' Sincronizar datos de dirección con Estado
            If value IsNot Nothing Then
                Estado.Contacto = value.contacto
                Estado.Vendedor = value.vendedor
                Estado.PeriodoFacturacion = value.periodoFacturacion
                Estado.Ruta = value.ruta
                Estado.Ccc = value.ccc
                Estado.NoComisiona = value.noComisiona
                Estado.MantenerJunto = value.mantenerJunto
                Estado.ServirJunto = value.servirJunto
            End If

            If PlazoPagoCliente <> _direccionEntregaSeleccionada?.plazosPago Then
                PlazoPagoCliente = _direccionEntregaSeleccionada?.plazosPago
            ElseIf _direccionEntregaSeleccionada.codigoPostal <> codigoPostalAnterior Then
                cmdCalcularSePuedeServirPorGlovo.Execute()
            End If

            FormaPagoCliente = _direccionEntregaSeleccionada?.formaPago
            If String.IsNullOrEmpty(ComentarioRuta) AndAlso Not IsNothing(_direccionEntregaSeleccionada) Then
                ComentarioRuta = _direccionEntregaSeleccionada.comentarioRuta
            End If
            If fechaEntrega < fechaMinimaEntrega Then
                fechaEntrega = fechaMinimaEntrega
            End If
            RaisePropertyChanged(NameOf(textoFacturacionElectronica))
            'RaisePropertyChanged(NameOf(fechaMinimaEntrega))
            ' Se hace así para que coja la fecha de hoy cuando se pueda
            ' Si lo hacemos en otro orden, da error porque ponemos una fecha
            ' menor a la que nos permite el datapicker
            'If fechaMinimaEntrega < fechaEntrega Then
            '    fechaEntrega = fechaMinimaEntrega
            'End If
        End Set
    End Property

    Private _direccionGoogleMaps As String
    Public Property DireccionGoogleMaps As String
        Get
            Return _direccionGoogleMaps
        End Get
        Set(value As String)
            Dim unused = SetProperty(_direccionGoogleMaps, value)
        End Set
    End Property

    ''' <summary>
    ''' Si se envía por Glovo (entrega urgente). Delegado a Estado.
    ''' El setter cambia el almacén automáticamente.
    ''' </summary>
    Public Property EnviarPorGlovo As Boolean
        Get
            Return Estado.EnviarPorGlovo
        End Get
        Set(value As Boolean)
            If Estado.EnviarPorGlovo <> value Then
                Estado.EnviarPorGlovo = value
                RaisePropertyChanged()
                If Estado.EnviarPorGlovo Then
                    AlmacenAnterior = almacenSeleccionado.Codigo
                    almacenSeleccionado = listaAlmacenes.Single(Function(a) a.Codigo = AlmacenEntregaUrgente)
                Else
                    almacenSeleccionado = listaAlmacenes.Single(Function(a) a.Codigo = AlmacenAnterior)
                End If
            End If
        End Set
    End Property

    Private _esBusquedaConAND As Boolean
    Public Property EsBusquedaConAND As Boolean
        Get
            Return _esBusquedaConAND
        End Get
        Set(value As Boolean)
            If SetProperty(_esBusquedaConAND, value) Then
                configuracion.GuardarParametroSync(
                    Constantes.Empresas.EMPRESA_DEFECTO,
                    Parametros.Claves.UsarBusquedaContextualAND,
                    If(EsBusquedaConAND, "1", "0")
                )
                If value Then
                    EsBusquedaConOR = False
                End If
            End If
        End Set
    End Property

    Private _esBusquedaConOR As Boolean
    Public Property EsBusquedaConOR As Boolean
        Get
            Return _esBusquedaConOR
        End Get
        Set(value As Boolean)
            If SetProperty(_esBusquedaConOR, value) Then
                If value Then
                    EsBusquedaConAND = False
                End If
            End If
        End Set
    End Property

    ''' <summary>
    ''' Si es un presupuesto en lugar de un pedido. Delegado a Estado.
    ''' </summary>
    Public Property EsPresupuesto As Boolean
        Get
            Return Estado.EsPresupuesto
        End Get
        Set(value As Boolean)
            If Estado.EsPresupuesto <> value Then
                Estado.EsPresupuesto = value
                RaisePropertyChanged()
                RaisePropertyChanged(NameOf(SePuedeFinalizar))
            End If
        End Set
    End Property
    Public ReadOnly Property EsTarjetaPrepago As Boolean
        Get
            Return Not IsNothing(FormaPagoSeleccionada) AndAlso Not IsNothing(plazoPagoSeleccionado) AndAlso
                FormaPagoSeleccionada.formaPago = Constantes.FormasPago.TARJETA AndAlso
                plazoPagoSeleccionado.plazoPago = Constantes.PlazosPago.PREPAGO
        End Get
    End Property

    Private _estanGanavisionesMostrados As Boolean
    Public Property EstanGanavisionesMostrados As Boolean
        Get
            Return _estanGanavisionesMostrados
        End Get
        Set(value As Boolean)
            Try
                If value Then
                    MostrarGanavisionesCommand.Execute()
                End If
                Dim listaIntermedia As ObservableCollection(Of IFiltrableItem) = ListaFiltrableProductos.Lista
                ListaFiltrableProductos.Lista = New ObservableCollection(Of IFiltrableItem)(ListaProductosGanavisiones)
                ListaProductosGanavisiones = listaIntermedia
                Dim unused = SetProperty(_estanGanavisionesMostrados, value)
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            End Try
        End Set
    End Property

    Private _estaOcupado As Boolean = False
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(ByVal value As Boolean)
            Dim unused = SetProperty(_estaOcupado, value)
        End Set
    End Property

    ''' <summary>
    ''' Fecha de entrega solicitada. Delegado a Estado.
    ''' </summary>
    Public Property fechaEntrega As Date
        Get
            Return Estado.FechaEntrega
        End Get
        Set(ByVal value As Date)
            If value < fechaMinimaEntrega AndAlso fechaMinimaEntrega > Date.MinValue Then
                value = fechaMinimaEntrega
            End If
            If Estado.FechaEntrega <> value Then
                Estado.FechaEntrega = value
                RaisePropertyChanged()
            End If
        End Set
    End Property

    Private _fechaMinimaEntrega As Date
    Public Property fechaMinimaEntrega As Date
        Get
            Return _fechaMinimaEntrega
        End Get
        Set
            If _fechaMinimaEntrega < Value Then
                ' Issue #288: No sobreescribir la fecha del borrador con la fecha mínima
                Dim fechaReferencia = fechaEntrega
                If _borradorEnRestauracion IsNot Nothing AndAlso _borradorEnRestauracion.FechaEntrega > Date.MinValue Then
                    fechaReferencia = _borradorEnRestauracion.FechaEntrega
                End If
                If fechaReferencia < Value Then
                    fechaEntrega = Value
                End If
                Dim unused1 = SetProperty(_fechaMinimaEntrega, Value)
            End If

            If _fechaMinimaEntrega > Value Then
                Dim unused = SetProperty(_fechaMinimaEntrega, Value)
                Dim fechaReferencia2 = fechaEntrega
                If _borradorEnRestauracion IsNot Nothing AndAlso _borradorEnRestauracion.FechaEntrega > Date.MinValue Then
                    fechaReferencia2 = _borradorEnRestauracion.FechaEntrega
                End If
                If fechaReferencia2 < Value Then
                    fechaEntrega = Value
                End If
            End If
        End Set
    End Property

    Public Async Function ObtenerFechaMinimaEntregaAsync() As Task(Of Date)
        Try
            Return If(clienteSeleccionado?.cliente = Constantes.Clientes.Especiales.EL_EDEN,
                Await Task.FromResult(Date.Today),
                Await servicio.CalcularFechaEntrega(Date.Now, direccionEntregaSeleccionada?.ruta, almacenSeleccionado?.Codigo))
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return Nothing
        End Try
    End Function

    Private _filtroCliente As String
    Public Property filtroCliente As String
        Get
            Return _filtroCliente
        End Get
        Set(ByVal value As String)
            Dim unused = SetProperty(_filtroCliente, value.ToLower)
            If Not IsNothing(listaClientes) Then
                listaClientes = New ObservableCollection(Of ClienteJson)(From l In listaClientesOriginal Where
                    ((l.nombre IsNot Nothing) AndAlso l.nombre.ToLower.Contains(filtroCliente)) OrElse
                    ((l.direccion IsNot Nothing) AndAlso l.direccion.ToLower.Contains(filtroCliente)) OrElse
                    ((l.telefono IsNot Nothing) AndAlso l.telefono.Contains(filtroCliente)) OrElse
                    ((l.poblacion IsNot Nothing) AndAlso l.poblacion.ToLower.Contains(filtroCliente))
                )
            End If
        End Set
    End Property


    Private _formaPagoCliente As String
    Public Property FormaPagoCliente As String
        Get
            Return _formaPagoCliente
        End Get
        Set(value As String)
            Dim unused = SetProperty(_formaPagoCliente, value)
            cmdCrearPedido.RaiseCanExecuteChanged()
        End Set
    End Property

    ''' <summary>
    ''' Forma de pago seleccionada. Sincroniza código con Estado.FormaPago.
    ''' </summary>
    Private _formaPagoSeleccionada As FormaPago
    Public Property FormaPagoSeleccionada() As FormaPago
        Get
            Return _formaPagoSeleccionada
        End Get
        Set(ByVal value As FormaPago)
            If IsNothing(_formaPagoSeleccionada) AndAlso IsNothing(value) Then
                Return
            End If
            Dim unused = SetProperty(_formaPagoSeleccionada, value)
            ' Sincronizar código con Estado
            Estado.FormaPago = If(value IsNot Nothing, value.formaPago, Nothing)
            cmdCrearPedido.RaiseCanExecuteChanged()
            RaisePropertyChanged(NameOf(SePuedeFinalizar))
            RaisePropertyChanged(NameOf(EsTarjetaPrepago))
            RaisePropertyChanged(NameOf(MandarCobroTarjeta))
        End Set
    End Property

    Private ReadOnly _formaVentaDirecta As Boolean
    Public Property formaVentaDirecta() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(1)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 1
        End Set
    End Property

    Private ReadOnly _formaVentaOtras As Boolean
    Public Property formaVentaOtras() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(3)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 3
        End Set
    End Property

    Private ReadOnly _formaVentaTelefono As Boolean
    Public Property formaVentaTelefono() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(2)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 2
        End Set
    End Property

    ''' <summary>
    ''' Forma de venta seleccionada (1=Directa, 2=Teléfono, 3+=Otras). Delegado a Estado.
    ''' </summary>
    Public Property formaVentaSeleccionada() As Integer
        Get
            Return Estado.FormaVenta
        End Get
        Set(ByVal value As Integer)
            If Estado.FormaVenta <> value Then
                Estado.FormaVenta = value
                RaisePropertyChanged()
                RaisePropertyChanged(NameOf(formaVentaDirecta))
                RaisePropertyChanged(NameOf(formaVentaTelefono))
                RaisePropertyChanged(NameOf(formaVentaOtras))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Forma de venta "Otras" seleccionada. Sincroniza código con Estado.FormaVentaOtrasCodigo.
    ''' </summary>
    Private _formaVentaOtrasSeleccionada As FormaVentaDTO
    Public Property formaVentaOtrasSeleccionada As FormaVentaDTO
        Get
            Return _formaVentaOtrasSeleccionada
        End Get
        Set(ByVal value As FormaVentaDTO)
            Dim unused = SetProperty(_formaVentaOtrasSeleccionada, value)
            ' Sincronizar código con Estado
            Estado.FormaVentaOtrasCodigo = If(value IsNot Nothing, value.numero, Nothing)
            RaisePropertyChanged(NameOf(listaFormasVenta))
        End Set
    End Property

    Public ReadOnly Property hayProductosEnElPedido As Boolean
        Get
            Return Not IsNothing(listaProductosPedido) AndAlso listaProductosPedido.Count > 0
        End Get
    End Property

    Public ReadOnly Property hayUnClienteSeleccionado As Boolean
        Get
            Return Not IsNothing(clienteSeleccionado)
        End Get
    End Property

    Private _listaAlmacenes As ObservableCollection(Of Sede)
    Public Property listaAlmacenes As ObservableCollection(Of Sede)
        Get
            Return _listaAlmacenes
        End Get
        Set(value As ObservableCollection(Of Sede))
            Dim unused = SetProperty(_listaAlmacenes, value)
        End Set
    End Property

    Private _listaClientes As ObservableCollection(Of ClienteJson)
    Public Property listaClientes As ObservableCollection(Of ClienteJson)
        Get
            Return _listaClientes
        End Get
        Set(ByVal value As ObservableCollection(Of ClienteJson))
            Dim unused = SetProperty(_listaClientes, value)
            cmdCargarClientesVendedor.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _listaClientesOriginal As ObservableCollection(Of ClienteJson)
    Public Property listaClientesOriginal As ObservableCollection(Of ClienteJson)
        Get
            Return _listaClientesOriginal
        End Get
        Set(ByVal value As ObservableCollection(Of ClienteJson))
            Dim unused = SetProperty(_listaClientesOriginal, value)
            listaClientes = listaClientesOriginal
        End Set
    End Property

    Private _listaFormasPago As ObservableCollection(Of FormaPagoDTO)
    Public Property listaFormasPago() As ObservableCollection(Of FormaPagoDTO)
        Get
            Return _listaFormasPago
        End Get
        Set(ByVal value As ObservableCollection(Of FormaPagoDTO))
            Dim unused = SetProperty(_listaFormasPago, value)
        End Set
    End Property
    Private _listaFormasVenta As ObservableCollection(Of FormaVentaDTO)
    Public Property listaFormasVenta() As ObservableCollection(Of FormaVentaDTO)
        Get
            Return _listaFormasVenta
        End Get
        Set(ByVal value As ObservableCollection(Of FormaVentaDTO))
            Dim unused = SetProperty(_listaFormasVenta, value)
        End Set
    End Property
    Private _listaPedidosPendientes As List(Of Integer)
    Public Property ListaPedidosPendientes As List(Of Integer)
        Get
            Return _listaPedidosPendientes
        End Get
        Set(value As List(Of Integer))
            Dim unused = SetProperty(_listaPedidosPendientes, value)
            RaisePropertyChanged(NameOf(TienePedidosPendientes))
        End Set
    End Property

    Private _listaFiltrableProductos As ColeccionFiltrable
    Public Property ListaFiltrableProductos As ColeccionFiltrable
        Get
            Return _listaFiltrableProductos
        End Get
        Set(value As ColeccionFiltrable)
            Dim unused = SetProperty(_listaFiltrableProductos, value)
        End Set
    End Property
    Private _listaProductosGanavisiones As ObservableCollection(Of IFiltrableItem)
    Public Property ListaProductosGanavisiones As ObservableCollection(Of IFiltrableItem)
        Get
            Return _listaProductosGanavisiones
        End Get
        Set(value As ObservableCollection(Of IFiltrableItem))
            Dim unused = SetProperty(_listaProductosGanavisiones, value)
        End Set
    End Property
    Public ReadOnly Property listaProductosPedido() As ObservableCollection(Of LineaPlantillaVenta)
        Get
            If Not IsNothing(ListaFiltrableProductos.ListaOriginal) Then
                Dim listaDevuelta As New ObservableCollection(Of LineaPlantillaVenta)
                Dim lista = From l In ListaFiltrableProductos.ListaOriginal Where CType(l, LineaPlantillaVenta).cantidad > 0 OrElse CType(l, LineaPlantillaVenta).cantidadOferta > 0 Order By CType(l, LineaPlantillaVenta).fechaInsercion
                For Each item In lista
                    listaDevuelta.Add(item)
                Next
                Return listaDevuelta
            Else
                Return Nothing
            End If
        End Get
    End Property

    Private _listaUltimasVentas As ObservableCollection(Of UltimasVentasProductoClienteDTO)
    Public Property listaUltimasVentas As ObservableCollection(Of UltimasVentasProductoClienteDTO)
        Get
            Return _listaUltimasVentas
        End Get
        Set(value As ObservableCollection(Of UltimasVentasProductoClienteDTO))
            Dim unused = SetProperty(_listaUltimasVentas, value)
        End Set
    End Property

    ''' <summary>
    ''' Si se debe enviar solicitud de cobro por tarjeta. Delegado a Estado.
    ''' La lógica del getter solo lo permite si es tarjeta prepago.
    ''' </summary>
    Public Property MandarCobroTarjeta As Boolean
        Get
            Return Estado.MandarCobroTarjeta AndAlso EsTarjetaPrepago
        End Get
        Set(value As Boolean)
            If Estado.MandarCobroTarjeta <> value Then
                Estado.MandarCobroTarjeta = value
                RaisePropertyChanged()
                If MandarCobroTarjeta Then
                    CargarCorreoYMovilTarjeta.Execute()
                End If
            End If
        End Set
    End Property

    Public ReadOnly Property NoHayProductosEnElPedido
        Get
            Return Not hayProductosEnElPedido
        End Get
    End Property

    Private _paginaActual As WizardPage
    Public Property PaginaActual As WizardPage
        Get
            Return _paginaActual
        End Get
        Set(value As WizardPage)
            Dim unused = SetProperty(_paginaActual, value)
        End Set
    End Property

    Private _paginasWizard As New List(Of WizardPage)
    Public Property PaginasWizard As List(Of WizardPage)
        Get
            Return _paginasWizard
        End Get
        Set(value As List(Of WizardPage))
            Dim unused = SetProperty(_paginasWizard, value)
        End Set
    End Property
    Private _pedidoPendienteSeleccionado As Integer
    Public Property PedidoPendienteSeleccionado As Integer
        Get
            Return _pedidoPendienteSeleccionado
        End Get
        Set(value As Integer)
            Dim unused = SetProperty(_pedidoPendienteSeleccionado, value)
            NoAmpliarPedidoCommand.RaiseCanExecuteChanged()
        End Set
    End Property
    Private _plazoPagoCliente As String
    Public Property PlazoPagoCliente As String
        Get
            Return _plazoPagoCliente
        End Get
        Set(value As String)
            Dim unused = SetProperty(_plazoPagoCliente, value)
            cmdCrearPedido.RaiseCanExecuteChanged()
        End Set
    End Property
    ''' <summary>
    ''' Plazos de pago seleccionados. Sincroniza código y descuento con Estado.
    ''' </summary>
    Private _plazoPagoSeleccionado As PlazosPago
    Public Property plazoPagoSeleccionado As PlazosPago
        Get
            Return _plazoPagoSeleccionado
        End Get
        Set(ByVal value As PlazosPago)
            If (Not IsNothing(value) AndAlso Not IsNothing(_plazoPagoSeleccionado) AndAlso value.plazoPago = _plazoPagoSeleccionado.plazoPago) OrElse
            (IsNothing(value) AndAlso IsNothing(_plazoPagoSeleccionado)) Then
                Return
            End If
            Dim unused = SetProperty(_plazoPagoSeleccionado, value)
            ' Sincronizar con Estado
            Estado.PlazosPago = If(value IsNot Nothing, value.plazoPago, Nothing)
            Estado.DescuentoPP = If(value IsNot Nothing, value.descuentoPP, 0D)
            cmdCrearPedido.RaiseCanExecuteChanged()
            RaisePropertyChanged(NameOf(SePuedeFinalizar))
            RaisePropertyChanged(NameOf(EsTarjetaPrepago))
            RaisePropertyChanged(NameOf(MandarCobroTarjeta))
            If Not IsNothing(_plazoPagoSeleccionado) Then
                cmdCalcularSePuedeServirPorGlovo.Execute()
            End If
        End Set
    End Property

    Private _portesGlovo As Decimal
    Public Property PortesGlovo As Decimal
        Get
            Return _portesGlovo
        End Get
        Set(value As Decimal)
            Dim unused = SetProperty(_portesGlovo, value)
        End Set
    End Property

    Private _productoPedidoSeleccionado As LineaPlantillaVenta
    Public Property productoPedidoSeleccionado As LineaPlantillaVenta
        Get
            Return _productoPedidoSeleccionado
        End Get
        Set(ByVal value As LineaPlantillaVenta)
            Dim unused = SetProperty(_productoPedidoSeleccionado, value)
        End Set
    End Property

    Private _respuestaGlovo As RespuestaAgencia
    Public Property RespuestaGlovo As RespuestaAgencia
        Get
            Return _respuestaGlovo
        End Get
        Set(value As RespuestaAgencia)
            Dim unused = SetProperty(_respuestaGlovo, value)
        End Set
    End Property

    Public ReadOnly Property SePuedeFinalizar As Boolean
        Get
            Return CanCrearPedido()
        End Get
    End Property

    Private _sePodriaServirConGlovoEnPrepago As Boolean = False
    Public Property SePodriaServirConGlovoEnPrepago As Boolean
        Get
            Return _sePodriaServirConGlovoEnPrepago
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_sePodriaServirConGlovoEnPrepago, value AndAlso Not SePuedeServirConGlovo)
        End Set
    End Property

    Private _sePuedeServirConGlovo As Boolean = False
    Public Property SePuedeServirConGlovo As Boolean
        Get
            Return _sePuedeServirConGlovo
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_sePuedeServirConGlovo, value)
            If Not _sePuedeServirConGlovo AndAlso EnviarPorGlovo Then
                EnviarPorGlovo = False
            End If
        End Set
    End Property

    Public ReadOnly Property textoFacturacionElectronica As String
        Get
            Return If(IsNothing(direccionEntregaSeleccionada),
                String.Empty,
                If(direccionEntregaSeleccionada.tieneFacturacionElectronica,
                "Este contacto tiene la facturación electrónica activada",
                If(direccionEntregaSeleccionada.tieneCorreoElectronico,
                "Este contacto tiene correo electrónico, pero NO tiene la facturación electrónica activada",
                "Recuerde pedir un correo electrónico al cliente para poder activar la facturación electrónica")))
        End Get
    End Property
    Public ReadOnly Property TienePedidosPendientes As Boolean
        Get
            Return Not IsNothing(ListaPedidosPendientes) AndAlso ListaPedidosPendientes.Where(Function(f) f <> 0).Any
        End Get
    End Property
    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            Dim unused = SetProperty(_titulo, value)
        End Set
    End Property

    Private _todosLosVendedores As Boolean
    Public Property todosLosVendedores As Boolean
        Get
            Return _todosLosVendedores
        End Get
        Set(ByVal value As Boolean)
            Dim unused = SetProperty(_todosLosVendedores, value)
        End Set
    End Property

    Public ReadOnly Property totalPedido As Decimal
        Get
            Return If(IsNothing(clienteSeleccionado),
                0,
                DirectCast(IIf(Not IsNothing(clienteSeleccionado.iva), baseImponiblePedido * 1.21D, baseImponiblePedido), Decimal))
        End Get
    End Property

    Public ReadOnly Property TotalPedidoPlazosPago As Decimal
        Get
            Return totalPedido
        End Get
    End Property

    ''' <summary>
    ''' Issue #266: Actualiza los totales en la UI sin recargar datos del servidor.
    ''' Se usa cuando el usuario modifica manualmente el precio o descuento.
    ''' </summary>
    Public Sub ActualizarTotales()
        RaisePropertyChanged(NameOf(listaProductosPedido))
        RaisePropertyChanged(NameOf(baseImponiblePedido))
        RaisePropertyChanged(NameOf(baseImponibleParaPortes))
        RaisePropertyChanged(NameOf(totalPedido))
        RaisePropertyChanged(NameOf(TotalPedidoPlazosPago))
        RaisePropertyChanged(NameOf(HayGanavisionesDisponibles))
    End Sub

    'Enum PaginasWizard
    '    SeleccionCliente
    '    SeleccionProductos
    '    SeleccionEntrega
    '    Finalizar
    'End Enum

#End Region

#Region "Comandos"

    Private _cmdAbrirPlantillaVenta As DelegateCommand(Of Object)
    Public Property cmdAbrirPlantillaVenta As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirPlantillaVenta
        End Get
        Private Set(value As DelegateCommand(Of Object))
            Dim unused = SetProperty(_cmdAbrirPlantillaVenta, value)
        End Set
    End Property
    Private Function CanAbrirPlantillaVenta(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirPlantillaVenta(arg As Object)
        regionManager.RequestNavigate("MainRegion", "PlantillaVentaView")
        'regionManager.RegisterViewWithRegion("MainRegion", GetType(PlantillaVentaView))
        'Dim region As IRegion = regionManager.Regions("MainRegion")
        'Dim vista = container.Resolve(Of PlantillaVentaView)()
        'region.Add(vista, nombreVista(region, vista.ToString))
        'region.Activate(vista)
    End Sub

    Private _cmdActualizarPrecioProducto As DelegateCommand(Of Object)
    Public Property cmdActualizarPrecioProducto As DelegateCommand(Of Object)
        Get
            Return _cmdActualizarPrecioProducto
        End Get
        Set(value As DelegateCommand(Of Object))
            Dim unused = SetProperty(_cmdActualizarPrecioProducto, value)
        End Set
    End Property
    Private Function CanActualizarPrecioProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnActualizarPrecioProducto(arg As Object)
        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            'estaOcupado = True

            Try
                Dim urlConsulta As String = "PlantillaVentas/CargarPrecio?empresa=" + clienteSeleccionado.empresa
                urlConsulta += "&cliente=" + clienteSeleccionado.cliente
                urlConsulta += "&contacto=" + clienteSeleccionado.contacto
                urlConsulta += "&productoPrecio=" + arg.producto
                urlConsulta += "&cantidad=" + arg.cantidad.ToString
                urlConsulta += "&aplicarDescuento=" + arg.aplicarDescuento.ToString
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Dim datosPrecio = JsonConvert.DeserializeObject(Of PrecioProductoDTO)(cadenaJson)
                    arg.precio = datosPrecio.precio
                    arg.descuentoProducto = datosPrecio.descuento
                    arg.aplicarDescuento = datosPrecio.aplicarDescuento
                    If arg.descuento < arg.descuentoProducto OrElse Not arg.aplicarDescuento Then
                        arg.descuento = IIf(arg.aplicarDescuento, arg.descuentoProducto, 0)
                    End If
                    RaisePropertyChanged(NameOf(baseImponiblePedido))
                Else
                    dialogService.ShowError("Se ha producido un error al cargar el precio y los descuentos especiales")
                End If

                'Carlos 04/07/18: desactivo porque lo controlamos con las ofertas permitidas
                'Await cmdComprobarCondicionesPrecio.Execute(arg)

                RaisePropertyChanged(NameOf(listaProductosPedido))
                RaisePropertyChanged(NameOf(baseImponiblePedido))
                RaisePropertyChanged(NameOf(totalPedido))
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                'estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdActualizarProductosPedido As DelegateCommand(Of LineaPlantillaVenta)
    Public Property cmdActualizarProductosPedido As DelegateCommand(Of LineaPlantillaVenta)
        Get
            Return _cmdActualizarProductosPedido
        End Get
        Private Set(value As DelegateCommand(Of LineaPlantillaVenta))
            Dim unused = SetProperty(_cmdActualizarProductosPedido, value)
        End Set
    End Property
    Private Function CanActualizarProductosPedido(arg As LineaPlantillaVenta) As Boolean
        Return True
    End Function
    Private Sub OnActualizarProductosPedido(arg As LineaPlantillaVenta)
        If IsNothing(arg) Then
            Return
        End If

        ' Issue #94: Notificar si el producto es bonificable (Ganavisiones - FASE 6)
        ' Solo notificar la primera vez que se añade (fechaInsercion = MaxValue significa que no está en el pedido)
        Dim esPrimeraVezEnPedido = arg.fechaInsercion = DateTime.MaxValue
        If esPrimeraVezEnPedido AndAlso (arg.cantidad > 0 OrElse arg.cantidadOferta > 0) Then
            If _productosBonificablesIds IsNot Nothing AndAlso _productosBonificablesIds.Contains(arg.producto?.Trim()) Then
                Dim continuar As Boolean = True
                dialogService.ShowConfirmation(
                    "Producto bonificable",
                    $"El producto '{arg.textoNombreProducto}' puede obtenerse como regalo con Ganavisiones.{Environment.NewLine}{Environment.NewLine}¿Desea añadirlo al pedido como compra normal?{Environment.NewLine}{Environment.NewLine}Pulse 'Cancelar' si prefiere seleccionarlo como regalo en la página de Ganavisiones.",
                    Sub(r)
                        continuar = r.Result = ButtonResult.OK
                    End Sub)
                If Not continuar Then
                    arg.cantidad = 0
                    arg.cantidadOferta = 0
                    Return
                End If
            End If
        End If

        cmdActualizarPrecioProducto.Execute(arg)

        If arg.cantidadVendida = 0 AndAlso arg.cantidadAbonada = 0 Then
            cmdInsertarProducto.Execute(arg)
        End If

        If (arg.cantidad + arg.cantidadOferta <> 0) AndAlso (Not arg.stockActualizado OrElse String.IsNullOrEmpty(arg.urlImagen)) Then
            cmdCargarStockProducto.Execute(arg)
        End If
        RaisePropertyChanged(NameOf(hayProductosEnElPedido))
        RaisePropertyChanged(NameOf(NoHayProductosEnElPedido))
        RaisePropertyChanged(NameOf(HayGanavisionesDisponibles))
        If Not IsNothing(ListaFiltrableProductos) AndAlso (IsNothing(ListaFiltrableProductos.ElementoSeleccionado) OrElse CType(ListaFiltrableProductos.ElementoSeleccionado, LineaPlantillaVenta).producto <> arg.producto) Then
            ListaFiltrableProductos.ElementoSeleccionado = arg
        End If

        RaisePropertyChanged(NameOf(listaProductosPedido))
        RaisePropertyChanged(NameOf(baseImponiblePedido))
        RaisePropertyChanged(NameOf(totalPedido))
    End Sub

    Private _soloConStockCommand As DelegateCommand
    Public Property SoloConStockCommand As DelegateCommand
        Get
            Return _soloConStockCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_soloConStockCommand, value)
        End Set
    End Property
    Private Function CanSoloConStock() As Boolean
        Return Not IsNothing(ListaFiltrableProductos) AndAlso Not IsNothing(ListaFiltrableProductos.Lista) AndAlso ListaFiltrableProductos.Lista.Any
    End Function
    Private Sub OnSoloConStock()
        ListaFiltrableProductos.Lista = New ObservableCollection(Of IFiltrableItem)(ListaFiltrableProductos.Lista.Where(Function(l) CType(l, LineaPlantillaVenta).cantidadDisponible > 0))
    End Sub

    Private _buscarContextualCommand As DelegateCommand(Of String)
    Public Property BuscarContextualCommand As DelegateCommand(Of String)
        Get
            Return _buscarContextualCommand
        End Get
        Private Set(value As DelegateCommand(Of String))
            Dim unused = SetProperty(_buscarContextualCommand, value)
        End Set
    End Property
    Private Function CanBuscarContextual(filtro As String) As Boolean
        Return Not IsNothing(ListaFiltrableProductos) AndAlso Not IsNothing(ListaFiltrableProductos.Filtro) AndAlso ListaFiltrableProductos.Filtro.Length >= 1
    End Function
    Private Async Sub OnBuscarContextual(filtro As String)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True
            Try
                Dim url As String = $"PlantillaVentas/Buscar?&q={filtro}&usarBusquedaConAND={EsBusquedaConAND}"
                response = Await client.GetAsync(url)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    ListaFiltrableProductos.ListaFijada = New ObservableCollection(Of IFiltrableItem)(JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaVenta))(cadenaJson))
                    Dim listaPlantilla = New ObservableCollection(Of LineaPlantillaVenta)()
                    For Each item In ListaFiltrableProductos.ListaFijada
                        listaPlantilla.Add(item)
                    Next
                    ListaFiltrableProductos.ListaFijada = New ObservableCollection(Of IFiltrableItem)(Await servicio.PonerStocks(listaPlantilla, almacenSeleccionado.Codigo, Constantes.Almacenes.ALMACENES_STOCK.ToList()))
                    Dim productoOriginal As LineaPlantillaVenta
                    Dim producto As LineaPlantillaVenta
                    For i = 0 To ListaFiltrableProductos.ListaFijada.Count - 1
                        producto = ListaFiltrableProductos.ListaFijada(i)
                        ' Calcular StockDisponibleTodosLosAlmacenes si no viene del API
                        If producto.StockDisponibleTodosLosAlmacenes = 0 AndAlso producto.stocks IsNot Nothing AndAlso producto.stocks.Count > 0 Then
                            producto.StockDisponibleTodosLosAlmacenes = producto.stocks.Sum(Function(s) s.cantidadDisponible)
                        End If
                        producto.stockActualizado = True
                        If clienteSeleccionado.cliente = Constantes.Clientes.Especiales.EL_EDEN OrElse clienteSeleccionado.estado = Constantes.Clientes.ESTADO_DISTRIBUIDOR Then
                            producto.aplicarDescuento = True
                            producto.aplicarDescuentoFicha = True
                        End If
                        productoOriginal = ListaFiltrableProductos.ListaOriginal.Where(Function(p) CType(p, LineaPlantillaVenta).producto = producto.producto).FirstOrDefault
                        If Not IsNothing(productoOriginal) Then
                            If productoOriginal.StockDisponibleTodosLosAlmacenes = 0 AndAlso productoOriginal.stocks IsNot Nothing AndAlso productoOriginal.stocks.Count > 0 Then
                                productoOriginal.StockDisponibleTodosLosAlmacenes = productoOriginal.stocks.Sum(Function(s) s.cantidadDisponible)
                            End If
                            productoOriginal.stockActualizado = True
                            ListaFiltrableProductos.ListaFijada(i) = productoOriginal
                        End If
                    Next
                    estaOcupado = False
                Else
                    dialogService.ShowError("Se ha producido un error al cargar los productos")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using
    End Sub

    Private _cmdBuscarEnTodosLosProductos As DelegateCommand(Of String)
    Public Property cmdBuscarEnTodosLosProductos As DelegateCommand(Of String)
        Get
            Return _cmdBuscarEnTodosLosProductos
        End Get
        Private Set(value As DelegateCommand(Of String))
            Dim unused = SetProperty(_cmdBuscarEnTodosLosProductos, value)
        End Set
    End Property
    Private Function CanBuscarEnTodosLosProductos(filtro As String) As Boolean
        Return Not IsNothing(ListaFiltrableProductos) AndAlso Not IsNothing(ListaFiltrableProductos.Filtro) AndAlso ListaFiltrableProductos.Filtro.Length >= 3
    End Function
    Private Async Sub OnBuscarEnTodosLosProductos(filtro As String)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                Dim url As String = "PlantillaVentas/BuscarProducto?empresa=" + clienteSeleccionado.empresa + "&filtroProducto=" + filtro
                response = Await client.GetAsync(url)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    ListaFiltrableProductos.ListaFijada = New ObservableCollection(Of IFiltrableItem)(JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaVenta))(cadenaJson))
                    Dim listaPlantilla = New ObservableCollection(Of LineaPlantillaVenta)()
                    For Each item In ListaFiltrableProductos.ListaFijada
                        listaPlantilla.Add(item)
                    Next
                    ListaFiltrableProductos.ListaFijada = New ObservableCollection(Of IFiltrableItem)(Await servicio.PonerStocks(listaPlantilla, almacenSeleccionado.Codigo, Constantes.Almacenes.ALMACENES_STOCK.ToList()))
                    Dim productoOriginal As LineaPlantillaVenta
                    Dim producto As LineaPlantillaVenta
                    For i = 0 To ListaFiltrableProductos.ListaFijada.Count - 1
                        producto = ListaFiltrableProductos.ListaFijada(i)
                        ' Calcular StockDisponibleTodosLosAlmacenes si no viene del API
                        If producto.StockDisponibleTodosLosAlmacenes = 0 AndAlso producto.stocks IsNot Nothing AndAlso producto.stocks.Count > 0 Then
                            producto.StockDisponibleTodosLosAlmacenes = producto.stocks.Sum(Function(s) s.cantidadDisponible)
                        End If
                        producto.stockActualizado = True
                        If clienteSeleccionado.cliente = Constantes.Clientes.Especiales.EL_EDEN OrElse clienteSeleccionado.estado = Constantes.Clientes.ESTADO_DISTRIBUIDOR Then
                            producto.aplicarDescuento = True
                            producto.aplicarDescuentoFicha = True
                        End If
                        productoOriginal = ListaFiltrableProductos.ListaOriginal.Where(Function(p) CType(p, LineaPlantillaVenta).producto = producto.producto).FirstOrDefault
                        If Not IsNothing(productoOriginal) Then
                            If productoOriginal.StockDisponibleTodosLosAlmacenes = 0 AndAlso productoOriginal.stocks IsNot Nothing AndAlso productoOriginal.stocks.Count > 0 Then
                                productoOriginal.StockDisponibleTodosLosAlmacenes = productoOriginal.stocks.Sum(Function(s) s.cantidadDisponible)
                            End If
                            productoOriginal.stockActualizado = True
                            ListaFiltrableProductos.ListaFijada(i) = productoOriginal
                        End If
                    Next
                    estaOcupado = False
                Else
                    dialogService.ShowError("Se ha producido un error al cargar los productos")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using
    End Sub


    Private _cambiarIvaCommand As DelegateCommand
    Public Property CambiarIvaCommand As DelegateCommand
        Get
            Return _cambiarIvaCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cambiarIvaCommand, value)
        End Set
    End Property
    Private Sub OnCambiarIva()
        clienteSeleccionado.iva = IIf(Not String.IsNullOrWhiteSpace(clienteSeleccionado.iva), Nothing, iva)
        RaisePropertyChanged(NameOf(clienteSeleccionado))
        RaisePropertyChanged(NameOf(totalPedido))
        RaisePropertyChanged(NameOf(SePuedeFinalizar))
    End Sub

    Private _cmdCargarClientesVendedor As DelegateCommand
    Public Property cmdCargarClientesVendedor As DelegateCommand
        Get
            Return _cmdCargarClientesVendedor
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdCargarClientesVendedor, value)
        End Set
    End Property
    Private Function CanCargarClientesVendedor() As Boolean
        Return filtroCliente.Length = 0 OrElse IsNothing(listaClientes) OrElse listaClientes.Count = 0
    End Function
    Private Async Sub OnCargarClientesVendedor()

        ' No se puede filtrar por menos de 4 caracteres
        If IsNothing(filtroCliente) OrElse (filtroCliente.Length < 4 AndAlso Not IsNumeric(filtroCliente)) Then
            listaClientes = Nothing
            Return
        End If

        Try
            estaOcupado = True
            vendedor = Await leerParametro("Vendedor")
            Dim parametroClientesTodosVendedores As String = Await leerParametro("PermitirVerClientesTodosLosVendedores")
            todosLosVendedores = IIf(parametroClientesTodosVendedores.Trim = "1", True, False)
            listaClientesOriginal = Await servicio.CargarClientesVendedor(filtroCliente, vendedor, todosLosVendedores)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaOcupado = False
        End Try

        fechaMinimaEntrega = Await ObtenerFechaMinimaEntregaAsync()
    End Sub

    Private _cargarCorreoYMovilTarjeta As DelegateCommand
    Public Property CargarCorreoYMovilTarjeta As DelegateCommand
        Get
            Return _cargarCorreoYMovilTarjeta
        End Get
        Set(value As DelegateCommand)
            Dim unused = SetProperty(_cargarCorreoYMovilTarjeta, value)
        End Set
    End Property
    Private Async Sub OnCargarCorreoYMovilTarjeta()
        Dim telefono As New Telefono(clienteSeleccionado.telefono)
        CobroTarjetaMovil = telefono.MovilUnico
        Dim cliente = Await servicio.CargarCliente(clienteSeleccionado.empresa, clienteSeleccionado.cliente, direccionEntregaSeleccionada.contacto)
        Dim personasContacto = New List(Of PersonasContactoCliente)
        For Each persona In cliente.PersonasContacto
            personasContacto.Add(New PersonasContactoCliente With {
                .Cargo = IIf(persona.FacturacionElectronica, 22, 14),
                .CorreoElectrónico = persona.CorreoElectronico
                                 })
        Next
        Dim correo As New CorreoCliente(personasContacto)
        CobroTarjetaCorreo = correo.CorreoUnicoFacturaElectronica
        If String.IsNullOrEmpty(CobroTarjetaCorreo) Then
            CobroTarjetaCorreo = correo.CorreoAgencia
        End If
    End Sub

    Private _cmdCargarFormasVenta As DelegateCommand(Of Object)
    Public Property cmdCargarFormasVenta As DelegateCommand(Of Object)
        Get
            Return _cmdCargarFormasVenta
        End Get
        Private Set(value As DelegateCommand(Of Object))
            Dim unused = SetProperty(_cmdCargarFormasVenta, value)
        End Set
    End Property
    Private Function CanCargarFormasVenta(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarFormasVenta(arg As Object)
        If IsNothing(clienteSeleccionado) Then
            Return
        End If
        If almacenSeleccionado.Codigo = Constantes.Almacenes.ALMACEN_CENTRAL AndAlso clienteSeleccionado?.cliente <> Constantes.Clientes.Especiales.EL_EDEN Then
            fechaMinimaEntrega = Await ObtenerFechaMinimaEntregaAsync()
        End If


        RaisePropertyChanged(NameOf(TotalPedidoPlazosPago))
        RaisePropertyChanged(NameOf(SePuedeFinalizar))

        ' Issue #286: Guardar valores del borrador antes de la lógica automática
        ' Solo aplicar si hay borrador pendiente Y no se han restaurado ya los valores
        Dim hayBorradorPendiente As Boolean = _borradorEnRestauracion IsNot Nothing AndAlso Not _borradorRestauradoEnFormasVenta
        Dim formaVentaBorrador As Integer = 0
        Dim formaVentaOtrasCodigoBorrador As String = Nothing
        Dim fechaEntregaBorrador As Date = Date.MinValue
        Dim contactoBorrador As String = Nothing
        Dim formaPagoBorrador As String = Nothing
        Dim plazosPagoBorrador As String = Nothing
        Dim mantenerJuntoBorrador As Boolean = False
        Dim servirJuntoBorrador As Boolean = False

        If hayBorradorPendiente Then
            formaVentaBorrador = _borradorEnRestauracion.FormaVenta
            formaVentaOtrasCodigoBorrador = _borradorEnRestauracion.FormaVentaOtrasCodigo
            fechaEntregaBorrador = _borradorEnRestauracion.FechaEntrega
            contactoBorrador = _borradorEnRestauracion.Contacto
            formaPagoBorrador = _borradorEnRestauracion.FormaPago
            plazosPagoBorrador = _borradorEnRestauracion.PlazosPago
            mantenerJuntoBorrador = _borradorEnRestauracion.MantenerJunto
            servirJuntoBorrador = _borradorEnRestauracion.ServirJunto
        End If

        Dim esApoyoComercial As Boolean
        vendedorUsuario = Await leerParametro("Vendedor")
        Dim vendedoresEquipo = Await servicio.CargarVendedoresEquipo(vendedorUsuario)

        ' Issue #286: Solo aplicar lógica automática si NO hay borrador pendiente
        If Not hayBorradorPendiente Then
            If vendedorUsuario = clienteSeleccionado.vendedor Then
                formaVentaSeleccionada = 1 ' Directa
            ElseIf Not IsNothing(vendedoresEquipo) AndAlso Not IsNothing(vendedoresEquipo.SingleOrDefault(Function(v) v.Vendedor = clienteSeleccionado.vendedor)) Then
                formaVentaSeleccionada = 3 ' Otros
                esApoyoComercial = True
            Else
                formaVentaSeleccionada = 2 ' Telefono
            End If
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                response = Await client.GetAsync("FormasVenta?empresa=" + clienteSeleccionado.empresa)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaFormasVenta = JsonConvert.DeserializeObject(Of ObservableCollection(Of FormaVentaDTO))(cadenaJson)

                    ' Issue #286: Restaurar forma de venta del borrador ahora que listaFormasVenta está cargada
                    If hayBorradorPendiente AndAlso formaVentaBorrador > 0 Then
                        formaVentaSeleccionada = formaVentaBorrador
                        If formaVentaBorrador > 2 AndAlso Not String.IsNullOrEmpty(formaVentaOtrasCodigoBorrador) Then
                            Dim formaOtra = listaFormasVenta?.FirstOrDefault(Function(f) f.numero?.Trim() = formaVentaOtrasCodigoBorrador?.Trim())
                            If formaOtra IsNot Nothing Then
                                formaVentaOtrasSeleccionada = formaOtra
                            End If
                        End If
                    ElseIf esApoyoComercial Then
                        formaVentaOtrasSeleccionada = listaFormasVenta.Single(Function(f) f.numero = Constantes.FormasVenta.APOYO_COMERCIAL)
                    End If
                Else
                    dialogService.ShowError("Se ha producido un error al cargar las formas de venta")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using

        ' Issue #286: Restaurar valores del borrador después de cargar los selectores
        ' Esperamos a que los selectores se carguen antes de asignar valores
        If hayBorradorPendiente Then
            Await Task.Delay(200)

            ' Restaurar contacto
            ' Issue #286: El SelectorDireccionEntrega no actualiza la selección si ya hay una dirección seleccionada.
            ' Necesitamos limpiar direccionEntregaSeleccionada primero para que el control vuelva a buscar.
            If Not String.IsNullOrEmpty(contactoBorrador) Then
                ' Primero establecer el contacto deseado
                ContactoSeleccionado = contactoBorrador
                Estado.Contacto = contactoBorrador

                ' Luego forzar al control a re-seleccionar limpiando la dirección actual
                ' Esto hará que el control use Seleccionada para encontrar la dirección correcta
                If direccionEntregaSeleccionada IsNot Nothing AndAlso direccionEntregaSeleccionada.contacto <> contactoBorrador Then
                    ' Limpiamos para forzar recarga
                    _direccionEntregaSeleccionada = Nothing
                    RaisePropertyChanged(NameOf(direccionEntregaSeleccionada))

                    ' Esperamos a que el control recargue
                    Await Task.Delay(100)

                    ' Si aún no se seleccionó la correcta, forzamos
                    If direccionEntregaSeleccionada Is Nothing OrElse direccionEntregaSeleccionada.contacto <> contactoBorrador Then
                        ContactoSeleccionado = contactoBorrador
                        RaisePropertyChanged(NameOf(ContactoSeleccionado))
                    End If
                End If
            End If

            ' Restaurar forma de pago y plazos
            If Not String.IsNullOrEmpty(formaPagoBorrador) Then
                FormaPagoCliente = formaPagoBorrador
            End If
            If Not String.IsNullOrEmpty(plazosPagoBorrador) Then
                PlazoPagoCliente = plazosPagoBorrador
            End If

            ' Restaurar fecha de entrega DESPUÉS de que fechaMinimaEntrega se haya calculado
            If fechaEntregaBorrador > Date.MinValue Then
                If fechaEntregaBorrador < fechaMinimaEntrega Then
                    fechaEntregaBorrador = fechaMinimaEntrega
                End If
                Estado.FechaEntrega = fechaEntregaBorrador
                RaisePropertyChanged(NameOf(fechaEntrega))
            End If

            ' Restaurar MantenerJunto y ServirJunto
            If direccionEntregaSeleccionada IsNot Nothing Then
                If mantenerJuntoBorrador Then
                    direccionEntregaSeleccionada.mantenerJunto = True
                End If
                If servirJuntoBorrador Then
                    direccionEntregaSeleccionada.servirJunto = True
                End If
                RaisePropertyChanged(NameOf(direccionEntregaSeleccionada))
            End If

            ' Restaurar ComentarioPicking
            If _borradorEnRestauracion IsNot Nothing AndAlso
               Not String.IsNullOrEmpty(_borradorEnRestauracion.ComentarioPicking) AndAlso
               clienteSeleccionado IsNot Nothing Then
                clienteSeleccionado.comentarioPicking = _borradorEnRestauracion.ComentarioPicking
                RaisePropertyChanged(NameOf(clienteSeleccionado))
            End If

            ' Issue #286: Marcar que ya restauramos los valores de esta página
            ' Evita aplicar los valores múltiples veces si el usuario navega varias veces
            _borradorRestauradoEnFormasVenta = True

            ' NO resetear _borradorEnRestauracion aquí
            ' Se mantiene para que CargarProductosBonificablesAsync pueda preservar los regalos
            ' Se reseteará en OnCrearPedido cuando se cree el pedido exitosamente
        End If

    End Sub

    Private _cargarProductoCommand As DelegateCommand(Of Object)
    Public Property CargarProductoCommand As DelegateCommand(Of Object)
        Get
            Return _cargarProductoCommand
        End Get
        Private Set(value As DelegateCommand(Of Object))
            Dim unused = SetProperty(_cargarProductoCommand, value)
        End Set
    End Property
    Private Function CanCargarProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnCargarProducto(arg As Object)
        If IsNothing(productoPedidoSeleccionado) Then
            Return
        End If

        Dim parameters As New NavigationParameters From {
            {"numeroProductoParameter", productoPedidoSeleccionado.producto}
        }
        regionManager.RequestNavigate("MainRegion", "ProductoView", parameters)
    End Sub

    Private _cmdCargarProductosPlantilla As DelegateCommand
    Public Property cmdCargarProductosPlantilla As DelegateCommand
        Get
            Return _cmdCargarProductosPlantilla
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdCargarProductosPlantilla, value)
        End Set
    End Property
    Private Async Sub OnCargarProductosPlantilla()
        Await CargarProductosPlantillaAsync()
    End Sub

    ''' <summary>
    ''' Carga los productos de la plantilla del cliente de forma awaitable.
    ''' Issue #286: Separado para poder usar desde OnCargarBorrador.
    ''' </summary>
    Private Async Function CargarProductosPlantillaAsync() As Task
        Try
            estaOcupado = True
            ListaFiltrableProductos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(Await servicio.CargarProductosPlantilla(clienteSeleccionado))
            Dim listaPlantilla = New ObservableCollection(Of LineaPlantillaVenta)
            For Each item In ListaFiltrableProductos.ListaOriginal
                listaPlantilla.Add(item)
            Next
            ListaFiltrableProductos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(Await servicio.PonerStocks(listaPlantilla, almacenSeleccionado.Codigo, Constantes.Almacenes.ALMACENES_STOCK.ToList()))
            ' Issue #286: Marcar stockActualizado=True y calcular StockDisponibleTodosLosAlmacenes si no viene del API
            For Each item In ListaFiltrableProductos.ListaOriginal
                Dim linea = DirectCast(item, LineaPlantillaVenta)
                ' Si el API no devolvió StockDisponibleTodosLosAlmacenes, calcularlo de la lista de stocks
                If linea.StockDisponibleTodosLosAlmacenes = 0 AndAlso linea.stocks IsNot Nothing AndAlso linea.stocks.Count > 0 Then
                    linea.StockDisponibleTodosLosAlmacenes = linea.stocks.Sum(Function(s) s.cantidadDisponible)
                End If
                linea.stockActualizado = True
            Next
            estaOcupado = False
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaOcupado = False
        End Try
    End Function

    Private _cmdCargarStockProducto As DelegateCommand(Of LineaPlantillaVenta)
    Public Property cmdCargarStockProducto As DelegateCommand(Of LineaPlantillaVenta)
        Get
            Return _cmdCargarStockProducto
        End Get
        Private Set(value As DelegateCommand(Of LineaPlantillaVenta))
            Dim unused = SetProperty(_cmdCargarStockProducto, value)
        End Set
    End Property

    Private Async Sub OnCargarStockProducto(linea As LineaPlantillaVenta)

        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            'estaOcupado = True

            Dim datosStock As StockProductoDTO

            Try
                response = Await client.GetAsync("PlantillaVentas/CargarStocks?empresa=" + clienteSeleccionado.empresa + "&almacen=" + almacenSeleccionado.Codigo + "&productoStock=" + linea.producto)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    datosStock = JsonConvert.DeserializeObject(Of StockProductoDTO)(cadenaJson)
                    linea.stock = datosStock.stock
                    linea.cantidadDisponible = datosStock.cantidadDisponible
                    linea.cantidadPendienteRecibir = datosStock.cantidadPendienteRecibir
                    linea.StockDisponibleTodosLosAlmacenes = datosStock.StockDisponibleTodosLosAlmacenes
                    linea.urlImagen = datosStock.urlImagen
                    linea.stockActualizado = True
                    linea.fechaInsercion = Now
                    RaisePropertyChanged(NameOf(listaProductosPedido))
                    RaisePropertyChanged(NameOf(baseImponiblePedido))
                Else
                    dialogService.ShowError("Se ha producido un error al cargar el stock del producto")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                'estaOcupado = False
            End Try

        End Using
    End Sub

    Private _cmdCargarUltimasVentas As DelegateCommand(Of Object)
    Public Property cmdCargarUltimasVentas As DelegateCommand(Of Object)
        Get
            Return _cmdCargarUltimasVentas
        End Get
        Private Set(value As DelegateCommand(Of Object))
            Dim unused = SetProperty(_cmdCargarUltimasVentas, value)
        End Set
    End Property
    Private Function CanCargarUltimasVentas(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarUltimasVentas(arg As Object)

        If IsNothing(clienteSeleccionado) OrElse IsNothing(arg) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            'estaOcupado = True

            Try
                response = Await client.GetAsync("PlantillaVentas/UltimasVentasProductoCliente?empresa=" + clienteSeleccionado.empresa + "&clienteUltimasVentas=" + clienteSeleccionado.cliente + "&productoUltimasVentas=" + arg.producto)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaUltimasVentas = JsonConvert.DeserializeObject(Of ObservableCollection(Of UltimasVentasProductoClienteDTO))(cadenaJson)

                Else
                    dialogService.ShowError("Se ha producido un error al cargar las últimas ventas del producto")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                'estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdComprobarPendientes As DelegateCommand
    Public Property cmdComprobarPendientes As DelegateCommand
        Get
            Return _cmdComprobarPendientes
        End Get
        Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdComprobarPendientes, value)
        End Set
    End Property
    Private Async Sub OnComprobarPendientes()
        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Try
            ListaPedidosPendientes = Await servicio.CargarListaPendientes(clienteSeleccionado.empresa, clienteSeleccionado.cliente)

            If TienePedidosPendientes Then
                Dim textoMensaje As String = "Este cliente tiene otros pedidos pendientes." + vbCr + vbCr
                For Each i In ListaPedidosPendientes.Where(Function(f) f <> 0)
                    textoMensaje += i.ToString + vbTab
                Next
                textoMensaje += vbCr + vbCr + "Por favor, revise que sea todo correcto."
                dialogService.ShowNotification("Pendientes", textoMensaje)
            End If
        Catch ex As Exception
            dialogService.ShowError("Se ha producido un error al comprobar los pedidos pendientes del cliente" + vbCrLf + ex.Message)
        End Try
    End Sub

    Private _cmdCrearPedido As DelegateCommand
    Public Property cmdCrearPedido As DelegateCommand
        Get
            Return _cmdCrearPedido
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdCrearPedido, value)
        End Set
    End Property
    Private Function CanCrearPedido() As Boolean
        Return Not IsNothing(FormaPagoSeleccionada) AndAlso Not IsNothing(plazoPagoSeleccionado) AndAlso
            (Not String.IsNullOrEmpty(clienteSeleccionado.cifNif) OrElse String.IsNullOrEmpty(clienteSeleccionado.iva) OrElse EsPresupuesto)
    End Function
    Private Async Sub OnCrearPedido()

        If IsNothing(FormaPagoSeleccionada) OrElse IsNothing(plazoPagoSeleccionado) OrElse IsNothing(clienteSeleccionado) Then
            dialogService.ShowError("Compruebe que tiene seleccionados plazos y forma de pago, por favor.")
            Return
        End If

        delegacionUsuario = Await leerParametro(Parametros.Claves.DelegacionDefecto)

        If formaVentaSeleccionada = 1 Then
            formaVentaPedido = "DIR"
        ElseIf formaVentaSeleccionada = 2 Then
            formaVentaPedido = "TEL"
        Else
            If IsNothing(formaVentaOtrasSeleccionada) Then
                Return
            End If
            formaVentaPedido = formaVentaOtrasSeleccionada.numero
        End If

        If IsNothing(plazoPagoSeleccionado) AndAlso (IsNothing(direccionEntregaSeleccionada) OrElse IsNothing(direccionEntregaSeleccionada.plazosPago)) Then
            dialogService.ShowError("Este contacto no tiene plazos de pago asignados")
            Return
        End If


        estaOcupado = True
        Dim pedido As PedidoVentaDTO = PrepararPedido()

        Try

            Dim numPedido As String

            If PedidoPendienteSeleccionado = 0 Then
                Dim crearEx As Exception = Nothing
                Try
                    numPedido = Await servicio.CrearPedido(pedido)
                Catch ex As ValidationException
                    crearEx = ex
                    ' Carlos 12/01/25: Verificar si puede crear sin pasar validación
                    ' - Dirección o Almacén pueden crear sin importar almacenes
                    ' - Tiendas puede crear solo si TODAS las líneas están en su almacén
                    Dim puedeCrearSinPasarValidacion As Boolean =
                    configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
                    configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) OrElse
                    (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS) AndAlso
                     pedido.Lineas.All(Function(l) l.almacen = almacenRutaUsuario))
                    If Not puedeCrearSinPasarValidacion Then
                        Throw crearEx
                    End If
                End Try

                If crearEx IsNot Nothing Then
                    ' Ahora sí estamos fuera del Catch y podemos Await el diálogo
                    Dim mensaje As String = crearEx.Message & vbCrLf & "¿Desea crearlo de todos modos?"
                    Dim confirmar As Boolean = Await dialogService.ShowConfirmationAsync("Pedido no válido", mensaje)

                    If confirmar Then
                        pedido.CreadoSinPasarValidacion = True
                        ' Reintento (si vuelve a fallar, la excepción se propagará y será capturada por el Catch exterior)
                        numPedido = Await servicio.CrearPedido(pedido)
                    Else
                        ' El usuario no quiere forzar el pedido: relanzamos la excepción original para que la maneje el Catch exterior
                        Throw crearEx
                    End If
                End If
            Else
                Dim pedidoUnido As PedidoVentaDTO = Await servicio.UnirPedidos(clienteSeleccionado.empresa, PedidoPendienteSeleccionado, pedido)
                numPedido = pedidoUnido.numero.ToString
            End If

            If MandarCobroTarjeta Then
                Dim pedidoCreado As PedidoVentaDTO = Await servicioPedidosVenta.cargarPedido(pedido.empresa, numPedido)
                servicio.EnviarCobroTarjeta(CobroTarjetaCorreo, CobroTarjetaMovil, pedidoCreado.Total, numPedido, clienteSeleccionado.cliente)
            End If

            ' Issue #286: Limpiar borrador en restauración ya que el pedido se creó exitosamente
            _borradorEnRestauracion = Nothing
            _borradorRestauradoEnFormasVenta = False

            ' Cerramos la ventana
            Dim view = regionManager.Regions("MainRegion").ActiveViews.FirstOrDefault
            If Not IsNothing(view) Then
                regionManager.Regions("MainRegion").Deactivate(view)
                regionManager.Regions("MainRegion").Remove(view)
            End If

            ' Abrimos el pedido
            PedidoVentaViewModel.CargarPedido(clienteSeleccionado.empresa, numPedido, container)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)

            ' Issue #286: Guardar borrador automáticamente en caso de error
            ' Esto permite recuperar el pedido si hay un error de red o del servidor
            GuardarBorradorAutomatico(ex.Message)
        Finally
            estaOcupado = False
        End Try



    End Sub

    Private _noAmpliarPedidoCommand As DelegateCommand
    Public Property NoAmpliarPedidoCommand As DelegateCommand
        Get
            Return _noAmpliarPedidoCommand
        End Get
        Set(value As DelegateCommand)
            Dim unused = SetProperty(_noAmpliarPedidoCommand, value)
        End Set
    End Property
    Private Function CanNoAmpliarPedido() As Boolean
        Return PedidoPendienteSeleccionado <> 0
    End Function
    Private Sub OnNoAmpliarPedido()
        PedidoPendienteSeleccionado = 0
    End Sub

    Private _copiarClientePortapapelesCommand As DelegateCommand
    Public Property CopiarClientePortapapelesCommand As DelegateCommand
        Get
            Return _copiarClientePortapapelesCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_copiarClientePortapapelesCommand, value)
        End Set
    End Property
    Private Sub OnCopiarClientePortapapeles()
        Dim html As New StringBuilder()
        Dim unused2 = html.Append(Constantes.Formatos.HTML_CLIENTE_P_TAG)
        Dim unused1 = html.Append(clienteSeleccionado.ToString.Replace(vbCr, "<br/>"))
        Dim unused = html.Append("</p>")
        ClipboardHelper.CopyToClipboard(html.ToString, clienteSeleccionado.ToString)
        dialogService.ShowNotification("Datos del cliente copiados al portapapeles")
    End Sub

    Private _mostrarGanavisionesCommand As DelegateCommand
    Public Property MostrarGanavisionesCommand As DelegateCommand
        Get
            Return _mostrarGanavisionesCommand
        End Get
        Set(value As DelegateCommand)
            Dim unused = SetProperty(_mostrarGanavisionesCommand, value)
        End Set
    End Property
    Private Function CanMostrarGanavisiones() As Boolean
        Return True
    End Function
    Private Sub OnMostrarGanavisiones()
        If IsNothing(ListaProductosGanavisiones) Then
            Try
                Dim lista = servicio.CargarProductosBonificables(clienteSeleccionado.cliente, listaProductosPedido.ToList)
                ListaProductosGanavisiones = New ObservableCollection(Of IFiltrableItem)(lista)
            Catch ex As Exception
                dialogService.ShowError("No se han podido cargar los Ganavisiones")
            End Try
        End If
    End Sub



    ''' <summary>
    ''' Issue #287: Refactorizado para usar Estado.ToPedidoVentaDTO().
    ''' Sincroniza las listas al Estado, llama a ToPedidoVentaDTO, y añade campos de contexto.
    ''' </summary>
    Private Function PrepararPedido() As PedidoVentaDTO
        almacenRutaUsuario = almacenSeleccionado.Codigo

        ' Sincronizar listas de productos y regalos al Estado
        SincronizarListasAlEstado()

        ' Usar Estado.ToPedidoVentaDTO() para crear el pedido base
        Dim pedido = Estado.ToPedidoVentaDTO(
            formaVentaPedido,
            AddressOf cogerSiguienteOferta,
            AddressOf CalcularSerie
        )

        ' Añadir campos de contexto que no están en Estado
        pedido.Usuario = configuracion.usuario

        ' Manejar ccc condicionalmente según forma de pago
        If IsNothing(FormaPagoSeleccionada) OrElse Not FormaPagoSeleccionada.cccObligatorio Then
            pedido.ccc = Nothing
        End If

        ' Añadir Usuario y delegacion a cada línea
        For Each linea In pedido.Lineas
            linea.Usuario = configuracion.usuario
            linea.delegacion = delegacionUsuario
        Next

        Return pedido
    End Function

    ''' <summary>
    ''' Sincroniza las listas de productos y regalos del ViewModel al Estado.
    ''' Necesario antes de llamar a Estado.ToPedidoVentaDTO() o guardar borrador.
    ''' </summary>
    Private Sub SincronizarListasAlEstado()
        ' Sincronizar productos
        Estado.LineasProducto = New List(Of LineaPlantillaVenta)
        If listaProductosPedido IsNot Nothing Then
            For Each linea In listaProductosPedido.Where(Function(p) p.cantidad <> 0 OrElse p.cantidadOferta <> 0)
                Estado.LineasProducto.Add(linea)
            Next
        End If

        ' Sincronizar regalos (Ganavisiones)
        Estado.LineasRegalo = New List(Of LineaRegalo)
        If ListaProductosBonificables IsNot Nothing Then
            For Each regalo In ListaProductosBonificables.Where(Function(r) r.cantidad > 0)
                Estado.LineasRegalo.Add(regalo)
            Next
        End If
    End Sub

    Private Function CalcularSerie() As String
        Dim estadosValidos = {Constantes.Clientes.ESTADO_DISTRIBUIDOR, Constantes.Clientes.ESTADO_DISTRIBUIDOR_NO_VISITABLE}
        Return If(estadosValidos.Contains(clienteSeleccionado.estado) AndAlso listaProductosPedido.Where(Function(l) l.precio <> 0 AndAlso l.descuento <> 1 AndAlso l.descuentoProducto <> 1).All(Function(l) l.familia = Constantes.Familias.UNION_LASER_NOMBRE),
            Constantes.Series.UNION_LASER,
            If(estadosValidos.Contains(clienteSeleccionado.estado) AndAlso listaProductosPedido.Where(Function(l) l.precio <> 0 AndAlso l.descuento <> 1 AndAlso l.descuentoProducto <> 1).All(Function(l) l.familia = Constantes.Familias.EVA_VISNU_NOMBRE),
            Constantes.Series.EVA_VISNU,
            Constantes.Series.SERIE_DEFECTO))
    End Function


    Private _cmdInsertarProducto As DelegateCommand(Of Object)
    Public Property cmdInsertarProducto As DelegateCommand(Of Object)
        Get
            Return _cmdInsertarProducto
        End Get
        Private Set(value As DelegateCommand(Of Object))
            Dim unused = SetProperty(_cmdInsertarProducto, value)
        End Set
    End Property
    Private Function CanInsertarProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnInsertarProducto(arg As Object)
        ' Solo insertamos si es un producto que no está en listaProductosOrigina
        If IsNothing(arg) OrElse Not IsNothing(ListaFiltrableProductos.ListaOriginal.Where(Function(p) CType(p, LineaPlantillaVenta).producto = arg.producto).FirstOrDefault) Then
            Return
        End If
        'arg.cantidadVendida = arg.cantidad + arg.cantidadOferta
        ListaFiltrableProductos.ListaOriginal.Add(arg)
        RaisePropertyChanged(NameOf(baseImponiblePedido))
    End Sub

    Private _cmdCalcularSePuedeServirPorGlovo As DelegateCommand

    Public Property cmdCalcularSePuedeServirPorGlovo As DelegateCommand
        Get
            Return _cmdCalcularSePuedeServirPorGlovo
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdCalcularSePuedeServirPorGlovo, value)
        End Set
    End Property
    Private Async Sub OnCalcularSePuedeServirPorGlovo()
        If PaginaActual.Name <> PAGINA_FINALIZAR Then
            Return
        End If
        Using client As New HttpClient
            estaOcupado = True

            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim pedido As PedidoVentaDTO = PrepararPedido()

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")

            Try
                response = Await client.PostAsync("PedidosVenta/SePuedeServirPorAgencia", content)


                If response.IsSuccessStatusCode Then
                    Dim respuestaString As String = Await response.Content.ReadAsStringAsync()
                    RespuestaGlovo = JsonConvert.DeserializeObject(Of RespuestaAgencia)(respuestaString)
                    If Not IsNothing(RespuestaGlovo) Then
                        SePuedeServirConGlovo = RespuestaGlovo.CondicionesPagoValidas
                        SePodriaServirConGlovoEnPrepago = True
                        DireccionGoogleMaps = RespuestaGlovo.DireccionFormateada
                        PortesGlovo = RespuestaGlovo.Coste
                        AlmacenEntregaUrgente = RespuestaGlovo.Almacen
                    Else
                        SePuedeServirConGlovo = False
                        SePodriaServirConGlovoEnPrepago = False
                        DireccionGoogleMaps = String.Empty
                        PortesGlovo = 0
                        AlmacenEntregaUrgente = String.Empty
                    End If
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    ' Carlos 21/11/24: Usar HttpErrorHelper para parsear errores del API
                    Dim contenido As String = HttpErrorHelper.ParsearErrorHttp(detallesError)
                    dialogService.ShowError(contenido)
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using
    End Sub

#End Region

#Region "Funciones Auxiliares"

    Private Function cogerSiguienteOferta() As Integer
        ultimaOferta += 1
        Return ultimaOferta
    End Function

    Private Async Function leerParametro(v As String) As Task(Of String)
        Dim empresa As String = "1"
        If Not IsNothing(clienteSeleccionado) Then
            empresa = clienteSeleccionado.empresa
        End If

        Return Await configuracion.leerParametro(empresa, v)
    End Function

    Private Function nombreVista(region As Region, nombre As String) As String
        Dim contador As Integer = 2
        Dim repetir As Boolean = True
        Dim nombreAmpliado As String = nombre
        While repetir
            repetir = False
            For Each view In region.Views
                If Not IsNothing(region.GetView(nombreAmpliado)) Then
                    nombreAmpliado = nombre + contador.ToString
                    contador += 1
                    repetir = True
                    Exit For
                End If
            Next
        End While
        Return nombreAmpliado
    End Function
    Private Sub SeleccionarElCliente(value As ClienteJson)
        Dim unused = SetProperty(_clienteSeleccionado, value)
        RaisePropertyChanged(NameOf(hayUnClienteSeleccionado))
        ' Sincronizar datos del cliente con Estado
        Estado.Empresa = value.empresa
        Estado.Cliente = value.cliente
        Estado.Contacto = value.contacto
        Estado.NombreCliente = value.nombre
        Estado.IvaCliente = value.iva
        Estado.EstadoCliente = value.estado
        Estado.ComentarioPickingCliente = value.comentarioPicking
        Estado.ComentarioPicking = value.comentarioPicking
        Titulo = String.Format("Plantilla Ventas ({0})", value.cliente)
        cmdCargarProductosPlantilla.Execute()
        cmdComprobarPendientes.Execute()
        iva = clienteSeleccionado.iva
        PaginaActual = PaginasWizard.Where(Function(p) p.Name = PAGINA_SELECCION_PRODUCTOS).First
        RaisePropertyChanged(NameOf(clienteSeleccionado))
    End Sub

    ''' <summary>
    ''' Selecciona un cliente de forma awaitable para restaurar borradores.
    ''' Issue #286: Permite esperar a que los productos se carguen completamente.
    ''' </summary>
    Private Async Function SeleccionarClienteParaBorradorAsync(value As ClienteJson) As Task
        Dim unused = SetProperty(_clienteSeleccionado, value)
        RaisePropertyChanged(NameOf(hayUnClienteSeleccionado))
        ' Sincronizar datos del cliente con Estado
        Estado.Empresa = value.empresa
        Estado.Cliente = value.cliente
        Estado.Contacto = value.contacto
        Estado.NombreCliente = value.nombre
        Estado.IvaCliente = value.iva
        Estado.EstadoCliente = value.estado
        Estado.ComentarioPickingCliente = value.comentarioPicking
        Estado.ComentarioPicking = value.comentarioPicking
        Titulo = String.Format("Plantilla Ventas ({0})", value.cliente)
        Await CargarProductosPlantillaAsync() ' Awaitable en lugar de fire-and-forget
        cmdComprobarPendientes.Execute()
        iva = clienteSeleccionado.iva
        PaginaActual = PaginasWizard.Where(Function(p) p.Name = PAGINA_SELECCION_PRODUCTOS).First
        RaisePropertyChanged(NameOf(clienteSeleccionado))
    End Function
    Private Sub NavegarAClienteCrear(value As ClienteJson)
        Dim parameters As New NavigationParameters From {
            {"empresaParameter", value.empresa},
            {"clienteParameter", value.cliente},
            {"contactoParameter", value.contacto}
        }
        regionManager.RequestNavigate("MainRegion", "CrearClienteView", parameters)
    End Sub
    Private Sub ActualizarCliente(cliente As Clientes)
        Dim clienteEncontrado As ClienteJson = listaClientes?.SingleOrDefault(Function(c) c.empresa = cliente.Empresa.Trim() AndAlso c.cliente = cliente.Nº_Cliente.Trim() AndAlso c.contacto = cliente.Contacto.Trim())
        If Not IsNothing(clienteEncontrado) Then
            clienteEncontrado.nombre = cliente.Nombre
            clienteEncontrado.direccion = cliente.Dirección
            clienteEncontrado.poblacion = cliente.Población
            clienteEncontrado.comentarios = cliente.Comentarios
            clienteEncontrado.estado = cliente.Estado
            clienteEncontrado.cifNif = cliente.CIF_NIF
        End If
    End Sub


    Public Async Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo
        ' Issue #94: Cargar cache de productos bonificables (Ganavisiones)
        If _productosBonificablesIds Is Nothing Then
            ' Usar ConfigureAwait(True) para que RaisePropertyChanged se ejecute en el hilo de la UI
            _productosBonificablesIds = Await servicio.CargarProductosBonificablesIds().ConfigureAwait(True)
            ' Notificar que HayGanavisionesDisponibles puede haber cambiado ahora que tenemos los IDs
            RaisePropertyChanged(NameOf(HayGanavisionesDisponibles))
        End If

        ' Issue #286: Cargar lista de borradores guardados
        OnActualizarListaBorradores()
    End Sub

    Public Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean Implements INavigationAware.IsNavigationTarget
        Return False
    End Function

    Public Sub OnNavigatedFrom(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedFrom

    End Sub

    Public Function ConfirmTabClose() As Boolean Implements ITabCloseConfirmation.ConfirmTabClose
        Dim hayAlgunProducto = listaProductosPedido IsNot Nothing AndAlso
                               listaProductosPedido.Any(Function(p) p.cantidad <> 0 OrElse p.cantidadOferta <> 0)

        If hayAlgunProducto Then
            ' Issue #286: Ofrecer guardar borrador antes de salir
            Dim puedeGuardarBorrador = clienteSeleccionado IsNot Nothing

            If puedeGuardarBorrador Then
                Dim deseaGuardar = dialogService.ShowConfirmationAnswer("Confirmar cierre",
                    "Hay productos en la plantilla que no se han enviado." & vbCrLf & vbCrLf &
                    "¿Desea guardar un borrador para continuar más tarde?")

                If deseaGuardar Then
                    Try
                        Dim borrador = CrearBorradorDesdeEstadoActual("Guardado manualmente al salir")
                        servicioBorradores.GuardarBorrador(borrador)
                        dialogService.ShowNotification("Borrador guardado",
                            "Borrador guardado correctamente. Podrá recuperarlo la próxima vez que abra la Plantilla de Ventas.")
                    Catch ex As Exception
                        dialogService.ShowError($"Error al guardar el borrador: {ex.Message}")
                    End Try
                    Return True
                Else
                    ' No quiere guardar, preguntar si quiere salir sin guardar
                    Return dialogService.ShowConfirmationAnswer("Confirmar cierre",
                        "¿Salir sin guardar el borrador?")
                End If
            Else
                Return dialogService.ShowConfirmationAnswer("Confirmar cierre",
                    "Hay productos en la plantilla que no se han enviado." & vbCrLf &
                    "(No se puede guardar borrador porque no hay cliente seleccionado)" & vbCrLf & vbCrLf &
                    "¿Seguro que desea salir?")
            End If
        End If

        Return True ' Cerrar sin confirmación si no hay productos
    End Function

#End Region

#Region "Borradores - Issue #286"
    ''' <summary>
    ''' Lista de borradores guardados localmente
    ''' </summary>
    Private _listaBorradores As ObservableCollection(Of BorradorPlantillaVenta)
    Public Property ListaBorradores As ObservableCollection(Of BorradorPlantillaVenta)
        Get
            Return _listaBorradores
        End Get
        Set(value As ObservableCollection(Of BorradorPlantillaVenta))
            Dim unused = SetProperty(_listaBorradores, value)
        End Set
    End Property

    ''' <summary>
    ''' Indica si hay borradores guardados
    ''' </summary>
    Public ReadOnly Property HayBorradores As Boolean
        Get
            Return ListaBorradores IsNot Nothing AndAlso ListaBorradores.Count > 0
        End Get
    End Property

    ''' <summary>
    ''' Número de borradores guardados
    ''' </summary>
    Public ReadOnly Property NumeroBorradores As Integer
        Get
            Return If(ListaBorradores IsNot Nothing, ListaBorradores.Count, 0)
        End Get
    End Property

    Private _guardarBorradorCommand As DelegateCommand
    Public Property GuardarBorradorCommand As DelegateCommand
        Get
            Return _guardarBorradorCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_guardarBorradorCommand, value)
        End Set
    End Property

    Private Function CanGuardarBorrador() As Boolean
        ' Se puede guardar si hay un cliente seleccionado y al menos una línea con cantidad
        Return clienteSeleccionado IsNot Nothing AndAlso
               direccionEntregaSeleccionada IsNot Nothing AndAlso
               listaProductosPedido IsNot Nothing AndAlso
               listaProductosPedido.Any(Function(p) p.cantidad <> 0 OrElse p.cantidadOferta <> 0)
    End Function

    ''' <summary>
    ''' Crea un borrador desde el estado actual de la PlantillaVenta.
    ''' Issue #287: Usa SincronizarListasAlEstado() y lee todo desde Estado.
    ''' </summary>
    Private Function CrearBorradorDesdeEstadoActual(Optional mensajeError As String = Nothing) As BorradorPlantillaVenta
        ' Sincronizar listas al Estado primero
        SincronizarListasAlEstado()

        ' Sincronizar campos que tienen binding directo a otros objetos (no a Estado)
        If direccionEntregaSeleccionada IsNot Nothing Then
            Estado.MantenerJunto = direccionEntregaSeleccionada.mantenerJunto
        End If

        Dim borrador As New BorradorPlantillaVenta With {
            .FechaCreacion = DateTime.Now,
            .Usuario = configuracion?.usuario,
            .MensajeError = mensajeError,
            .Empresa = Estado.Empresa,
            .Cliente = Estado.Cliente,
            .Contacto = Estado.Contacto,
            .NombreCliente = Estado.NombreCliente,
            .ComentarioRuta = Estado.ComentarioRuta,
            .ComentarioPicking = Estado.ComentarioPicking,
            .EsPresupuesto = Estado.EsPresupuesto,
            .FormaVenta = Estado.FormaVenta,
            .FormaVentaOtrasCodigo = Estado.FormaVentaOtrasCodigo,
            .FormaPago = Estado.FormaPago,
            .PlazosPago = Estado.PlazosPago,
            .FechaEntrega = Estado.FechaEntrega,
            .AlmacenCodigo = Estado.AlmacenCodigo,
            .MantenerJunto = Estado.MantenerJunto,
            .ServirJunto = Estado.ServirJunto,
            .LineasProducto = Estado.LineasProducto,
            .LineasRegalo = Estado.LineasRegalo,
            .Total = Estado.BaseImponible,
            .ServirPorGlovo = Estado.EnviarPorGlovo,
            .MandarCobroTarjeta = Estado.MandarCobroTarjeta,
            .CobroTarjetaCorreo = Estado.CobroTarjetaCorreo,
            .CobroTarjetaMovil = Estado.CobroTarjetaMovil
        }

        Return borrador
    End Function

    Private Sub OnGuardarBorrador()
        Try
            Dim borrador = CrearBorradorDesdeEstadoActual()
            borrador = servicioBorradores.GuardarBorrador(borrador)

            dialogService.ShowNotification($"Borrador guardado correctamente ({borrador.NumeroLineas} líneas)")

            ' Actualizar lista de borradores
            OnActualizarListaBorradores()
        Catch ex As Exception
            dialogService.ShowError($"Error al guardar borrador: {ex.Message}")
        End Try
    End Sub

    Private _cargarBorradorCommand As DelegateCommand(Of BorradorPlantillaVenta)
    Public Property CargarBorradorCommand As DelegateCommand(Of BorradorPlantillaVenta)
        Get
            Return _cargarBorradorCommand
        End Get
        Private Set(value As DelegateCommand(Of BorradorPlantillaVenta))
            Dim unused = SetProperty(_cargarBorradorCommand, value)
        End Set
    End Property

    Private Async Sub OnCargarBorrador(borradorResumen As BorradorPlantillaVenta)
        If borradorResumen Is Nothing Then
            Return
        End If

        Try
            estaOcupado = True

            ' Cargar el borrador completo (con las líneas)
            Dim borrador = servicioBorradores.CargarBorrador(borradorResumen.Id)
            If borrador Is Nothing Then
                dialogService.ShowError("No se pudo cargar el borrador")
                Return
            End If

            ' Issue #286: Establecer flags para indicar que estamos cargando desde borrador
            ' Esto evita que otros procesos sobrescriban los datos del borrador
            _cargandoDesdeBorrador = True
            _borradorEnRestauracion = borrador
            _borradorRestauradoEnFormasVenta = False ' Resetear para que se apliquen los nuevos valores

            ' Issue #288: Establecer FechaEntrega tempranamente para que la lógica de
            ' fechaMinimaEntrega (que se ejecuta async durante la carga del cliente)
            ' no la pise con Date.Today
            If borrador.FechaEntrega > Date.MinValue Then
                Estado.FechaEntrega = borrador.FechaEntrega
            End If

            ' Verificar que tiene datos válidos
            Dim tieneLineasProducto = borrador.LineasProducto IsNot Nothing AndAlso borrador.LineasProducto.Any()
            Dim tieneLineasRegalo = borrador.LineasRegalo IsNot Nothing AndAlso borrador.LineasRegalo.Any()

            If Not tieneLineasProducto AndAlso Not tieneLineasRegalo Then
                dialogService.ShowError("El borrador no tiene líneas de productos")
                Return
            End If

            ' Buscar y seleccionar el cliente
            If String.IsNullOrEmpty(borrador.Cliente) Then
                dialogService.ShowError("El borrador no tiene cliente asociado")
                Return
            End If

            ' Establecer el filtro con el número de cliente para que la búsqueda funcione
            filtroCliente = borrador.Cliente.Trim()
            RaisePropertyChanged(NameOf(filtroCliente))

            ' Cargar clientes usando el número de cliente como filtro
            Try
                vendedorUsuario = Await leerParametro(Parametros.Claves.Vendedor)
                Dim parametroClientesTodosVendedores As String = Await leerParametro("PermitirVerClientesTodosLosVendedores")
                todosLosVendedores = If(parametroClientesTodosVendedores?.Trim() = "1", True, False)
                Dim clientes = Await servicio.CargarClientesVendedor(filtroCliente, vendedorUsuario, todosLosVendedores)
                listaClientes = New ObservableCollection(Of ClienteJson)(clientes)
            Catch ex As Exception
                dialogService.ShowError($"Error al buscar cliente: {ex.Message}")
                Return
            End Try

            ' Buscar el cliente y contacto en la lista cargada
            Dim clienteEncontrado = listaClientes?.FirstOrDefault(Function(c) _
                c.cliente?.Trim() = borrador.Cliente?.Trim() AndAlso
                c.contacto?.Trim() = borrador.Contacto?.Trim())

            ' Si no encuentra con el contacto exacto, buscar solo por cliente
            If clienteEncontrado Is Nothing Then
                clienteEncontrado = listaClientes?.FirstOrDefault(Function(c) c.cliente?.Trim() = borrador.Cliente?.Trim())
            End If

            If clienteEncontrado Is Nothing Then
                dialogService.ShowError($"No se encontró el cliente {borrador.Cliente} en sus clientes asignados")
                Return
            End If

            ' Issue #286: Usar método awaitable para cargar cliente y productos
            ' Esto espera a que los productos estén completamente cargados antes de aplicar cantidades
            Await SeleccionarClienteParaBorradorAsync(clienteEncontrado)

            ' Cargar líneas de productos del borrador
            ' Como guardamos LineaPlantillaVenta completas, tenemos todos los datos
            Dim lineasCargadas As Integer = 0
            Dim lineasActualizadas As Integer = 0

            If tieneLineasProducto Then
                For Each lineaBorrador In borrador.LineasProducto
                    ' Buscar si el producto ya está en la plantilla del cliente
                    Dim lineaExistente As LineaPlantillaVenta = Nothing
                    If ListaFiltrableProductos?.ListaOriginal IsNot Nothing Then
                        lineaExistente = ListaFiltrableProductos.ListaOriginal.
                            Cast(Of LineaPlantillaVenta)().
                            FirstOrDefault(Function(p) p.producto?.Trim() = lineaBorrador.producto?.Trim())
                    End If

                    If lineaExistente IsNot Nothing Then
                        ' El producto está en la plantilla - actualizar cantidades (mantiene stock actualizado)
                        lineaExistente.cantidad = lineaBorrador.cantidad
                        lineaExistente.cantidadOferta = lineaBorrador.cantidadOferta
                        lineaExistente.precio = lineaBorrador.precio
                        lineaExistente.descuento = lineaBorrador.descuento
                        lineaExistente.aplicarDescuento = lineaBorrador.aplicarDescuento
                        lineasActualizadas += 1
                    Else
                        ' El producto NO está en la plantilla - añadirlo directamente del borrador
                        ' El borrador tiene todos los datos (nombre, foto, subgrupo, etc.)
                        ' Solo el stock puede estar desactualizado
                        lineaBorrador.stockActualizado = False ' Marcar que el stock puede no estar actualizado
                        ListaFiltrableProductos?.ListaOriginal?.Add(lineaBorrador)
                        lineasCargadas += 1
                    End If
                Next
            End If

            ' Issue #286: Los regalos (Ganavisiones) se restaurarán en CargarProductosBonificablesAsync
            ' cuando el usuario navegue a esa página. Solo guardamos el conteo para el mensaje.
            Dim regalosCargados As Integer = If(tieneLineasRegalo, borrador.LineasRegalo.Count, 0)

            ' Restaurar forma de venta (también se hará en OnCargarFormasVenta cuando listaFormasVenta esté cargada)
            If borrador.FormaVenta > 0 Then
                formaVentaSeleccionada = borrador.FormaVenta
            End If

            ' Restaurar comentarios y presupuesto
            If Not String.IsNullOrEmpty(borrador.ComentarioRuta) Then
                ComentarioRuta = borrador.ComentarioRuta
            End If
            If Not String.IsNullOrEmpty(borrador.ComentarioPicking) Then
                ComentarioPicking = borrador.ComentarioPicking
            End If
            EsPresupuesto = borrador.EsPresupuesto

            ' Restaurar almacén si está guardado y existe en la lista
            If Not String.IsNullOrEmpty(borrador.AlmacenCodigo) AndAlso listaAlmacenes IsNot Nothing Then
                Dim almacenBorrador = listaAlmacenes.FirstOrDefault(Function(a) a.Codigo = borrador.AlmacenCodigo)
                If almacenBorrador IsNot Nothing Then
                    almacenSeleccionado = almacenBorrador
                End If
            End If

            ' Dar tiempo a los selectores para cargar sus datos antes de restaurar
            ' Los controles SelectorDireccionEntrega, SelectorFormaPago y SelectorPlazosPago necesitan cargar primero
            ' También esperamos a que OnCargarFormasVenta termine de calcular fechaMinimaEntrega
            Await Task.Delay(800)

            ' Restaurar contacto/dirección de entrega
            If Not String.IsNullOrEmpty(borrador.Contacto) Then
                ContactoSeleccionado = borrador.Contacto
            End If

            ' Restaurar forma de pago y plazos después del delay
            If Not String.IsNullOrEmpty(borrador.FormaPago) Then
                FormaPagoCliente = borrador.FormaPago
            End If
            If Not String.IsNullOrEmpty(borrador.PlazosPago) Then
                PlazoPagoCliente = borrador.PlazosPago
            End If

            ' Restaurar fecha de entrega DESPUÉS del delay (para que no sea sobrescrita por fechaMinimaEntrega)
            If borrador.FechaEntrega > Date.MinValue Then
                Dim fechaRestaurar = borrador.FechaEntrega
                If fechaRestaurar < fechaMinimaEntrega Then
                    fechaRestaurar = fechaMinimaEntrega
                End If
                Estado.FechaEntrega = fechaRestaurar
                RaisePropertyChanged(NameOf(fechaEntrega))
            End If

            ' Restaurar MantenerJunto y ServirJunto si la dirección está cargada
            If direccionEntregaSeleccionada IsNot Nothing Then
                If borrador.MantenerJunto Then
                    direccionEntregaSeleccionada.mantenerJunto = True
                End If
                If borrador.ServirJunto Then
                    direccionEntregaSeleccionada.servirJunto = True
                End If
                Estado.MantenerJunto = direccionEntregaSeleccionada.mantenerJunto
                Estado.ServirJunto = direccionEntregaSeleccionada.servirJunto
                RaisePropertyChanged(NameOf(direccionEntregaSeleccionada))
            End If

            ' Restaurar ComentarioPicking en el objeto clienteSeleccionado (el TextBox hace binding ahí)
            If Not String.IsNullOrEmpty(borrador.ComentarioPicking) AndAlso clienteSeleccionado IsNot Nothing Then
                clienteSeleccionado.comentarioPicking = borrador.ComentarioPicking
                RaisePropertyChanged(NameOf(clienteSeleccionado))
            End If

            ' Issue #288: Restaurar envío por Glovo y cobro por tarjeta
            EnviarPorGlovo = borrador.ServirPorGlovo
            Estado.MandarCobroTarjeta = borrador.MandarCobroTarjeta
            RaisePropertyChanged(NameOf(MandarCobroTarjeta))
            If Not String.IsNullOrEmpty(borrador.CobroTarjetaCorreo) Then
                CobroTarjetaCorreo = borrador.CobroTarjetaCorreo
            End If
            If Not String.IsNullOrEmpty(borrador.CobroTarjetaMovil) Then
                CobroTarjetaMovil = borrador.CobroTarjetaMovil
            End If

            ' Solo actualizar stocks de productos que NO estaban en la plantilla (añadidos desde el borrador)
            ' Los productos que ya estaban en la plantilla ya tienen stocks actualizados de CargarProductosPlantillaAsync
            Try
                Dim productosSinStock = ListaFiltrableProductos?.ListaOriginal?.
                    Cast(Of LineaPlantillaVenta)().
                    Where(Function(p) (p.cantidad > 0 OrElse p.cantidadOferta > 0) AndAlso Not p.stockActualizado).
                    ToList()

                If productosSinStock IsNot Nothing AndAlso productosSinStock.Any() Then
                    Dim listaPlantilla = New ObservableCollection(Of LineaPlantillaVenta)(productosSinStock)
                    Dim stocksActualizados = Await servicio.PonerStocks(listaPlantilla, almacenSeleccionado.Codigo, Constantes.Almacenes.ALMACENES_STOCK.ToList())

                    Dim productosActualizados As Integer = 0
                    ' Actualizar los productos originales con la info de stock
                    If stocksActualizados IsNot Nothing Then
                        For Each lineaConStock In stocksActualizados
                            Dim lineaOriginal = productosSinStock.FirstOrDefault(Function(l) l.producto?.Trim() = lineaConStock.producto?.Trim())
                            If lineaOriginal IsNot Nothing Then
                                lineaOriginal.stock = lineaConStock.stock
                                lineaOriginal.cantidadDisponible = lineaConStock.cantidadDisponible
                                lineaOriginal.cantidadPendienteRecibir = lineaConStock.cantidadPendienteRecibir
                                lineaOriginal.stocks = lineaConStock.stocks
                                ' Calcular StockDisponibleTodosLosAlmacenes si no viene del API
                                If lineaConStock.StockDisponibleTodosLosAlmacenes > 0 Then
                                    lineaOriginal.StockDisponibleTodosLosAlmacenes = lineaConStock.StockDisponibleTodosLosAlmacenes
                                ElseIf lineaConStock.stocks IsNot Nothing AndAlso lineaConStock.stocks.Count > 0 Then
                                    lineaOriginal.StockDisponibleTodosLosAlmacenes = lineaConStock.stocks.Sum(Function(s) s.cantidadDisponible)
                                End If
                                lineaOriginal.urlImagen = lineaConStock.urlImagen
                                lineaOriginal.stockActualizado = True
                                productosActualizados += 1
                            End If
                        Next
                    End If

                    System.Diagnostics.Debug.WriteLine($"[Borrador] Stocks: {productosSinStock.Count} productos sin stock, {If(stocksActualizados?.Count, 0)} respuestas, {productosActualizados} actualizados")
                Else
                    System.Diagnostics.Debug.WriteLine("[Borrador] Todos los productos ya tienen stock actualizado de la plantilla")
                End If
            Catch ex As Exception
                dialogService.ShowNotification($"No se pudieron actualizar los stocks: {ex.Message}")
                System.Diagnostics.Debug.WriteLine($"[Borrador] Error actualizando stocks: {ex.Message}")
            End Try

            ' Issue #288: Restauración final de FechaEntrega y ServirJunto como red de seguridad.
            ' Las operaciones async de carga de cliente/dirección/formas de venta pueden
            ' sobreescribir estos valores por timing. Al final todo está cargado.
            If _borradorEnRestauracion IsNot Nothing Then
                If _borradorEnRestauracion.FechaEntrega > Date.MinValue Then
                    Dim fechaFinal = _borradorEnRestauracion.FechaEntrega
                    If fechaFinal < fechaMinimaEntrega Then
                        fechaFinal = fechaMinimaEntrega
                    End If
                    Estado.FechaEntrega = fechaFinal
                    RaisePropertyChanged(NameOf(fechaEntrega))
                End If
                If direccionEntregaSeleccionada IsNot Nothing Then
                    direccionEntregaSeleccionada.mantenerJunto = _borradorEnRestauracion.MantenerJunto
                    direccionEntregaSeleccionada.servirJunto = _borradorEnRestauracion.ServirJunto
                    Estado.MantenerJunto = _borradorEnRestauracion.MantenerJunto
                    Estado.ServirJunto = _borradorEnRestauracion.ServirJunto
                    RaisePropertyChanged(NameOf(direccionEntregaSeleccionada))
                End If
            End If

            ' Notificar cambios a la UI
            RaisePropertyChanged(NameOf(baseImponiblePedido))
            RaisePropertyChanged(NameOf(hayProductosEnElPedido))
            RaisePropertyChanged(NameOf(listaProductosPedido))
            RaisePropertyChanged(NameOf(ListaProductosBonificables))

            ' Construir mensaje de resultado
            Dim mensaje = $"Borrador cargado: {lineasActualizadas + lineasCargadas} productos"
            If lineasCargadas > 0 Then
                mensaje &= $" ({lineasCargadas} añadidos)"
            End If
            If regalosCargados > 0 Then
                mensaje &= $", {regalosCargados} regalos"
            End If
            dialogService.ShowNotification(mensaje)

        Catch ex As Exception
            dialogService.ShowError($"Error al cargar borrador: {ex.Message}")
            ' Solo resetear el borrador si hubo error
            _borradorEnRestauracion = Nothing
        Finally
            ' Issue #286: NO resetear _borradorEnRestauracion aquí
            ' Se mantiene para que OnCargarFormasVenta y CargarProductosBonificablesAsync
            ' puedan usarlo cuando el usuario navegue a esas páginas
            ' Se reseteará en OnCrearPedido cuando se cree el pedido exitosamente
            _cargandoDesdeBorrador = False
            estaOcupado = False
        End Try
    End Sub

    Private _copiarBorradorJsonCommand As DelegateCommand(Of BorradorPlantillaVenta)
    Public Property CopiarBorradorJsonCommand As DelegateCommand(Of BorradorPlantillaVenta)
        Get
            Return _copiarBorradorJsonCommand
        End Get
        Private Set(value As DelegateCommand(Of BorradorPlantillaVenta))
            Dim unused = SetProperty(_copiarBorradorJsonCommand, value)
        End Set
    End Property

    Private Sub OnCopiarBorradorJson(borrador As BorradorPlantillaVenta)
        If borrador Is Nothing OrElse String.IsNullOrEmpty(borrador.Id) Then
            Return
        End If

        Try
            Dim rutaArchivo = IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Nesto", "Borradores", $"{borrador.Id}.json")

            If IO.File.Exists(rutaArchivo) Then
                Dim json = IO.File.ReadAllText(rutaArchivo)
                Clipboard.SetText(json)
                dialogService.ShowNotification("JSON del borrador copiado al portapapeles")
            Else
                dialogService.ShowError("No se encontró el archivo del borrador")
            End If
        Catch ex As Exception
            dialogService.ShowError($"Error al copiar JSON: {ex.Message}")
        End Try
    End Sub

    Private _eliminarBorradorCommand As DelegateCommand(Of BorradorPlantillaVenta)
    Public Property EliminarBorradorCommand As DelegateCommand(Of BorradorPlantillaVenta)
        Get
            Return _eliminarBorradorCommand
        End Get
        Private Set(value As DelegateCommand(Of BorradorPlantillaVenta))
            Dim unused = SetProperty(_eliminarBorradorCommand, value)
        End Set
    End Property

    Private Sub OnEliminarBorrador(borrador As BorradorPlantillaVenta)
        If borrador Is Nothing Then
            Return
        End If

        Try
            If servicioBorradores.EliminarBorrador(borrador.Id) Then
                ListaBorradores?.Remove(borrador)
                RaisePropertyChanged(NameOf(HayBorradores))
                RaisePropertyChanged(NameOf(NumeroBorradores))
                dialogService.ShowNotification("Borrador eliminado")
            End If
        Catch ex As Exception
            dialogService.ShowError($"Error al eliminar borrador: {ex.Message}")
        End Try
    End Sub

    Private _actualizarListaBorradoresCommand As DelegateCommand
    Public Property ActualizarListaBorradoresCommand As DelegateCommand
        Get
            Return _actualizarListaBorradoresCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_actualizarListaBorradoresCommand, value)
        End Set
    End Property

    Private Sub OnActualizarListaBorradores()
        Try
            Dim borradores = servicioBorradores.ObtenerBorradores()
            ListaBorradores = New ObservableCollection(Of BorradorPlantillaVenta)(borradores)
            RaisePropertyChanged(NameOf(HayBorradores))
            RaisePropertyChanged(NameOf(NumeroBorradores))
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"Error al actualizar lista de borradores: {ex.Message}")
        End Try
    End Sub

    ' Issue #288: Crear borrador desde JSON del portapapeles
    Private _hayJsonEnPortapapeles As Boolean
    ''' <summary>
    ''' Indica si se ha detectado un JSON válido de borrador en el portapapeles.
    ''' </summary>
    Public Property HayJsonEnPortapapeles As Boolean
        Get
            Return _hayJsonEnPortapapeles
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_hayJsonEnPortapapeles, value)
        End Set
    End Property

    Private _textoJsonPortapapeles As String

    Private _crearBorradorDesdeJsonCommand As DelegateCommand
    Public Property CrearBorradorDesdeJsonCommand As DelegateCommand
        Get
            Return _crearBorradorDesdeJsonCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_crearBorradorDesdeJsonCommand, value)
        End Set
    End Property

    ''' <summary>
    ''' Comprueba si el portapapeles contiene un JSON válido de borrador.
    ''' Se llama al inicializar el módulo y al actualizar la lista de borradores.
    ''' </summary>
    Private Sub ComprobarPortapapeles()
        Try
            If Clipboard.ContainsText() Then
                Dim texto = Clipboard.GetText()
                If servicioBorradores.EsJsonBorradorValido(texto) Then
                    _textoJsonPortapapeles = texto
                    HayJsonEnPortapapeles = True
                    Return
                End If
            End If
        Catch
            ' Clipboard puede lanzar excepciones si está en uso por otro proceso
        End Try
        _textoJsonPortapapeles = Nothing
        HayJsonEnPortapapeles = False
    End Sub

    Private Sub OnCrearBorradorDesdeJson()
        Try
            If String.IsNullOrEmpty(_textoJsonPortapapeles) Then
                dialogService.ShowError("No hay JSON válido en el portapapeles")
                Return
            End If

            Dim borrador = servicioBorradores.CrearBorradorDesdeJson(_textoJsonPortapapeles)
            OnActualizarListaBorradores()

            ' Limpiar estado del portapapeles
            _textoJsonPortapapeles = Nothing
            HayJsonEnPortapapeles = False

            dialogService.ShowNotification($"Borrador creado desde JSON: {borrador.Cliente} - {borrador.NombreCliente} ({borrador.NumeroLineas} líneas)")
        Catch ex As Exception
            dialogService.ShowError($"Error al crear borrador desde JSON: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Guarda automáticamente un borrador cuando falla la creación del pedido.
    ''' </summary>
    Private Sub GuardarBorradorAutomatico(mensajeError As String)
        Try
            Dim borrador = CrearBorradorDesdeEstadoActual(mensajeError)
            borrador = servicioBorradores.GuardarBorrador(borrador)
            dialogService.ShowNotification($"Se ha guardado un borrador automáticamente en caso de que quiera recuperarlo más tarde.")
            OnActualizarListaBorradores()
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"Error al guardar borrador automático: {ex.Message}")
        End Try
    End Sub
#End Region

    Public Class RespuestaAgencia
        Public Property DireccionFormateada As String ' no se usa para servir en 2h ¿vale para algo?
        Public Property Longitud As Double ' no se usa para servir en 2h ¿vale para algo?
        Public Property Latitud As Double ' no se usa para servir en 2h ¿vale para algo?
        Public Property Coste As Decimal
        Public Property Almacen As String
        Public Property CondicionesPagoValidas As Boolean
    End Class
End Class

