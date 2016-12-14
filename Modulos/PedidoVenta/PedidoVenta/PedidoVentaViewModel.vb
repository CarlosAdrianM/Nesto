Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.Regions
Imports Nesto.Contratos
Imports Nesto.Models.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

Public Class PedidoVentaViewModel
    Inherits ViewModelBase

    Private estaActualizarFechaActivo As Boolean = True
    Private ReadOnly regionManager As IRegionManager
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IPedidoVentaService
    Private vendedor As String
    Private verTodosLosVendedores As Boolean = False


    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IPedidoVentaService)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio

        cmdAbrirModulo = New DelegateCommand(Of Object)(AddressOf OnAbrirModulo, AddressOf CanAbrirModulo)
        cmdActualizarTotales = New DelegateCommand(Of Object)(AddressOf OnActualizarTotales, AddressOf CanActualizarTotales)
        cmdCambiarFechaEntrega = New DelegateCommand(Of Object)(AddressOf OnCambiarFechaEntrega, AddressOf CanCambiarFechaEntrega)
        cmdCargarListaPedidos = New DelegateCommand(Of Object)(AddressOf OnCargarListaPedidos, AddressOf CanCargarListaPedidos)
        cmdCargarPedido = New DelegateCommand(Of Object)(AddressOf OnCargarPedido, AddressOf CanCargarPedido)
        cmdCeldaModificada = New DelegateCommand(Of Object)(AddressOf OnCeldaModificada, AddressOf CanCeldaModificada)
        cmdModificarPedido = New DelegateCommand(Of Object)(AddressOf OnModificarPedido, AddressOf CanModificarPedido)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        Titulo = "Lista de Pedidos"
    End Sub

#Region "Propiedades de Prism"
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

#End Region

#Region "Propiedades"
    Private _celdaActual As DataGridCellInfo
    Public Property celdaActual As DataGridCellInfo
        Get
            Return _celdaActual
        End Get
        Set(value As DataGridCellInfo)
            SetProperty(_celdaActual, value)
        End Set
    End Property

    Private _estaCargandoListaPedidos As Boolean
    Public Property estaCargandoListaPedidos() As Boolean
        Get
            Return _estaCargandoListaPedidos
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaCargandoListaPedidos, value)
        End Set
    End Property

    Private _fechaEntrega As Date
    Public Property fechaEntrega As Date
        Get
            Return _fechaEntrega
        End Get
        Set(value As Date)
            SetProperty(_fechaEntrega, value)
            If estaActualizarFechaActivo Then
                cmdCambiarFechaEntrega.Execute(Nothing)
                OnPropertyChanged("pedido")
            End If
        End Set
    End Property

    Private _lineaActual As LineaPedidoVentaDTO
    Public Property lineaActual As LineaPedidoVentaDTO
        Get
            Return _lineaActual
        End Get
        Set(value As LineaPedidoVentaDTO)
            SetProperty(_lineaActual, value)
            OnPropertyChanged("pedido")
        End Set
    End Property

    Private _listaPedidos As ObservableCollection(Of ResumenPedido)
    Public Property listaPedidos() As ObservableCollection(Of ResumenPedido)
        Get
            Return _listaPedidos
        End Get
        Set(ByVal value As ObservableCollection(Of ResumenPedido))
            SetProperty(_listaPedidos, value)
        End Set
    End Property

    Private _pedido As PedidoVentaDTO
    Public Property pedido() As PedidoVentaDTO
        Get
            Return _pedido
        End Get
        Set(ByVal value As PedidoVentaDTO)
            SetProperty(_pedido, value)
            estaActualizarFechaActivo = False
            fechaEntrega = pedido.LineasPedido.FirstOrDefault(Function(l) l.estado >= -1 And l.estado <= 1)?.fechaEntrega
            estaActualizarFechaActivo = True
        End Set
    End Property

    Private _resumenSeleccionado As ResumenPedido
    Public Property resumenSeleccionado() As ResumenPedido
        Get
            Return _resumenSeleccionado
        End Get
        Set(ByVal value As ResumenPedido)
            SetProperty(_resumenSeleccionado, value)
            cmdCargarPedido.Execute(value)
        End Set
    End Property

#End Region

