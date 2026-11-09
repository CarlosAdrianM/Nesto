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
        ' Nesto#597: contabilizar y ver las diferencias (NestoAPI#790).
        cmdContabilizarInventario = New AsyncRelayCommand(AddressOf ContabilizarInventario)
        cmdVerDiferencias = New RelayCommand(AddressOf OnVerDiferencias)

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
            existente = Await _servicio.BuscarLinea(EMPRESA_DEFECTO, linea.Almacen, linea.Fecha, leido)
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
            .Lectura = TextoLectura(linea.PorCodigoBarras)
        }
        movimientosDia.Add(nuevoMovimiento)
        OnPropertyChanged(NameOf(movimientosDia))
        numeroProducto = String.Empty
        cantidad = 1
        Return nuevoMovimiento
    End Function


#End Region

#Region "Contabilizar (Nesto#597)"
    ''' <summary>Nesto#597: Tiendas (la suya), Administración, Dirección e Informática; la API lo vuelve a comprobar.</summary>
    Public ReadOnly Property PuedeContabilizar As Boolean
        Get
            Return EnAlgunGrupo(Constantes.GruposSeguridad.TIENDAS, Constantes.GruposSeguridad.ADMINISTRACION,
                                Constantes.GruposSeguridad.DIRECCION, Constantes.GruposSeguridad.INFORMATICA)
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
            Dim decision = Await MostrarComprobaciones(comprobaciones)
            If decision = DecisionComprobaciones.NoSeguir Then
                Return
            End If
            confirmarContadoCero = decision = DecisionComprobaciones.SeguirConfirmandoContadoCero
        End If

        Dim recuento = If(comprobaciones.Recuento, New List(Of RecuentoUsuarioInventarioDTO))
        Dim filas = recuento.Sum(Function(r) r.Filas)
        If filas = 0 Then
            dialogService.ShowNotification("Contabilizar inventario", $"No hay filas pendientes de contabilizar en {almacenInventario} el {textoDia}.")
            Return
        End If
        Dim mensaje = $"Se va a contabilizar el inventario de {almacenInventario} del {textoDia}: {TextoFilas(filas)} de {TextoUsuarios(recuento.Count)}."
        If confirmarContadoCero Then
            mensaje += " Ha confirmado que hay 0 en los productos vueltos a contar: su stock se quedará a cero."
        End If
        mensaje += " ¿Continuar?"
        If Not dialogService.ShowConfirmationAnswer("Contabilizar inventario", mensaje, True) Then
            Return
        End If

        Dim resultado = Await LlamarContabilizar(almacenInventario, fecha, confirmarContadoCero)
        If resultado Is Nothing Then
            Return
        End If
        If resultado.Bloqueado IsNot Nothing Then
            ' Algo ha cambiado desde las comprobaciones: no se ha tocado nada. Si solo quedan bloqueantes que se confirman y
            ' ahora se confirman (antes no), se intenta una vez más; si no, se enseñan y se para.
            Dim decision = Await MostrarComprobaciones(resultado.Bloqueado)
            If confirmarContadoCero OrElse decision <> DecisionComprobaciones.SeguirConfirmandoContadoCero Then
                Return
            End If
            resultado = Await LlamarContabilizar(almacenInventario, fecha, True)
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
    Private Async Function LlamarContabilizar(almacenInventario As String, fecha As Date, confirmarContadoCero As Boolean) As Task(Of ResultadoContabilizarInventario)
        estaOcupado = True
        Try
            Return Await _servicio.Contabilizar(EMPRESA_DEFECTO, almacenInventario, fecha, confirmarContadoCero)
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
    Private Async Function MostrarComprobaciones(comprobaciones As ComprobacionesInventarioDTO) As Task(Of DecisionComprobaciones)
        Dim parametros As New ParametrosDialogo From {
            {ComprobacionesInventarioDialogViewModel.PARAMETRO_COMPROBACIONES, comprobaciones}
        }
        Dim respuesta = Await dialogService.ShowDialogAsync(ComprobacionesInventarioDialogViewModel.NOMBRE, parametros)
        If respuesta Is Nothing OrElse respuesta.Result <> ResultadoBoton.OK Then
            Return DecisionComprobaciones.NoSeguir
        End If
        Dim bloqueantes = If(comprobaciones.Bloqueantes, New List(Of ComprobacionInventarioDTO))
        If Not bloqueantes.Any() Then
            Return DecisionComprobaciones.Seguir
        End If
        If bloqueantes.Any(Function(b) Not b.SeConfirma) Then
            Return DecisionComprobaciones.NoSeguir
        End If
        Dim confirmado = respuesta.Parameters IsNot Nothing AndAlso
            respuesta.Parameters.GetValue(Of Boolean)(ComprobacionesInventarioDialogViewModel.PARAMETRO_CONFIRMAR_CONTADO_CERO)
        Return If(confirmado, DecisionComprobaciones.SeguirConfirmandoContadoCero, DecisionComprobaciones.NoSeguir)
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

        ' NestoAPI#784: si ese día ya se contabilizó (409 YaContabilizado), solo se rellena otra vez si el usuario lo pide.
        Dim yaContabilizado As InventarioYaContabilizadoException = Nothing
        estaOcupado = True
        Try
            Return RellenadoAhora(clave, Await _servicio.Rellenar(EMPRESA_DEFECTO, almacenTienda, fechaSeleccionada.Date, False))
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
            Return RellenadoAhora(clave, Await _servicio.Rellenar(EMPRESA_DEFECTO, almacenTienda, fechaSeleccionada.Date, True))
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
    Private Function PreguntarContarOtraVez(ex As InventarioYaContabilizadoException) As Boolean
        Dim mensaje = If(ex?.Message, String.Empty).Trim()
        If mensaje.Length > 0 Then
            mensaje += Environment.NewLine + Environment.NewLine
        End If
        mensaje += "¿Contar otra vez?"
        Return dialogService.ShowConfirmationAnswer(TITULO_CONTAR_OTRA_VEZ, mensaje, True)
    End Function

    Private Function RellenadoAhora(clave As String, respuesta As RellenarInventarioRespuestaDTO) As Boolean
        _rellenados.Add(clave)
        Dim filas = If(respuesta Is Nothing, 0, respuesta.Filas)
        Dim unidades = If(respuesta Is Nothing, 0, respuesta.Unidades)
        dialogService.ShowNotification("Inventario rellenado", $"Inventario rellenado: {filas} productos, {unidades} unidades.")
        Return True
    End Function

    Public Async Function InsertarProducto(producto As String) As Task(Of Movimiento)
        Return Await OnInsertarProducto(producto)
    End Function

#End Region
End Class
