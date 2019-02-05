Imports Microsoft.Practices.Unity
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports System.Net.Http
Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports Nesto.Modulos.PlantillaVenta.PlantillaVentaModel
Imports System.Text
Imports Nesto.Contratos
Imports Nesto.Models.PedidoVenta
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Newtonsoft.Json.Linq
Imports Nesto.Modulos.PlantillaVenta

Public Class PlantillaVentaViewModel
    Inherits ViewModelBase

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager

    Private Const ESTADO_LINEA_CURSO As Integer = 1
    Private Const ESTADO_LINEA_PRESUPUESTO As Integer = -3


    Dim formaVentaPedido, delegacionUsuario, almacenRutaUsuario, vendedorUsuario As String

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, configuracion As IConfiguracion)
        Me.configuracion = configuracion
        Me.container = container
        Me.regionManager = regionManager

        Titulo = "Plantilla Ventas"

        cmdAbrirPlantillaVenta = New DelegateCommand(Of Object)(AddressOf OnAbrirPlantillaVenta, AddressOf CanAbrirPlantillaVenta)
        cmdActualizarPrecioProducto = New DelegateCommand(Of Object)(AddressOf OnActualizarPrecioProducto, AddressOf CanActualizarPrecioProducto)
        cmdActualizarProductosPedido = New DelegateCommand(Of Object)(AddressOf OnActualizarProductosPedido, AddressOf CanActualizarProductosPedido)
        cmdBuscarEnTodosLosProductos = New DelegateCommand(Of Object)(AddressOf OnBuscarEnTodosLosProductos, AddressOf CanBuscarEnTodosLosProductos)
        cmdCargarClientesVendedor = New DelegateCommand(Of Object)(AddressOf OnCargarClientesVendedor, AddressOf CanCargarClientesVendedor)
        cmdCargarDireccionesEntrega = New DelegateCommand(Of Object)(AddressOf OnCargarDireccionesEntrega, AddressOf CanCargarDireccionesEntrega)
        cmdCargarFormasPago = New DelegateCommand(Of Object)(AddressOf OnCargarFormasPago, AddressOf CanCargarFormasPago)
        cmdCargarFormasVenta = New DelegateCommand(Of Object)(AddressOf OnCargarFormasVenta, AddressOf CanCargarFormasVenta)
        cmdCargarPlazosPago = New DelegateCommand(Of Object)(AddressOf OnCargarPlazosPago, AddressOf CanCargarPlazosPago)
        CargarProductoCommand = New DelegateCommand(Of Object)(AddressOf OnCargarProducto, AddressOf CanCargarProducto)
        cmdCargarProductosPlantilla = New DelegateCommand(Of Object)(AddressOf OnCargarProductosPlantilla, AddressOf CanCargarProductosPlantilla)
        cmdCargarStockProducto = New DelegateCommand(Of Object)(AddressOf OnCargarStockProducto, AddressOf CanCargarStockProducto)
        cmdCargarUltimasVentas = New DelegateCommand(Of Object)(AddressOf OnCargarUltimasVentas, AddressOf CanCargarUltimasVentas)
        cmdComprobarPendientes = New DelegateCommand(Of Object)(AddressOf OnComprobarPendientes, AddressOf CanComprobarPendientes)
        cmdCrearPedido = New DelegateCommand(AddressOf OnCrearPedido, AddressOf CanCrearPedido)
        cmdFijarFiltroProductos = New DelegateCommand(Of Object)(AddressOf OnFijarFiltroProductos, AddressOf CanFijarFiltroProductos)
        cmdInsertarProducto = New DelegateCommand(Of Object)(AddressOf OnInsertarProducto, AddressOf CanInsertarProducto)
        cmdCalcularSePuedeServirPorGlovo = New DelegateCommand(AddressOf OnCalcularSePuedeServirPorGlovo)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        ' Esto habrá que leerlo de un parámetro de usuario si queremos que algunos usuarios puedan y otros no.
        ' De momento dejamos que aquí todos los usuarios vean a todos los clientes, 
        ' y en la plantilla de NestoWeb haremos que cada vendedor vea sólo los suyos.
        todosLosVendedores = True

        listaAlmacenes = New ObservableCollection(Of tipoAlmacen)
        almacenSeleccionado = New tipoAlmacen("ALG", "Algete")
        listaAlmacenes.Add(almacenSeleccionado)
        listaAlmacenes.Add(New tipoAlmacen("REI", "Reina"))

    End Sub

