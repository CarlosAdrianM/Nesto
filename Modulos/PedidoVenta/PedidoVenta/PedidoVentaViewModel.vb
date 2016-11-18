Imports System.Collections.ObjectModel
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.Regions
Imports Nesto.Contratos
Imports Nesto.Models.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

Public Class PedidoVentaViewModel
    Inherits ViewModelBase

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
        cmdCargarListaPedidos = New DelegateCommand(Of Object)(AddressOf OnCargarListaPedidos, AddressOf CanCargarListaPedidos)
        cmdCargarPedido = New DelegateCommand(Of Object)(AddressOf OnCargarPedido, AddressOf CanCargarPedido)

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
    Private _estaCargandoListaPedidos As Boolean
    Public Property estaCargandoListaPedidos() As Boolean
        Get
            Return _estaCargandoListaPedidos
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaCargandoListaPedidos, value)
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


#End Region

End Class
