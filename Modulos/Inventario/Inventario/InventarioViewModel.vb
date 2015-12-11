Imports System.Collections.ObjectModel
Imports System.Globalization
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
        cmdActualizarLineaInventario = New DelegateCommand(Of Object)(AddressOf OnActualizarLineaInventario, AddressOf CanActualizarLineaInventario)
        cmdActualizarMovimientos = New DelegateCommand(Of Object)(AddressOf OnActualizarMovimientos, AddressOf CanActualizarMovimientos)
        cmdCrearLineaInventario = New DelegateCommand(Of Object)(AddressOf OnCrearLineaInventario, AddressOf CanCrearLineaInventario)
        cmdInsertarProducto = New DelegateCommand(Of Object)(AddressOf OnInsertarProducto, AddressOf CanInsertarProducto)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        Titulo = "Inventario Tienda"

        fechaSeleccionada = Today
        cantidad = 1

        movimientosDia = New ObservableCollection(Of Movimiento)
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
    Private _almacen As String = "REI"
    Public Property almacen As String
        Get
            Return _almacen
        End Get
        Set(ByVal value As String)
            SetProperty(_almacen, value)
        End Set
    End Property

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

    Private _estaOcupado As Boolean = False
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaOcupado, value)
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

    Private _movimientoActual As Movimiento
    Public Property movimientoActual As Movimiento
        Get
            Return _movimientoActual
        End Get
        Set(ByVal value As Movimiento)
            SetProperty(_movimientoActual, value)
        End Set
    End Property

    Private _movimientosDia As ObservableCollection(Of Movimiento)
    Public Property movimientosDia As ObservableCollection(Of Movimiento)
        Get
            Return _movimientosDia
        End Get
        Set(ByVal value As ObservableCollection(Of Movimiento))
            SetProperty(_movimientosDia, value)
        End Set
    End Property

    Private _movimientosTotal As ObservableCollection(Of InventarioDTO)
    Public Property movimientosTotal As ObservableCollection(Of InventarioDTO)
        Get
            Return _movimientosTotal
        End Get
        Set(ByVal value As ObservableCollection(Of InventarioDTO))
            SetProperty(_movimientosTotal, value)
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

    Private _cmdActualizarMovimientos As DelegateCommand(Of Object)
    Public Property cmdActualizarMovimientos As DelegateCommand(Of Object)
        Get
            Return _cmdActualizarMovimientos
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdActualizarMovimientos, value)
        End Set
    End Property
    Private Function CanActualizarMovimientos(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnActualizarMovimientos(arg As Object)
        Using client As New HttpClient
            estaOcupado = True

            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            'Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(linea), Encoding.UTF8, "application/json")

            Try
                Dim culture As New CultureInfo("en-US")
                Dim cadenaGet As String = "Inventarios?empresa=" + EMPRESA_DEFECTO + "&almacen=" + almacen + "&fecha=" + fechaSeleccionada.ToString("d", culture)
                response = Await client.GetAsync(cadenaGet)

                If Not response.IsSuccessStatusCode Then
                    Dim cadenaError As String = response.Content.ReadAsStringAsync().Result
                    Dim detallesError = JsonConvert.DeserializeObject(cadenaError)

                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = detallesError("ExceptionMessage")
                    })
                Else
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    movimientosTotal = JsonConvert.DeserializeObject(Of ObservableCollection(Of InventarioDTO))(cadenaJson)
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = ex.Message
                })
            Finally
                estaOcupado = False
            End Try
            'OnPropertyChanged("movimientosDia")

        End Using

    End Sub

    Private _cmdActualizarLineaInventario As DelegateCommand(Of Object)
    Public Property cmdActualizarLineaInventario As DelegateCommand(Of Object)
        Get
            Return _cmdActualizarLineaInventario
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdActualizarLineaInventario, value)
        End Set
    End Property
    Private Function CanActualizarLineaInventario(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnActualizarLineaInventario(arg As Object)
        Using client As New HttpClient
            'estaOcupado = True

            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim linea As InventarioDTO = arg

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(linea), Encoding.UTF8, "application/json")

            Try
                response = Await client.PutAsync("Inventarios/" + linea.NumOrden.ToString, content)

                If Not response.IsSuccessStatusCode Then
                    Dim cadenaError As String = response.Content.ReadAsStringAsync().Result
                    Dim detallesError = JsonConvert.DeserializeObject(cadenaError)

                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = detallesError("ExceptionMessage")
                    })
                Else
                    movimientoActual = New Movimiento With {
                        .Cantidad = cantidad,
                        .Descripcion = linea.Descripcion,
                        .Familia = linea.Familia,
                        .Grupo = linea.Grupo,
                        .Subgrupo = linea.Subgrupo,
                        .Producto = linea.Producto
                    }
                    movimientosDia.Add(movimientoActual)
                    numeroProducto = ""
                    cantidad = 1
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

        Dim linea As InventarioDTO = Await buscarInventario(EMPRESA_DEFECTO, almacen, fechaSeleccionada, arg)

        If IsNothing(linea) Then
            linea = New InventarioDTO With {
                .Empresa = EMPRESA_DEFECTO,
                .Almacen = almacen,
                .Fecha = fechaSeleccionada,
                .Producto = numeroProducto,
                .StockCalculado = 0,
                .StockReal = cantidad
            }
            Await cmdCrearLineaInventario.Execute(linea)
        Else
            linea.StockReal += cantidad
            Await cmdActualizarLineaInventario.Execute(linea)
        End If
        OnPropertyChanged("movimientosDia")
    End Sub

    Private _cmdCrearLineaInventario As DelegateCommand(Of Object)
    Public Property cmdCrearLineaInventario As DelegateCommand(Of Object)
        Get
            Return _cmdCrearLineaInventario
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCrearLineaInventario, value)
        End Set
    End Property
    Private Function CanCrearLineaInventario(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCrearLineaInventario(arg As Object)
        Using client As New HttpClient
            'estaOcupado = True

            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim linea As InventarioDTO = arg

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(linea), Encoding.UTF8, "application/json")

            Try
                response = Await client.PostAsync("Inventarios", content)

                Dim cadenaError As String = response.Content.ReadAsStringAsync().Result
                Dim detallesError = JsonConvert.DeserializeObject(cadenaError)

                If Not response.IsSuccessStatusCode Then
                    NotificationRequest.Raise(New Notification() With {
                        .Title = "Error",
                        .Content = detallesError("ExceptionMessage")
                    })
                Else
                    linea.Producto = detallesError("Número")
                    linea.Descripcion = detallesError("Descripción")
                    linea.Familia = detallesError("Familia")
                    movimientosDia.Add(New Movimiento With {
                        .Cantidad = cantidad,
                        .Descripcion = linea.Descripcion,
                        .Familia = linea.Familia,
                        .Grupo = linea.Grupo,
                        .Subgrupo = linea.Subgrupo,
                        .Producto = linea.Producto
                    })
                    numeroProducto = ""
                    cantidad = 1
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

#Region "Funciones Auxiliares"
    Private Async Function buscarInventario(empresa As String, almacen As String, fecha As DateTime, producto As String) As Task(Of InventarioDTO)
        Dim linea As InventarioDTO = Nothing
        Using client As New HttpClient
            'estaOcupado = True

            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            'Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(linea), Encoding.UTF8, "application/json")

            Try
                Dim culture As New CultureInfo("en-US")
                Dim cadenaGet As String = "Inventarios?empresa=" + EMPRESA_DEFECTO + "&almacen=" + almacen + "&fecha=" + fechaSeleccionada.ToString("d", culture) + "&producto=" + producto
                response = Await client.GetAsync(cadenaGet)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    linea = JsonConvert.DeserializeObject(Of InventarioDTO)(cadenaJson)
                    'Else
                    '    Dim cadenaError As String = response.Content.ReadAsStringAsync().Result
                    '    Dim detallesError = JsonConvert.DeserializeObject(cadenaError)

                    '    NotificationRequest.Raise(New Notification() With {
                    '        .Title = "Error",
                    '        .Content = detallesError("ExceptionMessage")
                    '    })
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

        Return linea

    End Function

#End Region
End Class
