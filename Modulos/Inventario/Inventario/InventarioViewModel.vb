Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.Net.Http
Imports System.Text
Imports Prism.Commands
Imports Prism.Regions
Imports Nesto.Contratos
Imports Nesto.Modulos.Inventario.InventarioModel
Imports Newtonsoft.Json
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs

Public Class InventarioViewModel
    Inherits BindableBase
    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly dialogService As IDialogService

    Const EMPRESA_DEFECTO As String = "1"

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, dialogService As IDialogService)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.dialogService = dialogService

        cmdAbrirInventario = New DelegateCommand(Of Object)(AddressOf OnAbrirInventario, AddressOf CanAbrirInventario)
        cmdActualizarLineaInventario = New DelegateCommand(Of Object)(AddressOf OnActualizarLineaInventario, AddressOf CanActualizarLineaInventario)
        cmdActualizarMovimientos = New DelegateCommand(Of Object)(AddressOf OnActualizarMovimientos, AddressOf CanActualizarMovimientos)
        cmdCrearLineaInventario = New DelegateCommand(Of InventarioDTO)(AddressOf OnCrearLineaInventario, AddressOf CanCrearLineaInventario)
        cmdInsertarProducto = New DelegateCommand(Of Object)(AddressOf OnInsertarProducto, AddressOf CanInsertarProducto)

        Titulo = "Inventario Tienda"

        fechaSeleccionada = Today
        cantidad = 1

        movimientosDia = New ObservableCollection(Of Movimiento)
    End Sub


#Region "Propiedades de Prism"
    Public Property Titulo As String

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
                    dialogService.ShowError(detallesError("ExceptionMessage"))
                Else
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    movimientosTotal = JsonConvert.DeserializeObject(Of ObservableCollection(Of InventarioDTO))(cadenaJson)
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

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
                    dialogService.ShowError(detallesError("ExceptionMessage"))
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
                    numeroProducto = String.Empty
                    cantidad = 1
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
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
            cmdCrearLineaInventario.Execute(linea) ' AWAIT
        Else
            linea.StockReal += cantidad
            cmdActualizarLineaInventario.Execute(linea) ' AWAIT
        End If
        RaisePropertyChanged(NameOf(movimientosDia))
    End Sub

    Private _cmdCrearLineaInventario As DelegateCommand(Of InventarioDTO)
    Public Property cmdCrearLineaInventario As DelegateCommand(Of InventarioDTO)
        Get
            Return _cmdCrearLineaInventario
        End Get
        Private Set(value As DelegateCommand(Of InventarioDTO))
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
                    dialogService.ShowError(detallesError("ExceptionMessage"))
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
                    numeroProducto = String.Empty
                    cantidad = 1
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
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
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                'estaOcupado = False
            End Try

        End Using

        Return linea

    End Function

#End Region
End Class
