Imports System.Threading
Imports CommunityToolkit.Mvvm.Input
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

''' <summary>
''' Nesto#642 (NestoAPI#873, pedido 929555): que nadie guarde un pedido con una copia vieja.
''' - La API manda la versión del pedido al leerlo, crearlo y modificarlo (va en el modelo, <see cref="PedidoVentaDTO.Version"/>) y,
'''   si al guardar ya no es la del pedido guardado, contesta 409 PEDIDO_CAMBIADO sin guardar nada: se enseña su mensaje (quién y
'''   cuándo) y se ofrece recargar.
''' - En tiempo real: mientras el detalle está a la vista se está en el grupo «Pedido:{empresa}:{numero}» del hub del almacén (el
'''   mismo patrón que «Reposiciones:{tienda}»). Si llega «pedidoCambiado» con otra versión (otra persona u otra ventana; el aviso
'''   del propio guardado trae la versión que acaba de devolver el PUT), banda «Aida ha modificado este pedido a las 15:14.
'''   Recárguelo antes de guardar» con «Recargar», y Guardar deshabilitado hasta recargar.
''' - Sin temporizadores: al volver a enseñar el detalle (otra pestaña) o al recuperar la conexión se pregunta la versión una vez
'''   (GET …/Version), por si se perdió algún aviso. Lo que no llegue lo para el 409.
''' </summary>
Partial Public Class DetallePedidoViewModel

    ''' <summary>Los grupos de SignalR (Nothing = sin tiempo real: solo el 409). Friend para los tests.</summary>
    Friend Property GruposTiempoReal As IGruposAlmacenTiempoReal

    Public Property RecargarPedidoCambiadoCommand As AsyncRelayCommand

    Private _contextoUIVersion As SynchronizationContext
    Private _grupoPedidoActual As String
    Private _avisosPedidoActivos As Boolean
    Private _avisosPedidoYaActivados As Boolean
    Private _guardandoPedido As Boolean
    Private _avisoDuranteGuardado As PedidoCambiadoAviso

    Private Sub InicializarVersionPedido(avisos As IAvisosEnTiempoReal)
        GruposTiempoReal = TryCast(avisos, IGruposAlmacenTiempoReal)
        _contextoUIVersion = SynchronizationContext.Current
        RecargarPedidoCambiadoCommand = New AsyncRelayCommand(AddressOf RecargarPedidoCambiadoAsync, Function() PedidoCambiadoPorOtro)
    End Sub

    Private _pedidoCambiadoPorOtro As Boolean
    ''' <summary>Alguien ha guardado el pedido desde que se cargó: banda visible y Guardar deshabilitado hasta recargar.</summary>
    Public Property PedidoCambiadoPorOtro As Boolean
        Get
            Return _pedidoCambiadoPorOtro
        End Get
        Private Set(value As Boolean)
            If SetProperty(_pedidoCambiadoPorOtro, value) Then
                cmdModificarPedido?.NotifyCanExecuteChanged()
                RecargarPedidoCambiadoCommand?.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _textoPedidoCambiadoPorOtro As String
    ''' <summary>«Aida ha modificado este pedido a las 15:14. Recárguelo antes de guardar.»</summary>
    Public Property TextoPedidoCambiadoPorOtro As String
        Get
            Return _textoPedidoCambiadoPorOtro
        End Get
        Private Set(value As String)
            Dim unused = SetProperty(_textoPedidoCambiadoPorOtro, value)
        End Set
    End Property

    ''' <summary>El texto de la banda. Sin quién o sin cuándo, sin ellos; si no fue hoy, con el día.</summary>
    Friend Shared Function TextoBandaPedidoCambiado(usuario As String, fecha As Date?, hoy As Date) As String
        Dim quien As String = If(String.IsNullOrWhiteSpace(usuario), "Otra persona", usuario.Trim())
        Dim cuando As String = String.Empty
        If fecha.HasValue AndAlso fecha.Value > Date.MinValue Then
            cuando = If(fecha.Value.Date = hoy.Date,
                        $" a las {fecha.Value:HH\:mm}",
                        $" el {fecha.Value:dd\/MM\/yyyy} a las {fecha.Value:HH\:mm}")
        End If
        Return $"{quien} ha modificado este pedido{cuando}. Recárguelo antes de guardar."
    End Function

    ''' <summary>
    ''' Si el aviso es de un cambio que este detalle no ha hecho: del pedido en pantalla y con otra versión que la suya (el aviso del
    ''' propio guardado trae la versión que devolvió el PUT). Sin versión en el aviso, se avisa (no se sabe).
    ''' </summary>
    Friend Shared Function EsCambioDeOtro(aviso As PedidoCambiadoAviso, empresa As String, numero As Integer, versionPropia As String) As Boolean
        If aviso Is Nothing OrElse numero <= 0 OrElse aviso.Pedido <> numero OrElse
            Not String.Equals(If(aviso.Empresa, String.Empty).Trim(), If(empresa, String.Empty).Trim(), StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If
        If String.IsNullOrWhiteSpace(aviso.Version) Then
            Return True
        End If
        Return Not String.Equals(aviso.Version.Trim(), If(versionPropia, String.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>El grupo del pedido en pantalla, o Nothing si no hay (o es nuevo).</summary>
    Private Function GrupoDelPedidoEnPantalla() As String
        If pedido Is Nothing OrElse pedido.numero <= 0 OrElse String.IsNullOrWhiteSpace(pedido.empresa) Then
            Return Nothing
        End If
        Return GruposAlmacen.Pedido(pedido.empresa, pedido.numero)
    End Function

    ''' <summary>La vista se enseña (Loaded): se entra en el grupo del pedido; si vuelve a verse, se mira la versión una vez.</summary>
    Public Sub ActivarAvisosPedido()
        If _avisosPedidoActivos Then
            Return
        End If
        _avisosPedidoActivos = True
        If GruposTiempoReal IsNot Nothing Then
            RemoveHandler GruposTiempoReal.PedidoCambiado, AddressOf AlLlegarPedidoCambiado
            AddHandler GruposTiempoReal.PedidoCambiado, AddressOf AlLlegarPedidoCambiado
            RemoveHandler GruposTiempoReal.GrupoHaCambiado, AddressOf AlCambiarGrupoPedido
            AddHandler GruposTiempoReal.GrupoHaCambiado, AddressOf AlCambiarGrupoPedido
        End If
        EntrarEnGrupoDelPedido()
        If _avisosPedidoYaActivados Then
            ComprobacionVersionEnCurso = ComprobarVersionPedidoAsync() ' estuvo oculta: pudo perderse algún aviso
        End If
        _avisosPedidoYaActivados = True
    End Sub

    ''' <summary>La vista se oculta o se cierra (Unloaded, o se le quita el DataContext): se sale del grupo. Nunca lanza.</summary>
    Public Sub DesactivarAvisosPedido()
        If Not _avisosPedidoActivos Then
            Return
        End If
        _avisosPedidoActivos = False
        SalirDelGrupoDelPedido()
        If GruposTiempoReal IsNot Nothing Then
            RemoveHandler GruposTiempoReal.PedidoCambiado, AddressOf AlLlegarPedidoCambiado
            RemoveHandler GruposTiempoReal.GrupoHaCambiado, AddressOf AlCambiarGrupoPedido
        End If
    End Sub

    Private Sub EntrarEnGrupoDelPedido()
        Dim grupo As String = If(_avisosPedidoActivos, GrupoDelPedidoEnPantalla(), Nothing)
        If String.Equals(grupo, _grupoPedidoActual, StringComparison.OrdinalIgnoreCase) Then
            Return
        End If
        SalirDelGrupoDelPedido()
        If grupo Is Nothing OrElse GruposTiempoReal Is Nothing Then
            Return
        End If
        _grupoPedidoActual = grupo
        GruposTiempoReal.EntrarEnGrupo(grupo)
    End Sub

    Private Sub SalirDelGrupoDelPedido()
        If _grupoPedidoActual Is Nothing Then
            Return
        End If
        Dim grupo As String = _grupoPedidoActual
        _grupoPedidoActual = Nothing
        GruposTiempoReal?.SalirDeGrupo(grupo)
    End Sub

    ''' <summary>Para los tests: el grupo en el que está el detalle (Nothing si en ninguno).</summary>
    Friend ReadOnly Property GrupoPedidoActual As String
        Get
            Return _grupoPedidoActual
        End Get
    End Property

    ''' <summary>Se ha cargado (o creado) otro pedido en pantalla: fuera la banda y al grupo del pedido nuevo.</summary>
    Friend Sub AlCambiarElPedidoEnPantalla()
        PedidoCambiadoPorOtro = False
        TextoPedidoCambiadoPorOtro = Nothing
        _avisoDuranteGuardado = Nothing
        EntrarEnGrupoDelPedido()
    End Sub

    Private Sub EnLaUI(accion As Action)
        If _contextoUIVersion IsNot Nothing AndAlso SynchronizationContext.Current IsNot _contextoUIVersion Then
            _contextoUIVersion.Post(Sub(estado) accion(), Nothing)
        Else
            accion()
        End If
    End Sub

    ''' <summary>«pedidoCambiado» (llega en otro hilo).</summary>
    Private Sub AlLlegarPedidoCambiado(sender As Object, aviso As PedidoCambiadoAviso)
        EnLaUI(Sub() ProcesarPedidoCambiado(aviso))
    End Sub

    ''' <summary>Tras recuperar la conexión llega «haCambiado» de cada grupo: se pregunta la versión una vez.</summary>
    Private Sub AlCambiarGrupoPedido(sender As Object, grupo As String)
        If _grupoPedidoActual Is Nothing OrElse Not String.Equals(grupo?.Trim(), _grupoPedidoActual, StringComparison.OrdinalIgnoreCase) Then
            Return
        End If
        EnLaUI(Sub() ComprobacionVersionEnCurso = ComprobarVersionPedidoAsync())
    End Sub

    ''' <summary>Friend para los tests: lo que se hace con un «pedidoCambiado», ya en el hilo de la UI.</summary>
    Friend Sub ProcesarPedidoCambiado(aviso As PedidoCambiadoAviso)
        If pedido?.Model Is Nothing OrElse aviso Is Nothing Then
            Return
        End If
        If _guardandoPedido Then
            ' Puede ser el aviso del propio guardado, que llega antes que la respuesta del PUT: se mira al terminar
            _avisoDuranteGuardado = aviso
            Return
        End If
        If EsCambioDeOtro(aviso, pedido.empresa, pedido.numero, pedido.Model.Version) Then
            MostrarPedidoCambiadoPorOtro(aviso.Usuario, aviso.Fecha)
        End If
    End Sub

    Private Sub MostrarPedidoCambiadoPorOtro(usuario As String, fecha As Date?)
        TextoPedidoCambiadoPorOtro = TextoBandaPedidoCambiado(usuario, fecha, Date.Today)
        PedidoCambiadoPorOtro = True
    End Sub

    ''' <summary>Para los tests: la última comprobación de la versión lanzada.</summary>
    Friend Property ComprobacionVersionEnCurso As Task = Task.CompletedTask

    ''' <summary>Pregunta a la API la versión de ahora y, si no es la del pedido en pantalla, enseña la banda. Nunca lanza.</summary>
    Friend Async Function ComprobarVersionPedidoAsync() As Task
        Try
            If pedido?.Model Is Nothing OrElse pedido.numero <= 0 OrElse String.IsNullOrWhiteSpace(pedido.Model.Version) OrElse PedidoCambiadoPorOtro Then
                Return
            End If
            Dim empresa As String = pedido.empresa
            Dim numero As Integer = pedido.numero
            Dim actual As VersionPedidoModel = Await servicio.LeerVersionPedido(empresa, numero)
            If actual Is Nothing OrElse String.IsNullOrWhiteSpace(actual.Version) OrElse _guardandoPedido OrElse
                pedido?.Model Is Nothing OrElse pedido.numero <> numero Then
                Return
            End If
            If Not String.Equals(actual.Version.Trim(), If(pedido.Model.Version, String.Empty).Trim(), StringComparison.OrdinalIgnoreCase) Then
                MostrarPedidoCambiadoPorOtro(actual.Usuario, actual.Fecha)
            End If
        Catch
            ' Es una ayuda: si falla, el 409 del servidor lo para al guardar
        End Try
    End Function

    ''' <summary>Antes de guardar: marca que se está guardando (los avisos que lleguen se miran al terminar).</summary>
    Private Sub EmpezarGuardadoPedido()
        _guardandoPedido = True
        _avisoDuranteGuardado = Nothing
    End Sub

    ''' <summary>Al terminar de guardar (bien o mal): el aviso que llegó entre medias, ya con la versión que devolvió el PUT.</summary>
    Friend Sub TerminarGuardadoPedido()
        _guardandoPedido = False
        Dim pendiente As PedidoCambiadoAviso = _avisoDuranteGuardado
        _avisoDuranteGuardado = Nothing
        If pendiente IsNot Nothing Then
            ProcesarPedidoCambiado(pendiente)
        End If
    End Sub

    ''' <summary>
    ''' El 409 PEDIDO_CAMBIADO (o Guardar con la banda puesta): el mensaje de la API y «¿Desea recargarlo ahora?». No se ha guardado nada.
    ''' </summary>
    Friend Async Function ResolverPedidoCambiadoAsync(ex As PedidoCambiadoException) As Task
        If ex IsNot Nothing Then
            MostrarPedidoCambiadoPorOtro(ex.Usuario, ex.Fecha)
        End If
        Dim mensaje As String = If(ex?.Message, TextoPedidoCambiadoPorOtro)
        Dim recargar As Boolean = Await dialogService.ShowConfirmationAsync("Pedido modificado",
            mensaje & vbCrLf & vbCrLf & "No se ha guardado nada. ¿Desea recargar el pedido ahora? Se perderán los cambios que no haya guardado.")
        If recargar Then
            Await RecargarPedidoAsync()
        End If
    End Function

    ''' <summary>El botón «Recargar» de la banda. Si hay cambios sin guardar, pregunta antes (se pierden).</summary>
    Friend Async Function RecargarPedidoCambiadoAsync() As Task
        If pedido Is Nothing OrElse pedido.numero <= 0 Then
            Return
        End If
        If TieneCambiosSinGuardar Then
            Dim seguir As Boolean = Await dialogService.ShowConfirmationAsync("Recargar el pedido",
                "Al recargar el pedido se pierden los cambios que no ha guardado. ¿Desea recargarlo?")
            If Not seguir Then
                Return
            End If
        End If
        Await RecargarPedidoAsync()
    End Function

    Private Async Function RecargarPedidoAsync() As Task
        If pedido Is Nothing OrElse pedido.numero <= 0 Then
            Return
        End If
        Await CargarPedidoAsync(New ResumenPedido With {.empresa = pedido.empresa, .numero = pedido.numero})
    End Function
End Class
