Imports System.Collections.ObjectModel
Imports System.Globalization
Imports CommunityToolkit.Mvvm.Input
Imports Nesto.Modulos.Inventario.InventarioModel
Imports CommunityToolkit.Mvvm.ComponentModel
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]

Public Class InventarioViewModel
    Inherits ObservableObject
    Private ReadOnly navegacion As IServicioNavegacion
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly dialogService As IServicioDialogos
    ' Nesto#596: las llamadas a api/Inventarios van por el servicio (el HttpClient con JWT lo crea su IClienteApiFactory).
    Private ReadOnly _servicio As IServicioInventario
    Private ReadOnly _cargaAlmacen As Task
    ' Nesto#596: almacén|día que ya sabemos rellenados (hay filas pendientes) y los que ya se preguntaron al abrir.
    Private ReadOnly _rellenados As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _preguntadosAlAbrir As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    Const EMPRESA_DEFECTO As String = "1"

    Public Sub New(navegacion As IServicioNavegacion, configuracion As IConfiguracion, dialogService As IServicioDialogos, servicio As IServicioInventario)
        Me.navegacion = navegacion
        Me.configuracion = configuracion
        Me.dialogService = dialogService
        _servicio = servicio

        cmdAbrirInventario = New RelayCommand(AddressOf OnAbrirInventario)
        cmdActualizarLineaInventario = New RelayCommand(Of InventarioDTO)(AddressOf OnActualizarLineaInventario)
        cmdActualizarMovimientos = New RelayCommand(AddressOf OnActualizarMovimientos)

        cmdCrearLineaInventario = New RelayCommand(Of InventarioDTO)(AddressOf OnCrearLineaInventario)
        cmdInsertarProducto = New RelayCommand(Of String)(AddressOf OnInsertarProducto)

        Titulo = "Inventario Tienda"

        fechaSeleccionada = Today
        cantidad = 1

        movimientosDia = New ObservableCollection(Of Movimiento)

        _cargaAlmacen = CargarAlmacenInventario()
    End Sub

    Private Async Function CargarAlmacenInventario() As Task
        almacen = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenInventario)
    End Function


#Region "Propiedades de Prism"
    Public Property Titulo As String

#End Region