#Region "Propiedades"
    '*** Propiedades de Prism 
    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    Private _ConfirmationRequest As InteractionRequest(Of IConfirmation)
    Public Property ConfirmationRequest As InteractionRequest(Of IConfirmation)
        Get
            Return _ConfirmationRequest
        End Get
        Private Set(value As InteractionRequest(Of IConfirmation))
            _ConfirmationRequest = value
        End Set
    End Property

    Private resultMessage As String
    Public Property InteractionResultMessage As String
        Get
            Return Me.resultMessage
        End Get
        Set(value As String)
            Me.resultMessage = value
            Me.OnPropertyChanged("InteractionResultMessage")
        End Set
    End Property

    '*** Propiedades de Nesto
    Private vendedor As String = "NV"
    Private ultimaOferta As Integer = 0

    Private _almacenSeleccionado As tipoAlmacen
    Public Property almacenSeleccionado As tipoAlmacen
        Get
            Return _almacenSeleccionado
        End Get
        Set(value As tipoAlmacen)
            SetProperty(_almacenSeleccionado, value)
        End Set
    End Property

    Public ReadOnly Property baseImponiblePedido As Decimal
        Get
            Dim baseImponible As Decimal = 0
            If (Not IsNothing(listaProductosPedido) AndAlso listaProductosPedido.Count > 0) Then
                baseImponible = listaProductosPedido.Sum(Function(l) l.cantidad * l.precio * (1 - l.descuento))
            End If
            OnPropertyChanged("baseImponibleParaPortes")
            Return baseImponible
        End Get
    End Property

    Public ReadOnly Property baseImponibleParaPortes As Decimal
        Get
            Dim baseImponible As Decimal = 0
            If (Not IsNothing(listaProductosPedido) AndAlso listaProductosPedido.Count > 0) Then
                baseImponible = listaProductosPedido.Where(Function(l) l.esSobrePedido = False).Sum(Function(l) l.cantidad * l.precio * (1 - l.descuento))
            End If
            Return baseImponible
        End Get
    End Property

    Private _clienteSeleccionado As ClienteJson
    Public Property clienteSeleccionado As ClienteJson
        Get
            Return _clienteSeleccionado
        End Get
        Set(ByVal value As ClienteJson)
            SetProperty(_clienteSeleccionado, value)
            OnPropertyChanged("hayUnClienteSeleccionado")
            If Not IsNothing(clienteSeleccionado) Then
                Titulo = String.Format("Plantilla Ventas ({0})", clienteSeleccionado.cliente)
                cmdCargarProductosPlantilla.Execute(Nothing)
                cmdComprobarPendientes.Execute(Nothing)
            End If
        End Set
    End Property

    Private _direccionEntregaSeleccionada As DireccionesEntregaJson
    Public Property direccionEntregaSeleccionada As DireccionesEntregaJson
        Get
            Return _direccionEntregaSeleccionada
        End Get
        Set(ByVal value As DireccionesEntregaJson)
            SetProperty(_direccionEntregaSeleccionada, value)
            If fechaMinimaEntrega > fechaEntrega Then
                fechaEntrega = fechaMinimaEntrega
            End If
            OnPropertyChanged("fechaMinimaEntrega")
            ' Se hace así para que coja la fecha de hoy cuando se pueda
            ' Si lo hacemos en otro orden, da error porque ponemos una fecha
            ' menor a la que nos permite el datapicker
            If fechaMinimaEntrega < fechaEntrega Then
                fechaEntrega = fechaMinimaEntrega
            End If
        End Set
    End Property

    Private _direccionGoogleMaps As String
    Public Property DireccionGoogleMaps As String
        Get
            Return _direccionGoogleMaps
        End Get
        Set(value As String)
            SetProperty(_direccionGoogleMaps, value)
        End Set
    End Property

    Private _enviarPorGlovo As Boolean
    Public Property EnviarPorGlovo As Boolean
        Get
            Return _enviarPorGlovo
        End Get
        Set(value As Boolean)
            SetProperty(_enviarPorGlovo, value)
            If _enviarPorGlovo Then
                almacenSeleccionado = listaAlmacenes.Single(Function(a) a.id = "REI")
            End If
        End Set
    End Property

    Private _esPresupuesto As Boolean
    Public Property EsPresupuesto As Boolean
        Get
            Return _esPresupuesto
        End Get
        Set(value As Boolean)
            SetProperty(_esPresupuesto, value)
        End Set
    End Property

    Private _estaOcupado As Boolean = False
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaOcupado, value)
        End Set
    End Property

    Private _fechaEntrega As DateTime = DateTime.MinValue
    Public Property fechaEntrega As DateTime
        Get
            If _fechaEntrega < fechaMinimaEntrega Then
                _fechaEntrega = fechaMinimaEntrega
            End If
            Return _fechaEntrega
        End Get
        Set(ByVal value As DateTime)
            If value < fechaMinimaEntrega Then
                value = fechaMinimaEntrega
            End If
            SetProperty(_fechaEntrega, value)
        End Set
    End Property

    Public ReadOnly Property fechaMinimaEntrega As DateTime
        Get
            Dim fechaMinima As DateTime = IIf(Now.Hour < 11, Today, Today.AddDays(1))
            If Not IsNothing(direccionEntregaSeleccionada) AndAlso Not IsNothing(direccionEntregaSeleccionada.ruta) Then
                If direccionEntregaSeleccionada.ruta <> "FW" AndAlso
                    direccionEntregaSeleccionada.ruta <> "00" AndAlso
                    direccionEntregaSeleccionada.ruta <> "16" AndAlso
                    direccionEntregaSeleccionada.ruta <> "AT" AndAlso
                    direccionEntregaSeleccionada.ruta <> "OT" Then
                    fechaMinima = Today
                End If
            End If
            Return fechaMinima
        End Get
    End Property

    Private _filtroCliente As String
    Public Property filtroCliente As String
        Get
            Return _filtroCliente
        End Get
        Set(ByVal value As String)
            SetProperty(_filtroCliente, value.ToLower)
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

    Private _filtroProducto As String
    Public Property filtroProducto As String
        Get
            Return _filtroProducto
        End Get
        Set(ByVal value As String)
            SetProperty(_filtroProducto, value.ToLower)
            If Not IsNothing(listaProductosFijada) Then
                listaProductos = New ObservableCollection(Of LineaPlantillaJson)(From l In listaProductosFijada Where
                    l IsNot Nothing AndAlso
                    (((l.producto IsNot Nothing) AndAlso (l.producto.ToLower.Contains(filtroProducto)))) OrElse
                    (((l.texto IsNot Nothing) AndAlso (l.texto.ToLower.Contains(filtroProducto)))) OrElse
                    (((l.familia IsNot Nothing) AndAlso (l.familia.ToLower.Contains(filtroProducto)))) OrElse
                    (((l.subGrupo IsNot Nothing) AndAlso (l.subGrupo.ToLower.Contains(filtroProducto))))
                )
            End If
            cmdBuscarEnTodosLosProductos.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _formaPagoSeleccionada As FormaPagoDTO
    Public Property formaPagoSeleccionada() As FormaPagoDTO
        Get
            Return _formaPagoSeleccionada
        End Get
        Set(ByVal value As FormaPagoDTO)
            SetProperty(_formaPagoSeleccionada, value)
            cmdCrearPedido.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _formaVentaDirecta As Boolean
    Public Property formaVentaDirecta() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(1)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 1
        End Set
    End Property

    Private _formaVentaOtras As Boolean
    Public Property formaVentaOtras() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(3)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 3
        End Set
    End Property

    Private _formaVentaTelefono As Boolean
    Public Property formaVentaTelefono() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(2)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 2
        End Set
    End Property

    Private _formaVentaSeleccionada As Integer
    Public Property formaVentaSeleccionada() As Integer
        Get
            Return _formaVentaSeleccionada
        End Get
        Set(ByVal value As Integer)
            SetProperty(_formaVentaSeleccionada, value)
            OnPropertyChanged("formaVentaDirecta")
            OnPropertyChanged("formaVentaTelefono")
            OnPropertyChanged("formaVentaOtras")
        End Set
    End Property

    Private _formaVentaOtrasSeleccionada As FormaVentaDTO
    Public Property formaVentaOtrasSeleccionada As FormaVentaDTO
        Get
            Return _formaVentaOtrasSeleccionada
        End Get
        Set(ByVal value As FormaVentaDTO)
            SetProperty(_formaVentaOtrasSeleccionada, value)
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

    Private _listaAlmacenes As ObservableCollection(Of tipoAlmacen)
    Public Property listaAlmacenes As ObservableCollection(Of tipoAlmacen)
        Get
            Return _listaAlmacenes
        End Get
        Set(value As ObservableCollection(Of tipoAlmacen))
            SetProperty(_listaAlmacenes, value)
        End Set
    End Property

    Private _listaClientes As ObservableCollection(Of ClienteJson)
    Public Property listaClientes As ObservableCollection(Of ClienteJson)
        Get
            Return _listaClientes
        End Get
        Set(ByVal value As ObservableCollection(Of ClienteJson))
            SetProperty(_listaClientes, value)
            cmdCargarClientesVendedor.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _listaClientesOriginal As ObservableCollection(Of ClienteJson)
    Public Property listaClientesOriginal As ObservableCollection(Of ClienteJson)
        Get
            Return _listaClientesOriginal
        End Get
        Set(ByVal value As ObservableCollection(Of ClienteJson))
            SetProperty(_listaClientesOriginal, value)
            listaClientes = listaClientesOriginal
        End Set
    End Property

    Private _listaDireccionesEntrega As ObservableCollection(Of DireccionesEntregaJson)
    Public Property listaDireccionesEntrega As ObservableCollection(Of DireccionesEntregaJson)
        Get
            Return _listaDireccionesEntrega
        End Get
        Set(ByVal value As ObservableCollection(Of DireccionesEntregaJson))
            SetProperty(_listaDireccionesEntrega, value)
            direccionEntregaSeleccionada = (From d In listaDireccionesEntrega Where d.esDireccionPorDefecto).SingleOrDefault
        End Set
    End Property

    Private _listaFormasPago As ObservableCollection(Of FormaPagoDTO)
    Public Property listaFormasPago() As ObservableCollection(Of FormaPagoDTO)
        Get
            Return _listaFormasPago
        End Get
        Set(ByVal value As ObservableCollection(Of FormaPagoDTO))
            SetProperty(_listaFormasPago, value)
        End Set
    End Property

    Private _listaFormasVenta As ObservableCollection(Of FormaVentaDTO)
    Public Property listaFormasVenta() As ObservableCollection(Of FormaVentaDTO)
        Get
            Return _listaFormasVenta
        End Get
        Set(ByVal value As ObservableCollection(Of FormaVentaDTO))
            SetProperty(_listaFormasVenta, value)
        End Set
    End Property

    Private _listaPlazosPago As ObservableCollection(Of PlazoPagoDTO)
    Public Property listaPlazosPago() As ObservableCollection(Of PlazoPagoDTO)
        Get
            Return _listaPlazosPago
        End Get
        Set(ByVal value As ObservableCollection(Of PlazoPagoDTO))
            SetProperty(_listaPlazosPago, value)
        End Set
    End Property

    Private _listaProductos As ObservableCollection(Of LineaPlantillaJson)
    Public Property listaProductos As ObservableCollection(Of LineaPlantillaJson)
        Get
            Return _listaProductos
        End Get
        Set(ByVal value As ObservableCollection(Of LineaPlantillaJson))
            SetProperty(_listaProductos, value)
            OnPropertyChanged("listaProductosPedido")
            OnPropertyChanged("baseImponiblePedido")
            OnPropertyChanged("totalPedido")
        End Set
    End Property

    Private _listaProductosFijada As ObservableCollection(Of LineaPlantillaJson)
    Public Property listaProductosFijada As ObservableCollection(Of LineaPlantillaJson)
        Get
            Return _listaProductosFijada
        End Get
        Set(ByVal value As ObservableCollection(Of LineaPlantillaJson))
            SetProperty(_listaProductosFijada, value)
            listaProductos = listaProductosFijada
        End Set
    End Property

    Private _listaProductosOriginal As ObservableCollection(Of LineaPlantillaJson)
    Public Property listaProductosOriginal As ObservableCollection(Of LineaPlantillaJson)
        Get
            Return _listaProductosOriginal
        End Get
        Set(ByVal value As ObservableCollection(Of LineaPlantillaJson))
            SetProperty(_listaProductosOriginal, value)
            listaProductosFijada = listaProductosOriginal
        End Set
    End Property

    Public ReadOnly Property listaProductosPedido() As ObservableCollection(Of LineaPlantillaJson)
        Get
            If Not IsNothing(listaProductosOriginal) Then
                Return New ObservableCollection(Of LineaPlantillaJson)(From l In listaProductosOriginal Where (l.cantidad > 0 OrElse l.cantidadOferta > 0) Order By l.fechaInsercion)
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
            SetProperty(_listaUltimasVentas, value)
        End Set
    End Property

    Public ReadOnly Property NoHayProductosEnElPedido
        Get
            Return Not hayProductosEnElPedido
        End Get
    End Property

    Private _plazoPagoSeleccionado As PlazoPagoDTO
    Public Property plazoPagoSeleccionado As PlazoPagoDTO
        Get
            Return _plazoPagoSeleccionado
        End Get
        Set(ByVal value As PlazoPagoDTO)
            SetProperty(_plazoPagoSeleccionado, value)
            cmdCrearPedido.RaiseCanExecuteChanged()
            If (Not IsNothing(_plazoPagoSeleccionado)) Then
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
            SetProperty(_portesGlovo, value)
        End Set
    End Property

    Private _productoPedidoSeleccionado As LineaPlantillaJson
    Public Property productoPedidoSeleccionado As LineaPlantillaJson
        Get
            Return _productoPedidoSeleccionado
        End Get
        Set(ByVal value As LineaPlantillaJson)
            SetProperty(_productoPedidoSeleccionado, value)
        End Set
    End Property

    Private _productoSeleccionado As LineaPlantillaJson
    Public Property productoSeleccionado As LineaPlantillaJson
        Get
            Return _productoSeleccionado
        End Get
        Set(ByVal value As LineaPlantillaJson)
            SetProperty(_productoSeleccionado, value)
            cmdCargarUltimasVentas.Execute(productoSeleccionado)
        End Set
    End Property

    Private _respuestaGlovo As RespuestaAgencia
    Public Property RespuestaGlovo As RespuestaAgencia
        Get
            Return _respuestaGlovo
        End Get
        Set(value As RespuestaAgencia)
            SetProperty(_respuestaGlovo, value)
        End Set
    End Property

    Private _sePuedeServirConGlovo As Boolean = False
    Public Property SePuedeServirConGlovo As Boolean
        Get
            Return _sePuedeServirConGlovo
        End Get
        Set(value As Boolean)
            SetProperty(_sePuedeServirConGlovo, value)
            If Not _sePuedeServirConGlovo Then
                EnviarPorGlovo = False
            End If
        End Set
    End Property

    Private _todosLosVendedores As Boolean
    Public Property todosLosVendedores As Boolean
        Get
            Return _todosLosVendedores
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_todosLosVendedores, value)
        End Set
    End Property

    Public ReadOnly Property totalPedido As Decimal
        Get
            Return baseImponiblePedido * 1.21
        End Get
    End Property

