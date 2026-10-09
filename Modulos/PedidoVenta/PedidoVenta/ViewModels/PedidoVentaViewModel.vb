Imports System.Net.Http
Imports System.Text
Imports CommunityToolkit.Mvvm.Input
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Prism.Ioc
Imports CommunityToolkit.Mvvm.ComponentModel
Imports Unity
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models

' Nesto#490 (4C.4, 7.º tramo): sin IRegionManager. Cada apertura (el módulo o CargarPedido) es una pestaña nueva con su
' propio ámbito de regiones (IServicioNavegacion.AbrirVistaConAmbito), que recibe la vista por IConAmbitoNavegacion. Fuera
' el scopedRegionManager que guardaba aquí y nadie leía.
Public Class PedidoVentaViewModel
    Inherits ObservableObject

    Private ReadOnly navegacion As IServicioNavegacion
    Private ReadOnly container As IUnityContainer
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly servicio As IPedidoVentaService

    Public Sub New(navegacion As IServicioNavegacion, configuracion As IConfiguracion, servicio As IPedidoVentaService, container As IUnityContainer)
        Me.navegacion = navegacion
        Me.configuracion = configuracion
        Me.container = container
        Me.servicio = servicio

        cmdAbrirModulo = New RelayCommand(Of Object)(AddressOf OnAbrirModulo, AddressOf CanAbrirModulo)

        Titulo = "Lista de Pedidos"
    End Sub

    Public Property empresaInicial As String
    Public Property pedidoInicial As Integer


    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            SetProperty(_titulo, value)
        End Set
    End Property

#Region "Comandos"
    Private _cmdAbrirModulo As RelayCommand(Of Object)
    Public Property cmdAbrirModulo As RelayCommand(Of Object)
        Get
            Return _cmdAbrirModulo
        End Get
        Private Set(value As RelayCommand(Of Object))
            SetProperty(_cmdAbrirModulo, value)
        End Set
    End Property
    Private Function CanAbrirModulo(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirModulo(arg As Object)
        Dim view = Me.container.Resolve(Of PedidoVentaView)
        If Not IsNothing(view) Then
            Dim unused = navegacion.AbrirVistaConAmbito("MainRegion", view)
        End If
    End Sub

#End Region

    Public Shared Sub CargarPedido(empresa As String, pedido As Integer, container As IUnityContainer)
        Dim view = container.Resolve(Of PedidoVentaView)
        Dim navegacion = container.Resolve(Of IServicioNavegacion)
        If Not IsNothing(view) Then
            ' El pedido inicial lo lee la vista al cargarse (Loaded), que llega después de activarla: da igual ponerlo antes
            view.DataContext.empresaInicial = empresa
            view.DataContext.pedidoInicial = pedido
            Dim unused = navegacion.AbrirVistaConAmbito("MainRegion", view)
        End If
    End Sub

    ' Nesto#378: el método Shared CrearPedidoAsync se ha eliminado porque llamaba a la API sin
    ' token JWT (errores sin usuario en ELMAH y 401 cuando se proteja el endpoint). Usar
    ' IPedidoVentaService.CrearPedido, que autentica y parsea los errores del API.

End Class