#Region "Propiedades"
    Private _almacen As String
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

    Private _cmdAbrirInventario As RelayCommand
    Public Property cmdAbrirInventario As RelayCommand
        Get
            Return _cmdAbrirInventario
        End Get
        Private Set(value As RelayCommand)
            SetProperty(_cmdAbrirInventario, value)
        End Set
    End Property
    Private Sub OnAbrirInventario()
        navegacion.RequestNavigate("MainRegion", "InventarioView")
    End Sub

    Private _cmdActualizarMovimientos As RelayCommand
    Public Property cmdActualizarMovimientos As RelayCommand
        Get
            Return _cmdActualizarMovimientos
        End Get
        Private Set(value As RelayCommand)
            SetProperty(_cmdActualizarMovimientos, value)
        End Set
    End Property
    Private Async Sub OnActualizarMovimientos()
        estaOcupado = True
        Try
            movimientosTotal = New ObservableCollection(Of InventarioDTO)(Await _servicio.LeerLineas(EMPRESA_DEFECTO, almacen, fechaSeleccionada))
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaOcupado = False
        End Try
    End Sub

    Private _cmdActualizarLineaInventario As RelayCommand(Of InventarioDTO)
    Public Property cmdActualizarLineaInventario As RelayCommand(Of InventarioDTO)
        Get
            Return _cmdActualizarLineaInventario
        End Get
        Private Set(value As RelayCommand(Of InventarioDTO))
            SetProperty(_cmdActualizarLineaInventario, value)
        End Set
    End Property
    Private Function OnActualizarLineaInventario(linea As InventarioDTO) As Task(Of Movimiento)
        Return ActualizarLinea(linea, Nothing)
    End Function

    ''' <param name="porCodigoBarras">NestoAPI#788: cómo se ha leído esta vez (Nothing: no se sabe y la API conserva la marca).</param>
    Private Async Function ActualizarLinea(linea As InventarioDTO, porCodigoBarras As Boolean?) As Task(Of Movimiento)
        linea.Usuario = configuracion.usuario
        Try
            Await _servicio.ActualizarLinea(linea, porCodigoBarras)
            movimientoActual = New Movimiento With {
                .Cantidad = cantidad,
                .Descripcion = linea.Descripcion,
                .Familia = linea.Familia,
                .Grupo = linea.Grupo,
                .Subgrupo = linea.Subgrupo,
                .Producto = linea.Producto,
                .Lectura = TextoLectura(porCodigoBarras)
            }
            movimientosDia.Add(movimientoActual)
            numeroProducto = String.Empty
            cantidad = 1
            Return movimientoActual
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return Nothing
        End Try
    End Function

    Private _cmdInsertarProducto As RelayCommand(Of String)
    Public Property cmdInsertarProducto As RelayCommand(Of String)
        Get
            Return _cmdInsertarProducto
        End Get
        Private Set(value As RelayCommand(Of String))
            SetProperty(_cmdInsertarProducto, value)
        End Set
    End Property
    Private Async Function OnInsertarProducto(prod As String) As Task(Of Movimiento)
        ' Nesto#596: en la tienda no se cuenta nada sin haber rellenado antes el inventario con el stock actual.
        If Not Await AsegurarRellenado().ConfigureAwait(True) Then
            Return Nothing
        End If

        Dim leido As String = If(prod, String.Empty).Trim()
        Dim linea As InventarioDTO
        Try
            linea = Await _servicio.BuscarLinea(EMPRESA_DEFECTO, almacen, fechaSeleccionada, leido).ConfigureAwait(True)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return Nothing
        End Try

        Dim movimientoModificado As Movimiento

        If IsNothing(linea) Then
            ' La API decide si lo leído es la referencia o un código de barras (NestoAPI#788) y graba PorCodigoBarras.
            linea = New InventarioDTO With {
                .Empresa = EMPRESA_DEFECTO,
                .Almacen = almacen,
                .Fecha = fechaSeleccionada,
                .Producto = leido,
                .StockCalculado = 0,
                .StockReal = cantidad
            }
            movimientoModificado = Await OnCrearLineaInventario(linea) ' AWAIT
        Else
            linea.StockReal += cantidad
            movimientoModificado = Await ActualizarLinea(linea, EsLecturaPorCodigo(leido, linea.Producto)) ' AWAIT
        End If
        OnPropertyChanged(NameOf(movimientosDia))
        Return movimientoModificado
    End Function

    Private _cmdCrearLineaInventario As RelayCommand(Of InventarioDTO)
    Public Property cmdCrearLineaInventario As RelayCommand(Of InventarioDTO)
        Get
            Return _cmdCrearLineaInventario
        End Get
        Private Set(value As RelayCommand(Of InventarioDTO))
            SetProperty(_cmdCrearLineaInventario, value)
        End Set
    End Property
    Private Async Function OnCrearLineaInventario(linea As InventarioDTO) As Task(Of Movimiento)
        Try
            Dim grabada As InventarioDTO = Await _servicio.CrearLinea(linea)
            If grabada IsNot Nothing Then
                linea.Producto = grabada.Producto
                linea.Descripcion = grabada.Descripcion
                linea.Familia = grabada.Familia
                linea.PorCodigoBarras = grabada.PorCodigoBarras
            End If
            Dim nuevoMovimiento As New Movimiento With {
                .Cantidad = cantidad,
                .Descripcion = linea.Descripcion,
                .Familia = linea.Familia,
                .Grupo = linea.Grupo,
                .Subgrupo = linea.Subgrupo,
                .Producto = linea.Producto,
                .Lectura = TextoLectura(linea.PorCodigoBarras)
            }
            movimientosDia.Add(nuevoMovimiento)
            OnPropertyChanged(NameOf(movimientosDia))
            numeroProducto = String.Empty
            cantidad = 1
            Return nuevoMovimiento
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return Nothing
        End Try
    End Function


#End Region

#Region "Funciones Auxiliares"
    ''' <summary>Nesto#596: las tiendas (todo lo que no es Algete, que se inventaría por ubicaciones desde Ariadna).</summary>
    Public Shared Function EsAlmacenDeTienda(almacenInventario As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(almacenInventario) AndAlso
            Not String.Equals(almacenInventario.Trim(), Constantes.Almacenes.ALMACEN_ALGETE, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>NestoAPI#788: si lo leído no es la referencia que devuelve la API, se ha leído un código de barras.</summary>
    Public Shared Function EsLecturaPorCodigo(leido As String, referencia As String) As Boolean
        Return Not String.Equals(If(leido, String.Empty).Trim(), If(referencia, String.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function ClaveRelleno() As String
        Return If(almacen, String.Empty).Trim() + "|" + fechaSeleccionada.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>
    ''' Nesto#596: al abrir la pantalla (lo llama la vista al cargarse). Si el almacén es de tienda y no hay nada pendiente de
    ''' la fecha elegida, pregunta si se rellena. Una sola vez por almacén y día aunque la vista se cargue varias veces.
    ''' </summary>
    Public Async Function ComprobarRellenoAlAbrir() As Task
        If _cargaAlmacen IsNot Nothing Then
            Try
                Await _cargaAlmacen
            Catch
                ' Sin almacén no hay nada que comprobar.
            End Try
        End If
        Dim clave = ClaveRelleno()
        If Not EsAlmacenDeTienda(almacen) OrElse _preguntadosAlAbrir.Contains(clave) Then
            Return
        End If
        _preguntadosAlAbrir.Add(clave)
        Await AsegurarRellenado()
    End Function

    ''' <summary>
    ''' Nesto#596: True si se puede contar: no es una tienda, ya hay filas pendientes del día o se acaba de rellenar. Si no
    ''' hay nada, pregunta; con «No» (o si falla) devuelve False y no se graba nada.
    ''' </summary>
    Public Async Function AsegurarRellenado() As Task(Of Boolean)
        If Not EsAlmacenDeTienda(almacen) Then
            Return True
        End If
        Dim clave = ClaveRelleno()
        If _rellenados.Contains(clave) Then
            Return True
        End If
        Dim almacenTienda = almacen.Trim()

        Try
            Dim pendientes = Await _servicio.LeerPendientes(EMPRESA_DEFECTO, almacenTienda, fechaSeleccionada.Date)
            If pendientes IsNot Nothing AndAlso pendientes.Sum(Function(p) p.Filas) > 0 Then
                _rellenados.Add(clave)
                Return True
            End If
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return False
        End Try

        Dim mensaje = $"Antes de contar hay que rellenar el inventario con el stock actual de {almacenTienda}. ¿Rellenar ahora?"
        If Not dialogService.ShowConfirmationAnswer("Rellenar inventario", mensaje) Then
            Return False
        End If

        estaOcupado = True
        Try
            Dim respuesta = Await _servicio.Rellenar(EMPRESA_DEFECTO, almacenTienda, fechaSeleccionada.Date)
            _rellenados.Add(clave)
            Dim filas = If(respuesta Is Nothing, 0, respuesta.Filas)
            Dim unidades = If(respuesta Is Nothing, 0, respuesta.Unidades)
            dialogService.ShowNotification("Inventario rellenado", $"Inventario rellenado: {filas} productos, {unidades} unidades.")
            Return True
        Catch ex As InventarioYaRellenadoException
            ' Lo ha rellenado otro mientras tanto: se puede contar igual.
            _rellenados.Add(clave)
            dialogService.ShowNotification("Inventario", ex.Message)
            Return True
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return False
        Finally
            estaOcupado = False
        End Try
    End Function

    Public Async Function InsertarProducto(producto As String) As Task(Of Movimiento)
        Return Await OnInsertarProducto(producto)
    End Function

#End Region
End Class