#End Region

#Region "Comandos"

    Private _cmdAbrirPlantillaVenta As DelegateCommand(Of Object)
    Public Property cmdAbrirPlantillaVenta As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirPlantillaVenta
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirPlantillaVenta, value)
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
            SetProperty(_cmdActualizarPrecioProducto, value)
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
                    OnPropertyChanged("baseImponiblePedido")
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar el precio y los descuentos especiales"
                    })
                End If

                'Carlos 04/07/18: desactivo porque lo controlamos con las ofertas permitidas
                'Await cmdComprobarCondicionesPrecio.Execute(arg)

                OnPropertyChanged("listaProductosPedido")
                OnPropertyChanged("baseImponiblePedido")
                OnPropertyChanged("totalPedido")
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                'estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdActualizarProductosPedido As DelegateCommand(Of Object)
    Public Property cmdActualizarProductosPedido As DelegateCommand(Of Object)
        Get
            Return _cmdActualizarProductosPedido
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdActualizarProductosPedido, value)
        End Set
    End Property
    Private Function CanActualizarProductosPedido(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnActualizarProductosPedido(arg As Object)
        If IsNothing(arg) Then
            Return
        End If

        cmdActualizarPrecioProducto.Execute(arg)

        If arg.cantidadVendida = 0 AndAlso arg.cantidadAbonada = 0 Then
            cmdInsertarProducto.Execute(arg)
        End If

        If (arg.cantidad + arg.cantidadOferta <> 0) AndAlso Not arg.stockActualizado Then
            cmdCargarStockProducto.Execute(arg)
        End If
        OnPropertyChanged("hayProductosEnElPedido")
        OnPropertyChanged("NoHayProductosEnElPedido")
        If IsNothing(productoSeleccionado) OrElse productoSeleccionado.producto <> arg.producto Then
            productoSeleccionado = arg
        End If

        OnPropertyChanged("productoSeleccionado")
        OnPropertyChanged("listaProductos")
        OnPropertyChanged("listaProductosPedido")
        OnPropertyChanged("baseImponiblePedido")
        OnPropertyChanged("totalPedido")

    End Sub

    Private _cmdBuscarEnTodosLosProductos As DelegateCommand(Of Object)
    Public Property cmdBuscarEnTodosLosProductos As DelegateCommand(Of Object)
        Get
            Return _cmdBuscarEnTodosLosProductos
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdBuscarEnTodosLosProductos, value)
        End Set
    End Property
    Private Function CanBuscarEnTodosLosProductos(arg As Object) As Boolean
        Return Not IsNothing(filtroProducto) AndAlso filtroProducto.Length >= 3
    End Function
    Private Async Sub OnBuscarEnTodosLosProductos(arg As Object)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                Dim url As String = "PlantillaVentas/BuscarProducto?empresa=" + clienteSeleccionado.empresa + "&filtroProducto=" + filtroProducto
                response = Await client.GetAsync(url)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaProductosFijada = JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaJson))(cadenaJson)
                    Dim productoOriginal As LineaPlantillaJson
                    Dim producto As LineaPlantillaJson
                    For i = 0 To listaProductosFijada.Count - 1
                        producto = listaProductosFijada(i)
                        productoOriginal = listaProductosOriginal.Where(Function(p) p.producto = producto.producto).FirstOrDefault
                        If Not IsNothing(productoOriginal) Then
                            listaProductosFijada(i) = productoOriginal
                        End If
                    Next
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar los productos"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                estaOcupado = False
            End Try

        End Using
    End Sub

    Private _cmdCargarClientesVendedor As DelegateCommand(Of Object)
    Public Property cmdCargarClientesVendedor As DelegateCommand(Of Object)
        Get
            Return _cmdCargarClientesVendedor
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarClientesVendedor, value)
        End Set
    End Property
    Private Function CanCargarClientesVendedor(arg As Object) As Boolean
        Return filtroCliente.Length = 0 OrElse IsNothing(listaClientes) OrElse listaClientes.Count = 0
    End Function
    Private Async Sub OnCargarClientesVendedor(arg As Object)

        ' No se puede filtrar por menos de 4 caracteres
        If IsNothing(filtroCliente) OrElse (filtroCliente.Length < 4 AndAlso Not IsNumeric(filtroCliente)) Then
            listaClientes = Nothing
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage


            Try
                estaOcupado = True
                If todosLosVendedores Then
                    response = Await client.GetAsync("Clientes?empresa=1&filtro=" + filtroCliente)
                Else
                    response = Await client.GetAsync("Clientes?empresa=1&vendedor=" + vendedor + "&filtro=" + filtroCliente)
                End If


                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaClientesOriginal = JsonConvert.DeserializeObject(Of ObservableCollection(Of ClienteJson))(cadenaJson)
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar los clientes"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                estaOcupado = False
            End Try
        End Using

    End Sub

    Private _cmdCargarDireccionesEntrega As DelegateCommand(Of Object)
    Public Property cmdCargarDireccionesEntrega As DelegateCommand(Of Object)
        Get
            Return _cmdCargarDireccionesEntrega
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarDireccionesEntrega, value)
        End Set
    End Property
    Private Function CanCargarDireccionesEntrega(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarDireccionesEntrega(arg As Object)

        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                response = Await client.GetAsync("PlantillaVentas/DireccionesEntrega?empresa=" + clienteSeleccionado.empresa + "&clienteDirecciones=" + clienteSeleccionado.cliente)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaDireccionesEntrega = JsonConvert.DeserializeObject(Of ObservableCollection(Of DireccionesEntregaJson))(cadenaJson)
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar las direcciones de entrega"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdCargarFormasPago As DelegateCommand(Of Object)
    Public Property cmdCargarFormasPago As DelegateCommand(Of Object)
        Get
            Return _cmdCargarFormasPago
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarFormasPago, value)
        End Set
    End Property
    Private Function CanCargarFormasPago(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarFormasPago(arg As Object)

        If IsNothing(direccionEntregaSeleccionada) OrElse IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                response = Await client.GetAsync("FormasPago?empresa=" + clienteSeleccionado.empresa + "&cliente=" + clienteSeleccionado.cliente)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaFormasPago = JsonConvert.DeserializeObject(Of ObservableCollection(Of FormaPagoDTO))(cadenaJson)
                    formaPagoSeleccionada = listaFormasPago.Where(Function(l) l.formaPago = direccionEntregaSeleccionada.formaPago).SingleOrDefault
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar las formas de pago"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdCargarFormasVenta As DelegateCommand(Of Object)
    Public Property cmdCargarFormasVenta As DelegateCommand(Of Object)
        Get
            Return _cmdCargarFormasVenta
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarFormasVenta, value)
        End Set
    End Property
    Private Function CanCargarFormasVenta(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarFormasVenta(arg As Object)

        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        vendedorUsuario = Await leerParametro("Vendedor")
        If vendedorUsuario = clienteSeleccionado.vendedor Then
            formaVentaSeleccionada = 1 ' Directa
        Else
            formaVentaSeleccionada = 2 ' Telefono
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
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar las formas de venta"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdCargarPlazosPago As DelegateCommand(Of Object)
    Public Property cmdCargarPlazosPago As DelegateCommand(Of Object)
        Get
            Return _cmdCargarPlazosPago
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarPlazosPago, value)
        End Set
    End Property
    Private Function CanCargarPlazosPago(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarPlazosPago(arg As Object)

        If IsNothing(direccionEntregaSeleccionada) OrElse IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                response = Await client.GetAsync("PlazosPago?empresa=" + clienteSeleccionado.empresa + "&cliente=" + clienteSeleccionado.cliente)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaPlazosPago = JsonConvert.DeserializeObject(Of ObservableCollection(Of PlazoPagoDTO))(cadenaJson)
                    plazoPagoSeleccionado = listaPlazosPago.Where(Function(l) l.plazoPago = direccionEntregaSeleccionada.plazosPago).SingleOrDefault
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar los plazos de pago"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cargarProductoCommand As DelegateCommand(Of Object)
    Public Property CargarProductoCommand As DelegateCommand(Of Object)
        Get
            Return _cargarProductoCommand
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cargarProductoCommand, value)
        End Set
    End Property
    Private Function CanCargarProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnCargarProducto(arg As Object)
        If IsNothing(productoPedidoSeleccionado) Then
            Return
        End If
        Dim parameters As NavigationParameters = New NavigationParameters()
        parameters.Add("numeroProductoParameter", productoPedidoSeleccionado.producto)
        regionManager.RequestNavigate("MainRegion", "ProductoView", parameters)
    End Sub

    Private _cmdCargarProductosPlantilla As DelegateCommand(Of Object)
    Public Property cmdCargarProductosPlantilla As DelegateCommand(Of Object)
        Get
            Return _cmdCargarProductosPlantilla
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarProductosPlantilla, value)
        End Set
    End Property
    Private Function CanCargarProductosPlantilla(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarProductosPlantilla(arg As Object)

        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            client.Timeout = client.Timeout.Add(New TimeSpan(0, 5, 0)) 'cinco minutos más
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                response = Await client.GetAsync("PlantillaVentas?empresa=" + clienteSeleccionado.empresa + "&cliente=" + clienteSeleccionado.cliente)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaProductosOriginal = JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaJson))(cadenaJson)
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
                        contenido = contenido + vbCr + contenido2
                    End While
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar la plantilla" + vbCr + contenido
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = ex.Message
                    })
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdCargarStockProducto As DelegateCommand(Of Object)
    Public Property cmdCargarStockProducto As DelegateCommand(Of Object)
        Get
            Return _cmdCargarStockProducto
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarStockProducto, value)
        End Set
    End Property
    Private Function CanCargarStockProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarStockProducto(arg As Object)

        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            'estaOcupado = True

            Dim datosStock As StockProductoDTO

            Try
                response = Await client.GetAsync("PlantillaVentas/CargarStocks?empresa=" + clienteSeleccionado.empresa + "&almacen=" + almacenSeleccionado.id + "&productoStock=" + arg.producto)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    datosStock = JsonConvert.DeserializeObject(Of StockProductoDTO)(cadenaJson)
                    arg.stock = datosStock.stock
                    arg.cantidadDisponible = datosStock.cantidadDisponible
                    arg.urlImagen = datosStock.urlImagen
                    arg.stockActualizado = True
                    arg.fechaInsercion = Now
                    OnPropertyChanged("listaProductosPedido")
                    OnPropertyChanged("productoSeleccionado")
                    OnPropertyChanged("baseImponiblePedido")
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar el stock del producto"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = ex.Message
                    })
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
            SetProperty(_cmdCargarUltimasVentas, value)
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
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al cargar las últimas ventas del producto"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = ex.Message
                    })
            Finally
                'estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdComprobarPendientes As DelegateCommand(Of Object)
    Public Property cmdComprobarPendientes As DelegateCommand(Of Object)
        Get
            Return _cmdComprobarPendientes
        End Get
        Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdComprobarPendientes, value)
        End Set
    End Property
    Private Function CanComprobarPendientes(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnComprobarPendientes(arg As Object)
        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Try
                Dim urlConsulta As String = "PlantillaVentas/PedidosPendientes?empresa=" + clienteSeleccionado.empresa
                urlConsulta += "&clientePendientes=" + clienteSeleccionado.cliente
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Dim pedidosPendientes As List(Of Integer) = JsonConvert.DeserializeObject(Of List(Of Integer))(cadenaJson)

                    If pedidosPendientes.Count > 0 Then
                        Dim textoMensaje As String = "Este cliente tiene otros pedidos pendientes." + vbCr + vbCr
                        For Each i In pedidosPendientes
                            textoMensaje += i.ToString + vbCr
                        Next
                        textoMensaje += vbCr + "Por favor, revise que sea todo correcto."
                        NotificationRequest.Raise(New Notification() With {
                            .Title = "Pendientes",
                            .Content = textoMensaje
                        })
                    End If
                Else
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Se ha producido un error al comprobar los pedidos pendientes del cliente"
                    })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                'estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdCrearPedido As DelegateCommand
    Public Property cmdCrearPedido As DelegateCommand
        Get
            Return _cmdCrearPedido
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCrearPedido, value)
        End Set
    End Property
    Private Function CanCrearPedido() As Boolean
        Return Not IsNothing(formaPagoSeleccionada) AndAlso Not IsNothing(plazoPagoSeleccionado)
    End Function
    Private Async Sub OnCrearPedido()

        If IsNothing(formaPagoSeleccionada) OrElse IsNothing(plazoPagoSeleccionado) OrElse IsNothing(clienteSeleccionado) Then
            NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = "Compruebe que tiene seleccionados plazos y forma de pago, por favor."
                })
            Return
        End If

        delegacionUsuario = Await leerParametro("DelegaciónDefecto")

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
            NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = "Este contacto no tiene plazos de pago asignados"
                    })
            Return
            'Throw New Exception("Este contacto no tiene plazos de pago asignados")
        End If

        Using client As New HttpClient
            estaOcupado = True

            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim pedido As PedidoVentaDTO = PrepararPedido()

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")

            Try

                response = Await client.PostAsync("PedidosVenta", content)

                If response.IsSuccessStatusCode Then
                    'Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    'listaProductosOriginal = JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaJson))(cadenaJson)
                    Dim pathNumeroPedido = response.Headers.Location.LocalPath
                    Dim numPedido As String = pathNumeroPedido.Substring(pathNumeroPedido.LastIndexOf("/") + 1)
                    'NotificationRequest.Raise(New Notification() With {
                    '    .Title = "Plantilla",
                    '    .Content = "Pedido " + numPedido + " creado correctamente"
                    '})

                    ' Cerramos la ventana
                    Dim view = Me.regionManager.Regions("MainRegion").ActiveViews.FirstOrDefault
                    If Not IsNothing(view) Then
                        Me.regionManager.Regions("MainRegion").Deactivate(view)
                        Me.regionManager.Regions("MainRegion").Remove(view)
                    End If

                    ' Abrimos el pedido
                    PedidoVentaViewModel.cargarPedido(clienteSeleccionado.empresa, numPedido, container)
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
                        contenido = contenido + vbCr + contenido2
                    End While

                    NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = contenido
                })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private Function PrepararPedido() As PedidoVentaDTO
        If IsNothing(formaPagoSeleccionada) Then
            If IsNothing(listaFormasPago) Then
                formaPagoSeleccionada = New FormaPagoDTO With {.formaPago = "RCB", .cccObligatorio = True}
            Else
                formaPagoSeleccionada = listaFormasPago.First
            End If
        End If

        almacenRutaUsuario = almacenSeleccionado.id

        Dim pedido As New PedidoVentaDTO With {
                        .empresa = clienteSeleccionado.empresa,
                        .cliente = clienteSeleccionado.cliente,
                        .contacto = direccionEntregaSeleccionada.contacto,
                        .EsPresupuesto = EsPresupuesto,
                        .fecha = Today,
                        .formaPago = formaPagoSeleccionada.formaPago,
                        .plazosPago = plazoPagoSeleccionado.plazoPago,
                        .primerVencimiento = Today, 'se calcula en la API
                        .iva = clienteSeleccionado.iva,
                        .vendedor = direccionEntregaSeleccionada.vendedor,
                        .periodoFacturacion = direccionEntregaSeleccionada.periodoFacturacion,
                        .ruta = IIf(EnviarPorGlovo, "GLV", direccionEntregaSeleccionada.ruta),
                        .serie = "NV", 'calcular
                        .ccc = IIf(formaPagoSeleccionada.cccObligatorio, direccionEntregaSeleccionada.ccc, Nothing),
                        .origen = clienteSeleccionado.empresa,
                        .contactoCobro = clienteSeleccionado.contacto, 'calcular
                        .noComisiona = direccionEntregaSeleccionada.noComisiona,
                        .mantenerJunto = direccionEntregaSeleccionada.mantenerJunto,
                        .servirJunto = direccionEntregaSeleccionada.servirJunto,
                        .comentarioPicking = clienteSeleccionado.comentarioPicking,
                        .comentarios = direccionEntregaSeleccionada.comentarioRuta,
                        .usuario = System.Environment.UserDomainName + "\" + System.Environment.UserName
                    }

        Dim lineaPedido, lineaPedidoOferta As LineaPedidoVentaDTO
        Dim ofertaLinea As Integer?

        For Each linea In listaProductosPedido
            If linea.cantidadOferta <> 0 Then
                ofertaLinea = cogerSiguienteOferta()
            Else
                ofertaLinea = Nothing
            End If

            'If linea.descuento = linea.descuentoProducto Then
            '    linea.descuento = 0
            'Else
            '    linea.aplicarDescuento = False
            'End If

            lineaPedido = New LineaPedidoVentaDTO With {
                .estado = IIf(EsPresupuesto, ESTADO_LINEA_PRESUPUESTO, ESTADO_LINEA_CURSO), '¿Pongo 0 para tener que validar?
                .tipoLinea = 1, ' Producto
                .producto = linea.producto,
                .texto = linea.texto,
                .cantidad = linea.cantidad,
                .fechaEntrega = fechaEntrega,
                .precio = linea.precio,
                .descuento = IIf(linea.descuento = linea.descuentoProducto, 0, linea.descuento),
                .descuentoProducto = IIf(linea.descuento = linea.descuentoProducto, linea.descuentoProducto, 0),
                .aplicarDescuento = IIf(linea.descuento = linea.descuentoProducto, linea.aplicarDescuento, False),
                .vistoBueno = 0, 'calcular
                .usuario = System.Environment.UserDomainName + "\" + System.Environment.UserName,
                .almacen = almacenRutaUsuario,
                .iva = linea.iva,
                .delegacion = delegacionUsuario, 'pedir al usuario en alguna parte
                .formaVenta = formaVentaPedido,
                .oferta = ofertaLinea
            }
            If linea.cantidad <> 0 Then
                pedido.LineasPedido.Add(lineaPedido)
            End If

            If linea.cantidadOferta <> 0 Then
                lineaPedidoOferta = lineaPedido.ShallowCopy
                lineaPedidoOferta.cantidad = linea.cantidadOferta
                lineaPedidoOferta.precio = 0
                lineaPedidoOferta.oferta = lineaPedido.oferta
                pedido.LineasPedido.Add(lineaPedidoOferta)
            End If

        Next

        Return pedido
    End Function

    Private _cmdFijarFiltroProductos As DelegateCommand(Of Object)
    Public Property cmdFijarFiltroProductos As DelegateCommand(Of Object)
        Get
            Return _cmdFijarFiltroProductos
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdFijarFiltroProductos, value)
        End Set
    End Property
    Private Function CanFijarFiltroProductos(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnFijarFiltroProductos(arg As Object)
        If filtroProducto.Length > 0 Then
            listaProductosFijada = listaProductos
        Else
            listaProductosFijada = listaProductosOriginal
        End If
    End Sub

    Private _cmdInsertarProducto As DelegateCommand(Of Object)
    Public Property cmdInsertarProducto As DelegateCommand(Of Object)
        Get
            Return _cmdInsertarProducto
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdInsertarProducto, value)
        End Set
    End Property
    Private Function CanInsertarProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnInsertarProducto(arg As Object)
        ' Solo insertamos si es un producto que no está en listaProductosOrigina
        If IsNothing(arg) OrElse Not IsNothing(listaProductosOriginal.Where(Function(p) p.producto = arg.producto).FirstOrDefault) Then
            Return
        End If
        'arg.cantidadVendida = arg.cantidad + arg.cantidadOferta
        listaProductosOriginal.Add(arg)
        'listaProductos = listaProductosOriginal
        'filtroProducto = ""
        OnPropertyChanged("baseImponiblePedido")
        OnPropertyChanged("listaProductos")
        OnPropertyChanged("listaProductosOriginal")
    End Sub

    Private _cmdCalcularSePuedeServirPorGlovo As DelegateCommand
    Public Property cmdCalcularSePuedeServirPorGlovo As DelegateCommand
        Get
            Return _cmdCalcularSePuedeServirPorGlovo
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCalcularSePuedeServirPorGlovo, value)
        End Set
    End Property
    Private Async Sub OnCalcularSePuedeServirPorGlovo()
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
                        SePuedeServirConGlovo = True
                        DireccionGoogleMaps = RespuestaGlovo.DireccionFormateada
                        PortesGlovo = RespuestaGlovo.Coste
                    Else
                        SePuedeServirConGlovo = False
                        DireccionGoogleMaps = ""
                        PortesGlovo = 0
                    End If
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
                        contenido = contenido + vbCr + contenido2
                    End While

                    NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = contenido
                })
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
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
        If IsNothing(clienteSeleccionado) Then
            Return ""
        End If

        Return Await configuracion.leerParametro(clienteSeleccionado.empresa, v)
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
                    contador = contador + 1
                    repetir = True
                    Exit For
                End If
            Next
        End While
        Return nombreAmpliado
    End Function


#End Region

    Public Structure tipoAlmacen
        Public Sub New(
       ByVal _id As String,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As String
        Property descripcion As String
    End Structure

    Public Class RespuestaAgencia
        Public Property DireccionFormateada As String
        Public Property Longitud As Double
        Public Property Latitud As Double
        Public Property Coste As Decimal
    End Class
End Class

