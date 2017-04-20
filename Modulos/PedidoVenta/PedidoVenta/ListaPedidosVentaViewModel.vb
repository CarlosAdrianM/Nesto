Imports System.Collections.ObjectModel
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Nesto.Contratos
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

Public Class ListaPedidosVentaViewModel
    Inherits ViewModelBase


    Public Property configuracion As IConfiguracion
    Private ReadOnly container As IUnityContainer
    Private ReadOnly servicio As IPedidoVentaService

    Private vendedor As String
    Private verTodosLosVendedores As Boolean = False

    Public Sub New(configuracion As IConfiguracion, servicio As IPedidoVentaService, container As IUnityContainer)
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.container = container

        cmdCargarListaPedidos = New DelegateCommand(Of Object)(AddressOf OnCargarListaPedidos, AddressOf CanCargarListaPedidos)
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
    Private _empresaSeleccionada As String = "1  "
    Public Property empresaSeleccionada As String
        Get
            Return _empresaSeleccionada
        End Get
        Set(value As String)
            SetProperty(_empresaSeleccionada, value)
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

    Private _filtroPedidos As String
    Public Property filtroPedidos As String
        Get
            Return _filtroPedidos
        End Get
        Set(value As String)
            SetProperty(_filtroPedidos, value)

            If filtroPedidos = "" Then
                listaPedidos = listaPedidosOriginal
            Else
                If Not IsNothing(listaPedidos) Then
                    listaPedidos = New ObservableCollection(Of ResumenPedido)(listaPedidos.Where(Function(p) (Not IsNothing(p.direccion) AndAlso p.direccion.ToLower.Contains(filtroPedidos.ToLower)) OrElse
                                                                                                 (Not IsNothing(p.nombre) AndAlso p.nombre.ToLower.Contains(filtroPedidos.ToLower)) OrElse
                                                                                                 (Not IsNothing(p.cliente) AndAlso p.cliente.Trim.ToLower.Equals(filtroPedidos.ToLower)) OrElse
                                                                                                 (p.numero = Me.convertirCadenaInteger(filtroPedidos))
                                                                                                 ))

                End If
                If Not IsNothing(listaPedidos) AndAlso listaPedidos.Count = 1 Then
                    resumenSeleccionado = listaPedidos.FirstOrDefault
                End If

                If (Not IsNothing(listaPedidos) AndAlso listaPedidos.Count = 0) OrElse estaCargandoListaPedidos Then
                    Dim nuevoResumen As ResumenPedido = New ResumenPedido With {
                        .empresa = empresaSeleccionada,
                        .numero = filtroPedidos
                    }
                    resumenSeleccionado = nuevoResumen
                End If


            End If

            'p.direccion.Contains(filtroPedidos) OrElse
            'p.nombre.Contains(filtroPedidos) OrElse
            'p.cliente.Contains(filtroPedidos)
            'OrElse p.numero = CInt(filtroPedidos)
            '   ))
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

    Private _listaPedidosOriginal As ObservableCollection(Of ResumenPedido)
    Public Property listaPedidosOriginal As ObservableCollection(Of ResumenPedido)
        Get
            Return _listaPedidosOriginal
        End Get
        Set(value As ObservableCollection(Of ResumenPedido))
            SetProperty(_listaPedidosOriginal, value)
        End Set
    End Property

    Private _resumenSeleccionado As ResumenPedido
    Public Property resumenSeleccionado() As ResumenPedido
        Get
            Return _resumenSeleccionado
        End Get
        Set(ByVal value As ResumenPedido)
            SetProperty(_resumenSeleccionado, value)
            Dim parameters As NavigationParameters = New NavigationParameters()
            parameters.Add("numeroPedidoParameter", resumenSeleccionado)
            scopedRegionManager.RequestNavigate("DetallePedidoRegion", "DetallePedidoView", parameters)
            empresaSeleccionada = resumenSeleccionado.empresa
        End Set
    End Property

    Private _scopedRegionManager As IRegionManager
    Public Property scopedRegionManager As IRegionManager
        Get
            Return _scopedRegionManager
        End Get
        Set(value As IRegionManager)
            SetProperty(_scopedRegionManager, value)
        End Set
    End Property

#End Region

#Region "Comandos"
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
            Dim empresaDefecto As String = Await configuracion.leerParametro("1", "EmpresaPorDefecto")
            Dim pedidoDefecto As String = Await configuracion.leerParametro(empresaDefecto, "UltNumPedidoVta")
            Dim nuevoResumen As ResumenPedido = New ResumenPedido With {
                .empresa = empresaDefecto,
                .numero = pedidoDefecto
            }
            resumenSeleccionado = nuevoResumen
            vendedor = Await configuracion.leerParametro(empresaDefecto, "Vendedor")
            listaPedidos = Await servicio.cargarListaPedidos(vendedor, verTodosLosVendedores)
            listaPedidosOriginal = listaPedidos
        Catch ex As Exception
            NotificationRequest.Raise(New Notification() With {
            .Title = "Error",
            .Content = ex.Message
        })
        Finally
            estaCargandoListaPedidos = False
        End Try
    End Sub

#End Region

#Region "Funciones auxiliares"
    Private Function convertirCadenaInteger(texto As String) As Integer
        Dim valor As Integer
        Return IIf(Integer.TryParse(texto, valor), valor, Nothing)
    End Function

#End Region
End Class
