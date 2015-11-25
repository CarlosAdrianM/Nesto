Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports System.Text
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.Mvvm
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Nesto.Contratos
Imports Nesto.Modulos.Inventario.InventarioModel
Imports Newtonsoft.Json

Public Class InventarioViewModel
    Inherits ViewModelBase
    'Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly configuracion As IConfiguracion

    Const EMPRESA_DEFECTO As String = "1"

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion)
        'Me.container = container
        Me.regionManager = regionManager
        Me.configuracion = configuracion

        cmdAbrirInventario = New DelegateCommand(Of Object)(AddressOf OnAbrirInventario, AddressOf CanAbrirInventario)
        cmdInsertarProducto = New DelegateCommand(Of Object)(AddressOf OnInsertarProducto, AddressOf CanInsertarProducto)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        Titulo = "Inventario Tienda"

        fechaSeleccionada = Today
        cantidad = 1

        movimientosDia = New ObservableCollection(Of InventarioCuadreDTO)
    End Sub

    Public Overrides Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean
        Return True
    End Function

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
    Private _cantidad As Integer
    Public Property cantidad As Integer
        Get
            Return _cantidad
        End Get
        Set(ByVal value As Integer)
            SetProperty(_cantidad, value)
        End Set
    End Property

    Private _estaCantidadActiva As Boolean
    Public Property estaCantidadActiva As Boolean
        Get
            Return _estaCantidadActiva
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaCantidadActiva, value)
        End Set
    End Property

    Private _fechaSeleccionada As Date
    Public Property fechaSeleccionada As Date
        Get
            Return _fechaSeleccionada
        End Get
        Set(ByVal value As Date)
            SetProperty(_fechaSeleccionada, value)
        End Set
    End Property

    Private _movimientosDia As ObservableCollection(Of InventarioCuadreDTO)
    Public Property movimientosDia As ObservableCollection(Of InventarioCuadreDTO)
        Get
            Return _movimientosDia
        End Get
        Set(ByVal value As ObservableCollection(Of InventarioCuadreDTO))
            SetProperty(_movimientosDia, value)
        End Set
    End Property

    Private _numeroProducto As String
    Public Property numeroProducto As String
        Get
            Return _numeroProducto
        End Get
        Set(ByVal value As String)
            SetProperty(_numeroProducto, value)
        End Set
    End Property

#End Region

#Region "Comandos"

    Private _cmdAbrirInventario As DelegateCommand(Of Object)
    Public Property cmdAbrirInventario As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirInventario
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirInventario, value)
        End Set
    End Property
    Private Function CanAbrirInventario(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirInventario(arg As Object)
        regionManager.RequestNavigate("MainRegion", "InventarioView")
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
    Private Async Sub OnInsertarProducto(arg As Object)
        Using client As New HttpClient
            'estaOcupado = True

            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim linea = New InventarioCuadreDTO With {
                .Empresa = EMPRESA_DEFECTO,
                .Producto = arg,
                .Fecha = fechaSeleccionada,
                .Cantidad = cantidad
            }


            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(linea), Encoding.UTF8, "application/json")

            Try

                response = Await client.PostAsync("InventarioCuadres", content)

                If Not response.IsSuccessStatusCode Then
                    Dim cadenaError As String = response.Content.ReadAsStringAsync().Result
                    Dim detallesError = JsonConvert.DeserializeObject(cadenaError)

                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = detallesError("ExceptionMessage")
                    })
                Else
                    numeroProducto = ""
                    cantidad = 1
                    movimientosDia.Add(linea)
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


#End Region
End Class
