Imports Microsoft.Practices.Unity
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Mvvm
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports System.Net.Http
Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports Nesto.Modulos.PlantillaVenta.PlantillaVentaModel

Public Class PlantillaVentaViewModel
    Inherits BindableBase

    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager)
        Me.container = container
        Me.regionManager = regionManager


        cmdAbrirPlantillaVenta = New DelegateCommand(Of Object)(AddressOf OnAbrirPlantillaVenta, AddressOf CanAbrirPlantillaVenta)
        cmdActualizarProductosPedido = New DelegateCommand(Of Object)(AddressOf OnActualizarProductosPedido, AddressOf CanActualizarProductosPedido)
        cmdBuscarEnTodosLosProductos = New DelegateCommand(Of Object)(AddressOf OnBuscarEnTodosLosProductos, AddressOf CanBuscarEnTodosLosProductos)
        cmdCargarClientesVendedor = New DelegateCommand(Of Object)(AddressOf OnCargarClientesVendedor, AddressOf CanCargarClientesVendedor)
        cmdCargarDireccionesEntrega = New DelegateCommand(Of Object)(AddressOf OnCargarDireccionesEntrega, AddressOf CanCargarDireccionesEntrega)
        cmdCargarProductosPlantilla = New DelegateCommand(Of Object)(AddressOf OnCargarProductosPlantilla, AddressOf CanCargarProductosPlantilla)
        cmdFijarFiltroProductos = New DelegateCommand(Of Object)(AddressOf OnFijarFiltroProductos, AddressOf CanFijarFiltroProductos)
        cmdInsertarProducto = New DelegateCommand(Of Object)(AddressOf OnInsertarProducto, AddressOf CanInsertarProducto)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)


        ' Esto habrá que leerlo de un parámetro de usuario si queremos que algunos usuarios puedan y otros no.
        ' De momento dejamos que aquí todos los usuarios vean a todos los clientes, 
        ' y en la plantilla de NestoWeb haremos que cada vendedor vea sólo los suyos.
        todosLosVendedores = True
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

    Private _clienteSeleccionado As ClienteJson
    Public Property clienteSeleccionado As ClienteJson
        Get
            Return _clienteSeleccionado
        End Get
        Set(ByVal value As ClienteJson)
            SetProperty(_clienteSeleccionado, value)
            OnPropertyChanged("hayUnClienteSeleccionado")
            cmdCargarProductosPlantilla.Execute(Nothing)
            'cmdCargarDireccionesEntrega.Execute(Nothing)
        End Set
    End Property

    Private _direccionEntregaSeleccionada As DireccionesEntregaJson
    Public Property direccionEntregaSeleccionada As DireccionesEntregaJson
        Get
            Return _direccionEntregaSeleccionada
        End Get
        Set(ByVal value As DireccionesEntregaJson)
            SetProperty(_direccionEntregaSeleccionada, value)
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

    Private _listaProductos As ObservableCollection(Of LineaPlantillaJson)
    Public Property listaProductos As ObservableCollection(Of LineaPlantillaJson)
        Get
            Return _listaProductos
        End Get
        Set(ByVal value As ObservableCollection(Of LineaPlantillaJson))
            SetProperty(_listaProductos, value)
            OnPropertyChanged("listaProductosPedido")
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
                Return New ObservableCollection(Of LineaPlantillaJson)(From l In listaProductosOriginal Where l.cantidad > 0 OrElse l.cantidadOferta > 0)
            Else
                Return Nothing
            End If
        End Get
    End Property

    Private _productoSeleccionado As LineaPlantillaJson
    Public Property productoSeleccionado As LineaPlantillaJson
        Get
            Return _productoSeleccionado
        End Get
        Set(ByVal value As LineaPlantillaJson)
            SetProperty(_productoSeleccionado, value)
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
        'regionManager.RegisterViewWithRegion("MainRegion", GetType(PlantillaVentaView))
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of PlantillaVentaView)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
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
        OnPropertyChanged("listaProductosPedido")
        OnPropertyChanged("hayProductosEnElPedido")
        If Not IsNothing(arg) Then
            If arg.cantidadVendida = 0 AndAlso arg.cantidadAbonada = 0 Then
                cmdInsertarProducto.Execute(arg)
            End If
        End If
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
            client.BaseAddress = New Uri(My.Resources.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True
            response = Await client.GetAsync("PlantillaVentas/BuscarProducto?empresa=" + clienteSeleccionado.empresa + "&filtroProducto=" + filtroProducto)

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
                estaOcupado = False
            Else
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = "Se ha producido un error al cargar los productos"
                })
                estaOcupado = False
            End If
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
        If IsNothing(filtroCliente) OrElse filtroCliente.Length < 4 Then
            listaClientes = Nothing
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(My.Resources.servidorAPI)
            Dim response As HttpResponseMessage



            estaOcupado = True
            If todosLosVendedores Then
                response = Await client.GetAsync("Clientes?empresa=1&filtro=" + filtroCliente)
            Else
                response = Await client.GetAsync("Clientes?empresa=1&vendedor=" + vendedor + "&filtro=" + filtroCliente)
            End If


            If response.IsSuccessStatusCode Then
                Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                listaClientesOriginal = JsonConvert.DeserializeObject(Of ObservableCollection(Of ClienteJson))(cadenaJson)
                estaOcupado = False
            Else
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = "Se ha producido un error al cargar los clientes"
                })
                estaOcupado = False
            End If

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
            client.BaseAddress = New Uri(My.Resources.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True
            response = Await client.GetAsync("PlantillaVentas/DireccionesEntrega?empresa=" + clienteSeleccionado.empresa + "&clienteDirecciones=" + clienteSeleccionado.cliente)

            If response.IsSuccessStatusCode Then
                Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                listaDireccionesEntrega = JsonConvert.DeserializeObject(Of ObservableCollection(Of DireccionesEntregaJson))(cadenaJson)
                estaOcupado = False
            Else
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = "Se ha producido un error al cargar las direcciones de entrega"
                })
                estaOcupado = False
            End If
        End Using

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
            client.BaseAddress = New Uri(My.Resources.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True
            response = Await client.GetAsync("PlantillaVentas?empresa=" + clienteSeleccionado.empresa + "&cliente=" + clienteSeleccionado.cliente)

            If response.IsSuccessStatusCode Then
                Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                listaProductosOriginal = JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaJson))(cadenaJson)
                estaOcupado = False
            Else
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = "Se ha producido un error al cargar la plantilla"
                })
                estaOcupado = False
            End If
        End Using

    End Sub

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
            'OnPropertyChanged("listaProductosPedido")' 
            Return
        End If
        'arg.cantidadVendida = arg.cantidad + arg.cantidadOferta
        listaProductosOriginal.Add(arg)
        listaProductos = listaProductosOriginal
        filtroProducto = ""
    End Sub

#End Region

#Region "Funciones Auxiliares"

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



End Class