#Region "Comandos"
    Private _cmdAbrirModulo As DelegateCommand(Of Object)
    Public Property cmdAbrirModulo As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirModulo
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirModulo, value)
        End Set
    End Property
    Private Function CanAbrirModulo(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirModulo(arg As Object)
        regionManager.RequestNavigate("MainRegion", "PedidoVentaView")
    End Sub

    Private _cmdActualizarTotales As DelegateCommand(Of Object)
    Public Property cmdActualizarTotales As DelegateCommand(Of Object)
        Get
            Return _cmdActualizarTotales
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdActualizarTotales, value)
        End Set
    End Property
    Private Function CanActualizarTotales(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnActualizarTotales(arg As Object)
        OnPropertyChanged("pedido")
    End Sub

    Private _cmdCambiarFechaEntrega As DelegateCommand(Of Object)
    Public Property cmdCambiarFechaEntrega As DelegateCommand(Of Object)
        Get
            Return _cmdCambiarFechaEntrega
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCambiarFechaEntrega, value)
        End Set
    End Property
    Private Function CanCambiarFechaEntrega(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnCambiarFechaEntrega(arg As Object)
        'pedido.LineasPedido.ToList.ForEach(Function(l)
        '                                       l.fechaEntrega = fechaEntrega
        '                                       Return l
        '                                   End Function)
        For Each linea In pedido.LineasPedido
            linea.fechaEntrega = fechaEntrega
        Next

        OnPropertyChanged("pedido")
    End Sub

    Private _cmdCargarListaPedidos As DelegateCommand(Of Object)
    Public Property cmdCargarListaPedidos As DelegateCommand(Of Object)
        Get
            Return _cmdCargarListaPedidos
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarListaPedidos, value)
        End Set
    End Property
    Private Function CanCargarListaPedidos(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarListaPedidos(arg As Object)
        Try
            estaCargandoListaPedidos = True
            vendedor = Await configuracion.leerParametro("1", "Vendedor")
            listaPedidos = Await servicio.cargarListaPedidos(vendedor, verTodosLosVendedores)
        Finally
            estaCargandoListaPedidos = False
        End Try
    End Sub

    Private _cmdCargarPedido As DelegateCommand(Of Object)
    Public Property cmdCargarPedido As DelegateCommand(Of Object)
        Get
            Return _cmdCargarPedido
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarPedido, value)
        End Set
    End Property
    Private Function CanCargarPedido(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarPedido(arg As Object)
        Me.Titulo = "Pedido Venta (" + arg.numero.ToString + ")"
        pedido = Await servicio.cargarPedido(arg.empresa, arg.numero)
    End Sub

    Private _cmdCeldaModificada As DelegateCommand(Of Object)
    Public Property cmdCeldaModificada As DelegateCommand(Of Object)
        Get
            Return _cmdCeldaModificada
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCeldaModificada, value)
        End Set
    End Property
    Private Function CanCeldaModificada(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCeldaModificada(arg As Object)
        arg = CType(arg, DataGridCellEditEndingEventArgs)
        If arg.Column.Header = "Producto" AndAlso Not IsNothing(lineaActual) AndAlso arg.EditingElement.Text <> lineaActual.producto Then
            Dim lineaCambio As LineaPedidoVentaDTO = lineaActual 'para que se mantenga fija aunque cambie la linea actual durante el asíncrono
            Dim producto As Producto = Await servicio.cargarProducto(pedido.empresa, arg.EditingElement.Text)
            If Not IsNothing(producto) Then
                lineaCambio.precio = producto.precio
                lineaCambio.texto = producto.nombre
                lineaCambio.aplicarDescuento = producto.aplicarDescuento
                lineaCambio.descuento = 0 'habrá que calcularlo llamando a la API en una futura iteración
                If IsNothing(lineaCambio.usuario) Then
                    lineaCambio.usuario = System.Environment.UserDomainName + "\" + System.Environment.UserName
                End If
            End If
        End If
    End Sub

    Private _cmdModificarPedido As DelegateCommand(Of Object)
    Public Property cmdModificarPedido As DelegateCommand(Of Object)
        Get
            Return _cmdModificarPedido
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdModificarPedido, value)
        End Set
    End Property
    Private Function CanModificarPedido(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnModificarPedido(arg As Object)
        Try
            servicio.modificarPedido(pedido)
            NotificationRequest.Raise(New Notification() With {
                        .Title = "Pedido Modificado",
                        .Content = "Pedido " + pedido.numero.ToString + " modificado correctamente"
                    })
        Catch ex As Exception
            NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = ex.Message
                    })
        End Try
    End Sub

#End Region

End Class
