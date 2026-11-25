Imports System.Collections.ObjectModel
Imports System.Globalization
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
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
        ' Nesto#597: contabilizar y ver las diferencias (NestoAPI#790).
        cmdContabilizarInventario = New AsyncRelayCommand(AddressOf ContabilizarInventario)
        cmdVerDiferencias = New RelayCommand(AddressOf OnVerDiferencias)
        ' Sugerencia 1185: dejar el producto a cero en todas sus ubicaciones (POST api/Inventarios/DejarACero).
        cmdDejarACero = New AsyncRelayCommand(AddressOf DejarACeroEnUbicaciones)
        ' 25/11/26 (Paloma rellenó sin querer 3.032 filas de ALC): deshacer el rellenado de la tienda ese día.
        cmdDeshacerRellenado = New AsyncRelayCommand(AddressOf DeshacerRellenado)
        ' Novedad 1456 (Paloma): «Borrar» en la lista de las filas que la API marca como borrables.
        cmdBorrarFila = New AsyncRelayCommand(Of InventarioDTO)(AddressOf BorrarFila, AddressOf EsFilaBorrable)

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
            If SetProperty(_almacen, value) Then
                OnPropertyChanged(NameOf(AlmacenConUbicaciones))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Algete (12/11/26): el stock va por huecos, así que se cuenta EN un hueco. Una fila sin ubicación no se puede contabilizar
    ''' («Productos que se ubican contados sin ubicación») y, si se queda de un día para otro, para el picking. Las tiendas, no.
    ''' </summary>
    Public ReadOnly Property AlmacenConUbicaciones As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(almacen) AndAlso Not EsAlmacenDeTienda(almacen)
        End Get
    End Property

    ' El hueco donde se cuenta: se queda puesto de un producto al siguiente (se cuenta hueco a hueco) hasta que se cambia.
    Private _pasillo As String
    Public Property Pasillo As String
        Get
            Return _pasillo
        End Get
        Set(ByVal value As String)
            SetProperty(_pasillo, value)
        End Set
    End Property

    Private _fila As String
    Public Property Fila As String
        Get
            Return _fila
        End Get
        Set(ByVal value As String)
            SetProperty(_fila, value)
        End Set
    End Property

    Private _columna As String
    Public Property Columna As String
        Get
            Return _columna
        End Get
        Set(ByVal value As String)
            SetProperty(_columna, value)
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
                .Lectura = TextoLectura(porCodigoBarras),
                .Hueco = TextoHueco(linea.Pasillo, linea.Fila, linea.Columna)
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
    ''' <summary>Tope de unidades en una sola lectura: por encima es casi seguro una referencia o un código leídos en «Cantidad».</summary>
    Public Const CANTIDAD_MAXIMA_LECTURA As Integer = 9999

    ''' <summary>LA REGLA (pura): por qué no vale esa cantidad en una lectura del inventario, o Nothing si vale (también negativas, para corregir).</summary>
    Public Shared Function MotivoCantidadNoValida(cantidad As Integer) As String
        If Math.Abs(CLng(cantidad)) > CANTIDAD_MAXIMA_LECTURA Then
            Return $"La cantidad {cantidad} no es válida: parece una referencia o un código de barras leído en «Cantidad». Corrija la cantidad y vuelva a leer el producto."
        End If
        Return Nothing
    End Function

    Private Async Function OnInsertarProducto(prod As String) As Task(Of Movimiento)
        ' Nesto#596: en la tienda no se cuenta nada sin haber rellenado antes el inventario con el stock actual.
        If Not Await AsegurarRellenado().ConfigureAwait(True) Then
            Return Nothing
        End If

        ' Algete: sin hueco no se manda nada (una fila sin ubicación no se puede contabilizar y para el picking).
        Dim porHuecos = AlmacenConUbicaciones
        If porHuecos AndAlso Not PrepararHueco() Then
            dialogService.ShowError(MensajeFaltaHueco(almacen))
            Return Nothing
        End If

        ' 20/11/26 (Reina, víspera del inventario): una referencia o un código de barras leídos en «Cantidad» (45929 unidades) desbordaba
        ' sin avisar y la pantalla se quedaba sin contar nada. Una cantidad así no se manda: se dice y se corrige.
        Dim motivoCantidad = MotivoCantidadNoValida(cantidad)
        If motivoCantidad IsNot Nothing Then
            dialogService.ShowError(motivoCantidad)
            Return Nothing
        End If

        Dim leido As String = If(prod, String.Empty).Trim()
        Dim linea As InventarioDTO
        Try
            If porHuecos Then
                linea = Await _servicio.BuscarLineaEnHueco(EMPRESA_DEFECTO, almacen, fechaSeleccionada, leido, Pasillo, Fila, Columna).ConfigureAwait(True)
            Else
                linea = Await _servicio.BuscarLinea(EMPRESA_DEFECTO, almacen, fechaSeleccionada, leido).ConfigureAwait(True)
            End If
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
            If porHuecos Then
                linea.Pasillo = Pasillo
                linea.Fila = Fila
                linea.Columna = Columna
            End If
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
        ' NestoAPI#784: si mientras tanto ya tiene fila (409 YaTieneFila), lo contado se suma a esa (una sola vez, sin bucles).
        Dim yaTieneFila As ConflictoInventarioException = Nothing
        Try
            Return Await GrabarLineaNueva(linea)
        Catch ex As ConflictoInventarioException When ex.EsCodigo(CodigosConflictoInventario.YA_TIENE_FILA)
            yaTieneFila = ex
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return Nothing
        End Try

        Dim leido = If(linea.Producto, String.Empty).Trim()
        Dim existente As InventarioDTO
        Try
            If String.IsNullOrWhiteSpace(linea.Pasillo) Then
                existente = Await _servicio.BuscarLinea(EMPRESA_DEFECTO, linea.Almacen, linea.Fecha, leido)
            Else
                existente = Await _servicio.BuscarLineaEnHueco(EMPRESA_DEFECTO, linea.Almacen, linea.Fecha, leido, linea.Pasillo, linea.Fila, linea.Columna)
            End If
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return Nothing
        End Try
        If existente Is Nothing Then
            dialogService.ShowError(yaTieneFila.Message)
            Return Nothing
        End If
        existente.StockReal += cantidad
        Return Await ActualizarLinea(existente, EsLecturaPorCodigo(leido, existente.Producto))
    End Function

    ''' <summary>POST de la fila nueva; los errores los trata quien llama.</summary>
    Private Async Function GrabarLineaNueva(linea As InventarioDTO) As Task(Of Movimiento)
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
            .Lectura = TextoLectura(linea.PorCodigoBarras),
            .Hueco = TextoHueco(linea.Pasillo, linea.Fila, linea.Columna)
        }
        movimientosDia.Add(nuevoMovimiento)
        OnPropertyChanged(NameOf(movimientosDia))
        numeroProducto = String.Empty
        cantidad = 1
        Return nuevoMovimiento
    End Function


#End Region

#Region "Contabilizar (Nesto#597)"
    ''' <summary>
    ''' Nesto#597: Tiendas (la suya), Administración, Dirección e Informática; la API lo vuelve a comprobar. Sugerencia 1185: y
    ''' Almacén, que según la API (NestoAPI#784, Carlos 04/11/26) contabiliza el de Algete (en otro almacén la API contesta 403).
    ''' </summary>
    Public ReadOnly Property PuedeContabilizar As Boolean
        Get
            Return EnAlgunGrupo(Constantes.GruposSeguridad.TIENDAS, Constantes.GruposSeguridad.ADMINISTRACION,
                                Constantes.GruposSeguridad.DIRECCION, Constantes.GruposSeguridad.INFORMATICA,
                                Constantes.GruposSeguridad.ALMACEN)
        End Get
    End Property

    ''' <summary>Nesto#597: además, Compras y Almacén pueden ver las diferencias (a coste).</summary>
    Public ReadOnly Property PuedeVerDiferencias As Boolean
        Get
            Return PuedeContabilizar OrElse EnAlgunGrupo(Constantes.GruposSeguridad.COMPRAS, Constantes.GruposSeguridad.ALMACEN)
        End Get
    End Property

    Private Function EnAlgunGrupo(ParamArray grupos As String()) As Boolean
        Try
            Return grupos.Any(Function(g) configuracion.UsuarioEnGrupo(g))
        Catch
            Return False
        End Try
    End Function

    Private _cmdContabilizarInventario As AsyncRelayCommand
    Public Property cmdContabilizarInventario As AsyncRelayCommand
        Get
            Return _cmdContabilizarInventario
        End Get
        Private Set(value As AsyncRelayCommand)
            SetProperty(_cmdContabilizarInventario, value)
        End Set
    End Property

    Private _cmdVerDiferencias As RelayCommand
    Public Property cmdVerDiferencias As RelayCommand
        Get
            Return _cmdVerDiferencias
        End Get
        Private Set(value As RelayCommand)
            SetProperty(_cmdVerDiferencias, value)
        End Set
    End Property

    Private Sub OnVerDiferencias()
        AbrirDiferencias(If(almacen, String.Empty).Trim(), fechaSeleccionada.Date, Nothing)
    End Sub

    Private Sub AbrirDiferencias(almacenInventario As String, fecha As Date, traspasos As List(Of Integer))
        Dim parametros As New ParametrosNavegacion From {
            {DiferenciasInventarioViewModel.PARAMETRO_ALMACEN, almacenInventario}
        }
        If traspasos IsNot Nothing AndAlso traspasos.Any() Then
            parametros.Add(DiferenciasInventarioViewModel.PARAMETRO_TRASPASOS, traspasos)
        Else
            parametros.Add(DiferenciasInventarioViewModel.PARAMETRO_FECHA, fecha)
        End If
        navegacion.RequestNavigate("MainRegion", DiferenciasInventarioViewModel.NOMBRE_VISTA, parametros)
    End Sub

    ''' <summary>
    ''' Nesto#597 (NestoAPI#790): comprueba, enseña bloqueantes y avisos, confirma, contabiliza (un traspaso por usuario) y abre
    ''' las diferencias de los traspasos. Con bloqueantes no se contabiliza; si la API los encuentra al contabilizar (409), se
    ''' vuelven a enseñar.
    ''' </summary>
    Public Async Function ContabilizarInventario() As Task
        If Not PuedeContabilizar Then
            Return
        End If
        If _cargaAlmacen IsNot Nothing Then
            Try
                Await _cargaAlmacen
            Catch
                ' Se avisa abajo si no hay almacén.
            End Try
        End If
        If String.IsNullOrWhiteSpace(almacen) Then
            dialogService.ShowError("Indique el almacén del inventario.")
            Return
        End If
        Dim almacenInventario = almacen.Trim()
        Dim fecha = fechaSeleccionada.Date
        Dim textoDia = fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)

        Dim comprobaciones As ComprobacionesInventarioDTO
        estaOcupado = True
        Try
            comprobaciones = Await _servicio.Comprobaciones(EMPRESA_DEFECTO, almacenInventario, fecha)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return
        Finally
            estaOcupado = False
        End Try
        If comprobaciones Is Nothing Then
            Return
        End If

        ' NestoAPI#784: RecontadaSinContar bloquea, pero el usuario puede confirmar que de verdad hay 0 (ConfirmarContadoCero).
        Dim confirmarContadoCero As Boolean = False
        If HayComprobaciones(comprobaciones) Then
            Dim vistas = Await MostrarComprobaciones(comprobaciones)
            If vistas.Decision = DecisionComprobaciones.NoSeguir Then
                Return
            End If
            confirmarContadoCero = vistas.Decision = DecisionComprobaciones.SeguirConfirmandoContadoCero
            ' Incidencia 1186: si se han borrado filas en la ventana, el recuento es el de las comprobaciones vueltas a pedir.
            comprobaciones = vistas.Comprobaciones
        End If

        Dim recuento = If(comprobaciones.Recuento, New List(Of RecuentoUsuarioInventarioDTO))
        Dim filas = recuento.Sum(Function(r) r.Filas)
        If filas = 0 Then
            dialogService.ShowNotification("Contabilizar inventario", $"No hay filas pendientes de contabilizar en {almacenInventario} el {textoDia}.")
            Return
        End If
        ' Novedad 1364: si en la ventana se ha pulsado «Contabilizar también la del dd/MM», las comprobaciones lo dicen.
        Dim incluirDiasAnteriores = comprobaciones.IncluyeDiasAnteriores
        Dim mensaje = $"Se va a contabilizar el inventario de {almacenInventario} del {textoDia}: {TextoFilas(filas)} de {TextoUsuarios(recuento.Count)}."
        If incluirDiasAnteriores AndAlso comprobaciones.DiasAnterioresIncluidos IsNot Nothing AndAlso comprobaciones.DiasAnterioresIncluidos.Any() Then
            mensaje += $" Incluye las filas pendientes del {InventarioModel.TextoDias(comprobaciones.DiasAnterioresIncluidos)}."
        End If
        If confirmarContadoCero Then
            mensaje += " Ha confirmado que hay 0 en los productos vueltos a contar: su stock se quedará a cero."
        End If
        mensaje += " ¿Continuar?"
        If Not dialogService.ShowConfirmationAnswer("Contabilizar inventario", mensaje, True) Then
            Return
        End If

        Dim resultado = Await LlamarContabilizar(almacenInventario, fecha, confirmarContadoCero, incluirDiasAnteriores)
        If resultado Is Nothing Then
            Return
        End If
        If resultado.Bloqueado IsNot Nothing Then
            ' Algo ha cambiado desde las comprobaciones: no se ha tocado nada. Si solo quedan bloqueantes que se confirman y
            ' ahora se confirman (antes no), se intenta una vez más; si no, se enseñan y se para.
            Dim decision = (Await MostrarComprobaciones(resultado.Bloqueado)).Decision
            If confirmarContadoCero OrElse decision <> DecisionComprobaciones.SeguirConfirmandoContadoCero Then
                Return
            End If
            resultado = Await LlamarContabilizar(almacenInventario, fecha, True, incluirDiasAnteriores)
            If resultado Is Nothing Then
                Return
            End If
            If resultado.Bloqueado IsNot Nothing Then
                Await MostrarComprobaciones(resultado.Bloqueado)
                Return
            End If
        End If

        Dim contabilizacion = If(resultado.Contabilizacion, New ContabilizacionInventarioDTO)
        dialogService.ShowNotification("Inventario contabilizado", TextoContabilizacion(contabilizacion))
        Dim traspasos = If(contabilizacion.Contabilizados, New List(Of UsuarioContabilizadoInventarioDTO)).
            Where(Function(c) c.Traspaso.HasValue).Select(Function(c) c.Traspaso.Value).Distinct().ToList()
        If traspasos.Any() Then
            AbrirDiferencias(almacenInventario, fecha, traspasos)
        End If
    End Function

    Private Shared Function HayComprobaciones(comprobaciones As ComprobacionesInventarioDTO) As Boolean
        Return (comprobaciones.Bloqueantes IsNot Nothing AndAlso comprobaciones.Bloqueantes.Any()) OrElse
            (comprobaciones.Avisos IsNot Nothing AndAlso comprobaciones.Avisos.Any())
    End Function

    ''' <summary>Nothing si ha fallado (ya se ha enseñado el error).</summary>
    Private Async Function LlamarContabilizar(almacenInventario As String, fecha As Date, confirmarContadoCero As Boolean, incluirDiasAnteriores As Boolean) As Task(Of ResultadoContabilizarInventario)
        estaOcupado = True
        Try
            Return Await _servicio.Contabilizar(EMPRESA_DEFECTO, almacenInventario, fecha, confirmarContadoCero, incluirDiasAnteriores)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return Nothing
        Finally
            estaOcupado = False
        End Try
    End Function

    Private Enum DecisionComprobaciones
        NoSeguir
        Seguir
        ''' <summary>NestoAPI#784: solo había bloqueantes que se confirman y el usuario ha marcado «He contado y de verdad hay 0».</summary>
        SeguirConfirmandoContadoCero
    End Enum

    ''' <summary>
    ''' Se sigue si el usuario ha pulsado «Continuar» y no hay bloqueantes o, NestoAPI#784, si todos los bloqueantes se
    ''' confirman (SeConfirma) y ha marcado «He contado y de verdad hay 0». Un bloqueante que no se confirma no se salta nunca.
    ''' </summary>
    ''' <remarks>
    ''' Incidencia 1186: en la ventana se pueden borrar filas y se vuelven a pedir las comprobaciones; el OK trae las que se ven y
    ''' la decisión se toma con esas (si ya no queda ningún bloqueante, se contabiliza sin cerrar y volver a abrir).
    ''' </remarks>
    Private Async Function MostrarComprobaciones(comprobaciones As ComprobacionesInventarioDTO) As Task(Of (Decision As DecisionComprobaciones, Comprobaciones As ComprobacionesInventarioDTO))
        Dim parametros As New ParametrosDialogo From {
            {ComprobacionesInventarioDialogViewModel.PARAMETRO_COMPROBACIONES, comprobaciones},
            {ComprobacionesInventarioDialogViewModel.PARAMETRO_EMPRESA, EMPRESA_DEFECTO}
        }
        Dim respuesta = Await dialogService.ShowDialogAsync(ComprobacionesInventarioDialogViewModel.NOMBRE, parametros)
        If respuesta Is Nothing OrElse respuesta.Result <> ResultadoBoton.OK Then
            Return (DecisionComprobaciones.NoSeguir, comprobaciones)
        End If
        Dim vistas = If(respuesta.Parameters?.GetValue(Of ComprobacionesInventarioDTO)(ComprobacionesInventarioDialogViewModel.PARAMETRO_COMPROBACIONES), comprobaciones)
        Dim bloqueantes = If(vistas.Bloqueantes, New List(Of ComprobacionInventarioDTO))
        If Not bloqueantes.Any() Then
            Return (DecisionComprobaciones.Seguir, vistas)
        End If
        If bloqueantes.Any(Function(b) Not b.SeConfirma) Then
            Return (DecisionComprobaciones.NoSeguir, vistas)
        End If
        Dim confirmado = respuesta.Parameters IsNot Nothing AndAlso
            respuesta.Parameters.GetValue(Of Boolean)(ComprobacionesInventarioDialogViewModel.PARAMETRO_CONFIRMAR_CONTADO_CERO)
        Return (If(confirmado, DecisionComprobaciones.SeguirConfirmandoContadoCero, DecisionComprobaciones.NoSeguir), vistas)
    End Function

    ''' <summary>«Contabilizado: • Paloma: 64 filas, traspaso 77263 …» y, si los hay, los usuarios que han fallado.</summary>
    Public Shared Function TextoContabilizacion(contabilizacion As ContabilizacionInventarioDTO) As String
        Dim lineas As New List(Of String)
        Dim contabilizados = If(contabilizacion?.Contabilizados, New List(Of UsuarioContabilizadoInventarioDTO))
        Dim errores = If(contabilizacion?.Errores, New List(Of ErrorContabilizarInventarioDTO))
        If contabilizados.Any() Then
            lineas.Add("Contabilizado:")
            For Each c In contabilizados
                Dim traspaso = If(c.Traspaso.HasValue, $", traspaso {c.Traspaso.Value}", String.Empty)
                lineas.Add($"  • {c.Usuario?.Trim()}: {TextoFilas(c.Filas)}{traspaso}")
            Next
        End If
        If errores.Any() Then
            If lineas.Any() Then
                lineas.Add(String.Empty)
            End If
            lineas.Add("No se ha podido contabilizar:")
            For Each fallo In errores
                lineas.Add($"  • {fallo.Usuario?.Trim()}: {fallo.Mensaje}")
            Next
        End If
        Return String.Join(Environment.NewLine, lineas)
    End Function
#End Region

#Region "Dejar a cero en todas sus ubicaciones (Sugerencia 1185)"
    Public Const TITULO_DEJAR_A_CERO As String = "Dejar a cero en todas sus ubicaciones"

    ''' <summary>Los que cuentan el inventario (la API lo vuelve a comprobar): Almacén, Tiendas (la suya), Administración, Dirección e Informática.</summary>
    Public ReadOnly Property PuedeDejarACero As Boolean
        Get
            Return EnAlgunGrupo(Constantes.GruposSeguridad.ALMACEN, Constantes.GruposSeguridad.TIENDAS, Constantes.GruposSeguridad.ADMINISTRACION,
                                Constantes.GruposSeguridad.DIRECCION, Constantes.GruposSeguridad.INFORMATICA)
        End Get
    End Property

    Private _cmdDejarACero As AsyncRelayCommand
    Public Property cmdDejarACero As AsyncRelayCommand
        Get
            Return _cmdDejarACero
        End Get
        Private Set(value As AsyncRelayCommand)
            SetProperty(_cmdDejarACero, value)
        End Set
    End Property

    ''' <summary>
    ''' Sugerencia 1185 (Alfredo): el producto de la casilla «Producto» a cero en todas sus ubicaciones del almacén, hoy. Primero
    ''' pregunta a la API lo que haría (Simular) y lo enseña para confirmar; los 409 que se pueden resolver se preguntan antes
    ''' (ya contabilizado hoy → «Contar otra vez»; filas sin ubicación → borrarlas). No contabiliza: eso es «Contabilizar inventario».
    ''' </summary>
    Public Async Function DejarACeroEnUbicaciones() As Task
        If Not PuedeDejarACero Then
            Return
        End If
        If _cargaAlmacen IsNot Nothing Then
            Try
                Await _cargaAlmacen
            Catch
                ' Se avisa abajo si no hay almacén.
            End Try
        End If
        If String.IsNullOrWhiteSpace(almacen) Then
            dialogService.ShowError("Indique el almacén del inventario.")
            Return
        End If
        If String.IsNullOrWhiteSpace(numeroProducto) Then
            dialogService.ShowError("Indique en «Producto» la referencia (o lea su código de barras) que quiere dejar a cero.")
            Return
        End If
        Dim almacenInventario = almacen.Trim()
        Dim producto = numeroProducto.Trim()
        Dim otraVez As Boolean = False
        Dim quitarSinUbicacion As Boolean = False

        Dim plan As DejarACeroInventarioRespuestaDTO = Nothing
        ' Como mucho, un «Contar otra vez» y un «borrar las filas sin ubicación»: tres vueltas bastan.
        For vuelta = 1 To 3
            Dim conflicto As ConflictoInventarioException = Nothing
            plan = Nothing
            estaOcupado = True
            Try
                plan = Await _servicio.DejarACero(EMPRESA_DEFECTO, almacenInventario, producto, otraVez, quitarSinUbicacion, True)
            Catch ex As ConflictoInventarioException When (ex.EsCodigo(CodigosConflictoInventario.YA_CONTABILIZADO) AndAlso Not otraVez) OrElse
                                                          (ex.EsCodigo(CodigosConflictoInventario.FILAS_SIN_UBICACION) AndAlso Not quitarSinUbicacion)
                conflicto = ex
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
                Return
            Finally
                estaOcupado = False
            End Try
            If conflicto Is Nothing Then
                Exit For
            End If
            If conflicto.EsCodigo(CodigosConflictoInventario.YA_CONTABILIZADO) Then
                If Not PreguntarContarOtraVez(conflicto) Then
                    Return
                End If
                otraVez = True
            Else
                If Not dialogService.ShowConfirmationAnswer(TITULO_DEJAR_A_CERO, TextoQuitarSinUbicacion(conflicto), True) Then
                    Return
                End If
                quitarSinUbicacion = True
            End If
        Next
        If plan Is Nothing Then
            Return
        End If
        If Not HayQueHacer(plan) Then
            dialogService.ShowNotification(TITULO_DEJAR_A_CERO, If(String.IsNullOrWhiteSpace(plan.Aviso), "No hay nada que dejar a cero.", plan.Aviso))
            Return
        End If
        If Not dialogService.ShowConfirmationAnswer(TITULO_DEJAR_A_CERO, TextoConfirmacionDejarACero(plan), True) Then
            Return
        End If

        Dim hecho As DejarACeroInventarioRespuestaDTO
        estaOcupado = True
        Try
            hecho = Await _servicio.DejarACero(EMPRESA_DEFECTO, almacenInventario, producto, otraVez, quitarSinUbicacion, False)
        Catch ex As Exception
            ' También un 409 nuevo (algo ha cambiado desde la confirmación): no se ha tocado nada.
            dialogService.ShowError(ex.Message)
            Return
        Finally
            estaOcupado = False
        End Try
        numeroProducto = String.Empty
        dialogService.ShowNotification(TITULO_DEJAR_A_CERO, TextoResultadoDejarACero(hecho))
    End Function

    ''' <summary>Hay algo que cambiar (una fila que crear o poner a 0, o una sin ubicación que borrar).</summary>
    Public Shared Function HayQueHacer(plan As DejarACeroInventarioRespuestaDTO) As Boolean
        Return plan IsNot Nothing AndAlso (
            (plan.Lineas IsNot Nothing AndAlso plan.Lineas.Any(Function(l) l.Accion <> LineaDejarACeroDTO.YA_ESTABA_A_CERO)) OrElse
            (plan.FilasQuitadas IsNot Nothing AndAlso plan.FilasQuitadas.Any()))
    End Function

    ''' <summary>«001-002-005» o «sin ubicación».</summary>
    Public Shared Function TextoUbicacion(pasillo As String, fila As String, columna As String) As String
        If String.IsNullOrWhiteSpace(pasillo) Then
            Return "sin ubicación"
        End If
        Return $"{pasillo.Trim()}-{If(fila, String.Empty).Trim()}-{If(columna, String.Empty).Trim()}"
    End Function

    ''' <summary>Lo que se va a hacer, ubicación a ubicación, para confirmar.</summary>
    Public Shared Function TextoConfirmacionDejarACero(plan As DejarACeroInventarioRespuestaDTO) As String
        Dim lineas As New List(Of String)
        Dim nombre = If(String.IsNullOrWhiteSpace(plan.Descripcion), String.Empty, $" ({plan.Descripcion.Trim()})")
        lineas.Add($"Se va a dejar a cero el {plan.Producto?.Trim()}{nombre} en {plan.Almacen?.Trim()} (inventario de hoy):")
        For Each linea In If(plan.Lineas, New List(Of LineaDejarACeroDTO))
            Dim donde = TextoUbicacion(linea.Pasillo, linea.Fila, linea.Columna)
            Select Case linea.Accion
                Case LineaDejarACeroDTO.YA_ESTABA_A_CERO
                    lineas.Add($"  • {donde}: ya está contado a 0 (teórico {linea.StockCalculado}).")
                Case LineaDejarACeroDTO.PONER_A_CERO
                    lineas.Add($"  • {donde}: teórico {linea.StockCalculado}, contado {linea.ContadoAntes} → 0.")
                Case Else
                    lineas.Add($"  • {donde}: {linea.StockCalculado} → 0.")
            End Select
        Next
        For Each quitada In If(plan.FilasQuitadas, New List(Of LineaInventarioContadaDTO))
            lineas.Add($"  • Se borra la fila sin ubicación nº {quitada.NumeroOrden} (contado {quitada.StockReal}).")
        Next
        If Not String.IsNullOrWhiteSpace(plan.Aviso) Then
            lineas.Add(String.Empty)
            lineas.Add(plan.Aviso.Trim())
        End If
        lineas.Add(String.Empty)
        lineas.Add("¿Ha comprobado que de verdad no queda ninguno en esas ubicaciones?")
        Return String.Join(Environment.NewLine, lineas)
    End Function

    Public Shared Function TextoQuitarSinUbicacion(conflicto As ConflictoInventarioException) As String
        Dim mensaje = If(conflicto?.Message, String.Empty).Trim()
        If mensaje.Length > 0 Then
            mensaje += Environment.NewLine + Environment.NewLine
        End If
        Return mensaje + "¿Borrar esa fila y seguir?"
    End Function

    Public Shared Function TextoResultadoDejarACero(hecho As DejarACeroInventarioRespuestaDTO) As String
        If hecho Is Nothing Then
            Return "Hecho."
        End If
        Dim partes As New List(Of String)
        If hecho.FilasCreadas > 0 Then
            partes.Add(If(hecho.FilasCreadas = 1, "1 fila creada", $"{hecho.FilasCreadas} filas creadas") + " a 0")
        End If
        If hecho.FilasPuestasACero > 0 Then
            partes.Add(If(hecho.FilasPuestasACero = 1, "1 fila puesta", $"{hecho.FilasPuestasACero} filas puestas") + " a 0")
        End If
        Dim quitadas = If(hecho.FilasQuitadas, New List(Of LineaInventarioContadaDTO)).Count
        If quitadas > 0 Then
            partes.Add(If(quitadas = 1, "1 fila sin ubicación borrada", $"{quitadas} filas sin ubicación borradas"))
        End If
        Dim texto = $"{hecho.Producto?.Trim()} en {hecho.Almacen?.Trim()}: " + If(partes.Any(), String.Join(", ", partes), "nada que cambiar") + "."
        If Not String.IsNullOrWhiteSpace(hecho.Aviso) Then
            texto += Environment.NewLine + Environment.NewLine + hecho.Aviso.Trim()
        End If
        Return texto
    End Function
#End Region

#Region "Funciones Auxiliares"
    ''' <summary>Nesto#596: las tiendas (todo lo que no es Algete, que se inventaría por ubicaciones desde Ariadna).</summary>
    Public Shared Function EsAlmacenDeTienda(almacenInventario As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(almacenInventario) AndAlso
            Not String.Equals(almacenInventario.Trim(), Constantes.Almacenes.ALMACEN_ALGETE, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Const MAXIMO_CARACTERES_HUECO As Integer = 3

    ''' <summary>«En ALG el inventario va por huecos: indique el pasillo, la fila y la columna donde está el producto.»</summary>
    Public Shared Function MensajeFaltaHueco(almacenInventario As String) As String
        Return $"En {If(almacenInventario, String.Empty).Trim()} el inventario va por huecos: indique el pasillo, la fila y la columna donde está el producto."
    End Function

    ''' <summary>
    ''' Pasillo, fila o columna como en el resto del almacén: «1» → «001» (solo cifras; lo demás, sin espacios y tal cual).
    ''' Nothing si está vacío o tiene más de 3 caracteres.
    ''' </summary>
    Public Shared Function NormalizarParteHueco(texto As String) As String
        Dim limpio = If(texto, String.Empty).Trim()
        If limpio.Length = 0 OrElse limpio.Length > MAXIMO_CARACTERES_HUECO Then
            Return Nothing
        End If
        Return If(limpio.All(Function(c) Char.IsDigit(c)), limpio.PadLeft(MAXIMO_CARACTERES_HUECO, "0"c), limpio)
    End Function

    ''' <summary>Normaliza el hueco en sus casillas (se ve «001») y dice si está completo.</summary>
    Private Function PrepararHueco() As Boolean
        Dim p = NormalizarParteHueco(Pasillo)
        Dim f = NormalizarParteHueco(Fila)
        Dim c = NormalizarParteHueco(Columna)
        If p Is Nothing OrElse f Is Nothing OrElse c Is Nothing Then
            Return False
        End If
        Pasillo = p
        Fila = f
        Columna = c
        Return True
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
        If Not EsAlmacenDeTienda(almacen) Then
            PonerDeshacer(Nothing)
            Return
        End If
        Dim lecturasAntes = _lecturasDeshacer
        If Not _preguntadosAlAbrir.Contains(clave) Then
            _preguntadosAlAbrir.Add(clave)
            Await AsegurarRellenado()
        End If
        ' 25/11/26: si ese día hay un rellenado que se puede deshacer, se ofrece (si se acaba de rellenar, ya se ha mirado).
        If _lecturasDeshacer = lecturasAntes Then
            Await ActualizarDeshacerRellenado()
        End If
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

        ' 25/11/26 (Paloma rellenó sin querer 3.032 filas de ALC; no era la primera vez): ya no basta con un «Sí». Se dice
        ' cuántas filas va a crear y hay que ESCRIBIR el nombre o el código del almacén.
        Dim previa = Await LeerPreviaSinFallar(almacenTienda, fechaSeleccionada.Date)
        If Not PedirConfirmacionRellenar(almacenTienda, fechaSeleccionada.Date, previa) Then
            Return False
        End If

        ' NestoAPI#784: si ese día ya se contabilizó (409 YaContabilizado), solo se rellena otra vez si el usuario lo pide.
        Dim yaContabilizado As InventarioYaContabilizadoException = Nothing
        estaOcupado = True
        Try
            Return Await RellenadoAhora(clave, Await _servicio.Rellenar(EMPRESA_DEFECTO, almacenTienda, fechaSeleccionada.Date, False, almacenTienda))
        Catch ex As InventarioYaContabilizadoException
            yaContabilizado = ex
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

        If Not PreguntarContarOtraVez(yaContabilizado) Then
            Return False
        End If
        estaOcupado = True
        Try
            Return Await RellenadoAhora(clave, Await _servicio.Rellenar(EMPRESA_DEFECTO, almacenTienda, fechaSeleccionada.Date, True, almacenTienda))
        Catch ex As InventarioYaRellenadoException
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

    Public Const TITULO_CONTAR_OTRA_VEZ As String = "Contar otra vez"

    ''' <summary>
    ''' NestoAPI#784: el mensaje de la API (quién lo contabilizó y en qué traspaso) y «¿Contar otra vez?», con «No» por defecto:
    ''' lo que se rellena de nuevo y nadie cuenta se queda a cero al contabilizar.
    ''' </summary>
    Private Function PreguntarContarOtraVez(ex As ConflictoInventarioException) As Boolean
        Dim mensaje = If(ex?.Message, String.Empty).Trim()
        If mensaje.Length > 0 Then
            mensaje += Environment.NewLine + Environment.NewLine
        End If
        mensaje += "¿Contar otra vez?"
        Return dialogService.ShowConfirmationAnswer(TITULO_CONTAR_OTRA_VEZ, mensaje, True)
    End Function

    Private Async Function RellenadoAhora(clave As String, respuesta As RellenarInventarioRespuestaDTO) As Task(Of Boolean)
        _rellenados.Add(clave)
        Dim filas = If(respuesta Is Nothing, 0, respuesta.Filas)
        Dim unidades = If(respuesta Is Nothing, 0, respuesta.Unidades)
        dialogService.ShowNotification("Inventario rellenado", $"Inventario rellenado: {filas} productos, {unidades} unidades.")
        ' 25/11/26: si se ha rellenado sin querer, que se pueda deshacer en el acto.
        Await ActualizarDeshacerRellenado()
        Return True
    End Function

#Region "Confirmar y deshacer el rellenado (25/11/26)"
    Public Const TITULO_RELLENAR As String = "Rellenar inventario"
    Public Const TITULO_DESHACER_RELLENADO As String = "Deshacer el rellenado"
    Public Const TEXTO_BOTON_RELLENAR As String = "Rellenar"

    Private Shared ReadOnly CulturaEspanola As New CultureInfo("es-ES")

    ''' <summary>La Previa de la API; Nothing si no la tiene (servidor antiguo) o si falla: entonces se pide igual escribir el nombre.</summary>
    Private Async Function LeerPreviaSinFallar(almacenTienda As String, dia As Date) As Task(Of PreviaRellenarInventarioDTO)
        Try
            Return Await _servicio.PreviaRellenar(EMPRESA_DEFECTO, almacenTienda, dia)
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' LA REGLA (pura) de lo que vale escribir para confirmar el rellenado: el código del almacén, el nombre que dice la API y el
    ''' nombre de la sede que conocemos («ALC», «Alcobendas»).
    ''' </summary>
    Public Shared Function TextosQueConfirmanRellenar(almacenTienda As String, previa As PreviaRellenarInventarioDTO) As List(Of String)
        Dim codigo = If(almacenTienda, String.Empty).Trim()
        Dim textos As New List(Of String)
        If codigo.Length > 0 Then
            textos.Add(codigo)
        End If
        If previa IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(previa.NombreAlmacen) Then
            textos.Add(previa.NombreAlmacen.Trim())
        End If
        Dim sede = Constantes.Sedes.ListaSedes.FirstOrDefault(Function(s) String.Equals(s.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
        If sede IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(sede.Nombre) Then
            textos.Add(sede.Nombre.Trim())
        End If
        Return textos.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    ''' <summary>El nombre del almacén para los textos: el de la API, el de la sede o, si no, el código.</summary>
    Private Shared Function NombreAlmacen(almacenTienda As String, previa As PreviaRellenarInventarioDTO) As String
        Dim textos = TextosQueConfirmanRellenar(almacenTienda, previa)
        Return If(textos.Count > 1, textos(1), If(almacenTienda, String.Empty).Trim())
    End Function

    ''' <summary>
    ''' El mensaje del diálogo: el de la Previa («Va a crear 3.032 filas de inventario en Alcobendas con fecha 25/11/2026.») o, sin
    ''' ella, sin la cifra.
    ''' </summary>
    Public Shared Function MensajeConfirmarRellenar(almacenTienda As String, dia As Date, previa As PreviaRellenarInventarioDTO) As String
        If previa IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(previa.Mensaje) Then
            Return previa.Mensaje.Trim()
        End If
        Return $"Va a rellenar el inventario de {NombreAlmacen(almacenTienda, previa)} con el stock actual, con fecha {dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}. Antes de contar hay que rellenarlo."
    End Function

    Private Function PedirConfirmacionRellenar(almacenTienda As String, dia As Date, previa As PreviaRellenarInventarioDTO) As Boolean
        Dim aceptables = TextosQueConfirmanRellenar(almacenTienda, previa)
        Dim instruccion = $"Si de verdad quiere rellenarlo, escriba el nombre o el código del almacén ({String.Join(" o ", aceptables)}) y pulse «{TEXTO_BOTON_RELLENAR}»."
        Return ConfirmacionEscribiendo.Pedir(dialogService, TITULO_RELLENAR, MensajeConfirmarRellenar(almacenTienda, dia, previa),
                                             instruccion, TEXTO_BOTON_RELLENAR, aceptables)
    End Function

    ' Veces que se ha mirado si se puede deshacer (ComprobarRellenoAlAbrir no repite la lectura si se acaba de hacer).
    Private _lecturasDeshacer As Integer
    Private _previaDeshacer As PreviaRellenarInventarioDTO

    Private _puedeDeshacerRellenado As Boolean
    ''' <summary>Hay un rellenado de este almacén y día que se puede deshacer (lo dice la Previa: PuedeDeshacer). Si no, no se ve el botón.</summary>
    Public Property PuedeDeshacerRellenado As Boolean
        Get
            Return _puedeDeshacerRellenado
        End Get
        Private Set(value As Boolean)
            SetProperty(_puedeDeshacerRellenado, value)
        End Set
    End Property

    Private Sub PonerDeshacer(previa As PreviaRellenarInventarioDTO)
        _previaDeshacer = If(previa IsNot Nothing AndAlso previa.PuedeDeshacer, previa, Nothing)
        PuedeDeshacerRellenado = _previaDeshacer IsNot Nothing
    End Sub

    ''' <summary>Mira (Previa) si el rellenado de la tienda ese día se puede deshacer. Nunca falla: si no se sabe, no se ofrece.</summary>
    Public Async Function ActualizarDeshacerRellenado() As Task
        _lecturasDeshacer += 1
        If Not EsAlmacenDeTienda(almacen) Then
            PonerDeshacer(Nothing)
            Return
        End If
        PonerDeshacer(Await LeerPreviaSinFallar(almacen.Trim(), fechaSeleccionada.Date))
    End Function

    Private _cmdDeshacerRellenado As AsyncRelayCommand
    Public Property cmdDeshacerRellenado As AsyncRelayCommand
        Get
            Return _cmdDeshacerRellenado
        End Get
        Private Set(value As AsyncRelayCommand)
            SetProperty(_cmdDeshacerRellenado, value)
        End Set
    End Property

    ''' <summary>«Se han borrado 3.032 filas.» / «Se ha borrado 1 fila.» (y las contadas, si había).</summary>
    Public Shared Function MensajeRellenadoDeshecho(respuesta As DeshacerRellenarInventarioRespuestaDTO) As String
        Dim borradas = If(respuesta Is Nothing, 0, respuesta.FilasBorradas)
        Dim texto = If(borradas = 1, "Se ha borrado 1 fila.", $"Se han borrado {borradas.ToString("N0", CulturaEspanola)} filas.")
        Dim contadas = If(respuesta Is Nothing, 0, respuesta.FilasContadas)
        If contadas > 0 Then
            texto += If(contadas = 1, " 1 de ellas ya estaba contada.", $" {contadas.ToString("N0", CulturaEspanola)} de ellas ya estaban contadas.")
        End If
        Return texto
    End Function

    ''' <summary>
    ''' 25/11/26: borra las filas pendientes del rellenado de la tienda ese día (DELETE api/Inventarios/Rellenar; la API decide quién
    ''' puede: quien lo rellenó si todas son suyas y ninguna está contada). Con confirmación («No» por defecto); después recarga la lista.
    ''' </summary>
    Public Async Function DeshacerRellenado() As Task
        If Not PuedeDeshacerRellenado OrElse Not EsAlmacenDeTienda(almacen) Then
            Return
        End If
        Dim almacenTienda = almacen.Trim()
        Dim dia = fechaSeleccionada.Date
        Dim previa = _previaDeshacer
        Dim filas = If(previa Is Nothing, 0, previa.FilasPendientesYa)
        Dim cuantas = If(filas = 1, "la fila", If(filas > 1, $"las {filas.ToString("N0", CulturaEspanola)} filas", "las filas"))
        Dim mensaje = $"Se van a borrar {cuantas} pendientes del inventario de {NombreAlmacen(almacenTienda, previa)} del {dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}. El stock no cambia. ¿Deshacer el rellenado?"
        If Not dialogService.ShowConfirmationAnswer(TITULO_DESHACER_RELLENADO, mensaje, True) Then
            Return
        End If

        Dim respuesta As DeshacerRellenarInventarioRespuestaDTO = Nothing
        estaOcupado = True
        Try
            respuesta = Await _servicio.DeshacerRellenado(EMPRESA_DEFECTO, almacenTienda, dia)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaOcupado = False
        End Try

        If respuesta IsNot Nothing Then
            ' Ya no está rellenado: al contar se volverá a preguntar.
            _rellenados.Remove(ClaveRelleno())
            Dim aviso = MensajeRellenadoDeshecho(respuesta)
            Try
                movimientosTotal = New ObservableCollection(Of InventarioDTO)(Await _servicio.LeerLineas(EMPRESA_DEFECTO, almacenTienda, dia))
            Catch ex As Exception
                ' Lo borrado, borrado está: si no se puede recargar la lista, se dice en el mismo aviso.
                aviso += " No se ha podido recargar la lista: " + ex.Message
            End Try
            dialogService.ShowNotification(TITULO_DESHACER_RELLENADO, aviso)
        End If
        Await ActualizarDeshacerRellenado()
    End Function
#End Region

#Region "Borrar una fila desde la lista (novedad 1456)"
    Public Const MENSAJE_FILA_BORRADA As String = "Fila borrada. El stock no cambia."

    Private _cmdBorrarFila As AsyncRelayCommand(Of InventarioDTO)
    Public Property cmdBorrarFila As AsyncRelayCommand(Of InventarioDTO)
        Get
            Return _cmdBorrarFila
        End Get
        Private Set(value As AsyncRelayCommand(Of InventarioDTO))
            SetProperty(_cmdBorrarFila, value)
        End Set
    End Property

    ''' <summary>Lo decide la API (SePuedeBorrar): sin la marca, o sin Nº de orden, no hay botón.</summary>
    Public Shared Function EsFilaBorrable(fila As InventarioDTO) As Boolean
        Return fila IsNot Nothing AndAlso fila.SePuedeBorrar AndAlso fila.NumOrden > 0
    End Function

    ''' <summary>La fila de la lista como la de las comprobaciones, para la MISMA confirmación que «Borrar esta fila».</summary>
    Public Shared Function ComoFilaComprobacion(fila As InventarioDTO) As FilaComprobacionInventarioDTO
        Return New FilaComprobacionInventarioDTO With {
            .NumOrden = fila.NumOrden, .Producto = fila.Producto, .Descripcion = fila.Descripcion,
            .StockCalculado = fila.StockCalculado, .StockReal = fila.StockReal,
            .Pasillo = fila.Pasillo, .Fila = fila.Fila, .Columna = fila.Columna,
            .Usuario = fila.Usuario, .Fecha = fila.Fecha, .SePuedeBorrar = fila.SePuedeBorrar
        }
    End Function

    ''' <summary>
    ''' Novedad 1456 (Paloma no encontró cómo borrar una fila pendiente suya de otro día): la misma confirmación (por defecto «No»)
    ''' y la misma llamada que «Borrar esta fila» de las comprobaciones (DELETE api/Inventarios/Filas/{Nº orden}); después recarga la
    ''' lista. Los errores de la API (409 contabilizada, 403…), con su mensaje.
    ''' </summary>
    Public Async Function BorrarFila(fila As InventarioDTO) As Task
        If Not EsFilaBorrable(fila) OrElse estaOcupado Then
            Return
        End If
        If Not dialogService.ShowConfirmationAnswer(ComprobacionesInventarioDialogViewModel.TITULO_BORRAR_FILA,
                                                   ComprobacionesInventarioDialogViewModel.TextoConfirmarBorrado(ComoFilaComprobacion(fila)), True) Then
            Return
        End If
        Dim borrada = False
        estaOcupado = True
        Try
            Await _servicio.BorrarFila(fila.NumOrden)
            borrada = True
        Catch ex As FilaInventarioNoEncontradaException
            ' Ya no está (la habrá borrado otro): se avisa y se recarga igual.
            dialogService.ShowError(ex.Message)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return
        Finally
            estaOcupado = False
        End Try

        Dim aviso = If(borrada, MENSAJE_FILA_BORRADA, Nothing)
        Try
            movimientosTotal = New ObservableCollection(Of InventarioDTO)(Await _servicio.LeerLineas(EMPRESA_DEFECTO, almacen, fechaSeleccionada))
        Catch ex As Exception
            aviso = If(aviso Is Nothing, String.Empty, aviso + " ") + "No se ha podido recargar la lista: " + ex.Message
        End Try
        If Not String.IsNullOrEmpty(aviso) Then
            dialogService.ShowNotification(ComprobacionesInventarioDialogViewModel.TITULO_BORRAR_FILA, aviso)
        End If
    End Function
#End Region

    Public Async Function InsertarProducto(producto As String) As Task(Of Movimiento)
        Return Await OnInsertarProducto(producto)
    End Function

#End Region
End Class
