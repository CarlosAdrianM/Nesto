Imports System.Threading
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
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
'''   Recárguelo antes de guardar», y Guardar deshabilitado hasta recargar.
''' - Ampliación (OK de Carlos 17/11/26): el botón principal de la banda y del 409 es «Recargar conservando mis cambios» (relee y pone
'''   encima lo del usuario con <see cref="FusionCambiosPedido"/>, preguntando solo lo que han tocado los dos); «Recargar sin mis
'''   cambios» queda como opción aparte y con confirmación.
''' - Sin temporizadores: al volver a enseñar el detalle (otra pestaña) o al recuperar la conexión se pregunta la versión una vez
'''   (GET …/Version), por si se perdió algún aviso. Lo que no llegue lo para el 409.
''' </summary>
Partial Public Class DetallePedidoViewModel

    ''' <summary>Los grupos de SignalR (Nothing = sin tiempo real: solo el 409). Friend para los tests.</summary>
    Friend Property GruposTiempoReal As IGruposAlmacenTiempoReal

    ''' <summary>Nesto#642 (ampliación): el botón principal de la banda, «Recargar conservando mis cambios».</summary>
    Public Property RecargarPedidoCambiadoCommand As AsyncRelayCommand
    ''' <summary>Nesto#642 (ampliación): «Recargar sin mis cambios», con confirmación.</summary>
    Public Property RecargarSinMisCambiosCommand As AsyncRelayCommand

    Public Const TEXTO_RECARGAR_CONSERVANDO As String = "Recargar conservando mis cambios"
    Public Const TEXTO_RECARGAR_SIN_CAMBIOS As String = "Recargar sin mis cambios"

    ''' <summary>
    ''' Nesto#642 (ampliación): el pedido tal como estaba guardado al cargarlo (o tras el último guardado sin líneas nuevas), para
    ''' separar los cambios del usuario de los del otro. Nothing si no se sabe (recién creado o guardado con líneas nuevas: sus
    ''' Nº de orden solo los sabe el servidor).
    ''' </summary>
    Private _fotoAlCargar As PedidoVentaDTO
    ''' <summary>Quién ha cambiado el pedido (el de la banda o el del 409), para las preguntas.</summary>
    Private _usuarioQueLoCambio As String

    Private _contextoUIVersion As SynchronizationContext
    Private _grupoPedidoActual As String
    Private _avisosPedidoActivos As Boolean
    Private _avisosPedidoYaActivados As Boolean
    Private _guardandoPedido As Boolean
    Private _avisoDuranteGuardado As PedidoCambiadoAviso

    Private Sub InicializarVersionPedido(avisos As IAvisosEnTiempoReal)
        GruposTiempoReal = TryCast(avisos, IGruposAlmacenTiempoReal)
        _contextoUIVersion = SynchronizationContext.Current
        RecargarPedidoCambiadoCommand = New AsyncRelayCommand(AddressOf RecargarConservandoCambiosAsync, Function() PedidoCambiadoPorOtro)
        RecargarSinMisCambiosCommand = New AsyncRelayCommand(AddressOf RecargarPedidoCambiadoAsync, Function() PedidoCambiadoPorOtro)
    End Sub

    ''' <summary>La foto tras guardar: lo que hay en pantalla, salvo que lleve líneas nuevas (sin Nº de orden): entonces no se sabe.</summary>
    Private Function FotoTrasGuardar() As PedidoVentaDTO
        If pedido?.Model Is Nothing OrElse pedido.numero <= 0 OrElse pedido.Model.Lineas Is Nothing OrElse
            pedido.Model.Lineas.Any(Function(l) l IsNot Nothing AndAlso l.id <= 0) Then
            Return Nothing
        End If
        Return FusionCambiosPedido.ClonarPedido(pedido.Model)
    End Function

    ''' <summary>Para los tests: hay foto con la que conservar los cambios.</summary>
    Friend ReadOnly Property TieneFotoAlCargar As Boolean
        Get
            Return _fotoAlCargar IsNot Nothing
        End Get
    End Property

    ''' <summary>
    ''' Nesto#642 (ampliación): lee el pedido del servidor y pone encima los cambios del usuario (<see cref="FusionCambiosPedido"/>).
    ''' Solo pregunta lo que han tocado los dos; «Cancelar» en una pregunta deja la pantalla como estaba. Al terminar el pedido
    ''' queda con la versión nueva y los cambios sin guardar, para que los revise y guarde.
    ''' </summary>
    Friend Async Function RecargarConservandoCambiosAsync() As Task
        If pedido?.Model Is Nothing OrElse pedido.numero <= 0 Then
            Return
        End If
        If _fotoAlCargar Is Nothing Then
            ' Recién creado o guardado con líneas nuevas: no se pueden separar sus cambios de los del otro
            Dim seguir As Boolean = Await dialogService.ShowConfirmationAsync("Recargar el pedido",
                "Desde que guardó el pedido no se ha vuelto a leer, así que no se pueden separar sus cambios de los de la otra persona. " &
                "¿Desea recargarlo sin sus cambios?")
            If seguir Then
                Await RecargarPedidoAsync()
            End If
            Return
        End If
        Dim foto As PedidoVentaDTO = _fotoAlCargar
        Dim mio As PedidoVentaDTO = pedido.Model
        Dim otro As String = _usuarioQueLoCambio
        Dim resultado As ResultadoFusionPedido = Nothing
        Dim cargado As Boolean = Await CargarPedidoAsync(New ResumenPedido With {.empresa = pedido.empresa, .numero = pedido.numero},
            Function(servidor)
                resultado = FusionCambiosPedido.Fusionar(foto, mio, servidor)
                For Each conflicto In resultado.Conflictos
                    Dim respuesta As ResultadoBoton = ConfirmacionConBotones.PreguntarTres(dialogService, "Los dos han cambiado lo mismo",
                        conflicto.Pregunta(otro), conflicto.TextoOpcionUsted, conflicto.TextoOpcionOtro, "Cancelar")
                    If respuesta = ResultadoBoton.Cancel Then
                        Return Task.FromResult(Of PedidoVentaDTO)(Nothing)
                    End If
                    resultado.Resolver(conflicto, respuesta = ResultadoBoton.OK)
                Next
                Return Task.FromResult(resultado.Pedido)
            End Function)
        If Not cargado Then
            If resultado IsNot Nothing Then
                dialogService.ShowNotification("Recargar el pedido", "No se ha recargado nada: el pedido sigue como lo tenía.")
            End If
            Return
        End If
        Dim texto As String = If(resultado.CambiosAplicados + resultado.Conflictos.Count = 0,
            "Se ha recargado el pedido. No tenía cambios sin guardar.",
            "Se ha recargado el pedido con sus cambios encima. Revíselo y guárdelo.")
        If resultado.Avisos.Any() Then
            texto &= vbCrLf & vbCrLf & String.Join(vbCrLf, resultado.Avisos)
        End If
        dialogService.ShowNotification("Recargar el pedido", texto)
    End Function

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
        _usuarioQueLoCambio = Nothing
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
        If Not String.IsNullOrWhiteSpace(usuario) Then
            _usuarioQueLoCambio = usuario.Trim()
        End If
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
        ' Puede anidarse (la etiqueta pendiente va dentro del guardado): solo el primero empieza de cero
        If _guardadosEnCurso = 0 Then
            _avisoDuranteGuardado = Nothing
        End If
        _guardadosEnCurso += 1
        _guardandoPedido = True
    End Sub

    Private _guardadosEnCurso As Integer

    ''' <summary>Al terminar de guardar (bien o mal): el aviso que llegó entre medias, ya con la versión que devolvió el PUT.</summary>
    Friend Sub TerminarGuardadoPedido()
        _guardadosEnCurso = Math.Max(0, _guardadosEnCurso - 1)
        If _guardadosEnCurso > 0 Then
            Return ' termina el de fuera
        End If
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
        ' Nesto#642 (ampliación): lo normal es conservar lo suyo; descartarlo es una opción aparte (y se vuelve a confirmar)
        Dim respuesta As ResultadoBoton = ConfirmacionConBotones.PreguntarTres(dialogService, "Pedido modificado",
            mensaje & vbCrLf & vbCrLf & "No se ha guardado nada. Puede recargarlo conservando sus cambios: solo se le preguntará por " &
            "lo que hayan cambiado los dos.", TEXTO_RECARGAR_CONSERVANDO, TEXTO_RECARGAR_SIN_CAMBIOS, "Ahora no")
        If respuesta = ResultadoBoton.OK Then
            Await RecargarConservandoCambiosAsync()
        ElseIf respuesta = ResultadoBoton.No Then
            Await RecargarPedidoCambiadoAsync()
        End If
    End Function

    ''' <summary>«Recargar sin mis cambios»: siempre con confirmación (se pierde todo lo que no ha guardado).</summary>
    Friend Async Function RecargarPedidoCambiadoAsync() As Task
        If pedido Is Nothing OrElse pedido.numero <= 0 Then
            Return
        End If
        Dim seguir As Boolean = Await dialogService.ShowConfirmationAsync("Recargar sin sus cambios",
            "Al recargar el pedido sin sus cambios se pierde todo lo que no ha guardado (líneas nuevas incluidas). ¿Desea recargarlo así?")
        If Not seguir Then
            Return
        End If
        Await RecargarPedidoAsync()
    End Function

    Private Async Function RecargarPedidoAsync() As Task
        If pedido Is Nothing OrElse pedido.numero <= 0 Then
            Return
        End If
        Await CargarPedidoAsync(New ResumenPedido With {.empresa = pedido.empresa, .numero = pedido.numero})
    End Function

    ''' <summary>
    ''' Nesto#642 (Carlos 17/11/26): las acciones del detalle que cambian el pedido sin recargarlo (retener, liberar, decidir la
    ''' retención por falta, la cabecera de la nota, la etiqueta pendiente, servir lo pendiente, el P.O. de la factura) pasan por
    ''' aquí para que el siguiente Guardar no dé el 409 «usted mismo»:
    ''' 1. Antes, se pregunta la versión: si ya no es la de la pantalla, lo ha cambiado otro (banda) y después NO se adopta la nueva
    '''    (se taparía lo del otro).
    ''' 2. La acción; mientras, los «pedidoCambiado» que lleguen se miran al terminar (el de la propia acción no saca la banda).
    ''' 3. Si ha ido bien y la pantalla estaba al día, se adopta la versión de después (GET …/Version).
    ''' Si la acción lanza, se propaga y no se toca la versión. Si la API no contesta la versión, se queda como estaba.
    ''' </summary>
    Friend Async Function ConVersionAlDiaAsync(Of T)(accion As Func(Of Task(Of T))) As Task(Of T)
        Dim empresa As String = pedido?.empresa
        Dim numero As Integer = If(pedido?.numero, 0)
        Dim versionPantalla As String = pedido?.Model?.Version
        Dim alDia As Boolean = False
        If numero > 0 AndAlso Not String.IsNullOrWhiteSpace(versionPantalla) Then
            Dim antes As VersionPedidoModel = Await LeerVersionSinFallarAsync(empresa, numero)
            If antes IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(antes.Version) Then
                alDia = MismaVersion(antes.Version, versionPantalla)
                If Not alDia Then
                    MostrarPedidoCambiadoPorOtro(antes.Usuario, antes.Fecha)
                End If
            End If
        End If
        EmpezarGuardadoPedido()
        Try
            Dim resultado As T = Await accion()
            If alDia AndAlso pedido?.Model IsNot Nothing AndAlso pedido.numero = numero AndAlso
                MismaVersion(pedido.Model.Version, versionPantalla) Then
                Dim despues As VersionPedidoModel = Await LeerVersionSinFallarAsync(empresa, numero)
                If despues IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(despues.Version) Then
                    pedido.Model.Version = despues.Version
                End If
            End If
            Return resultado
        Finally
            TerminarGuardadoPedido()
        End Try
    End Function

    ''' <summary>Lo mismo para las acciones que no devuelven nada.</summary>
    Friend Async Function ConVersionAlDiaAsync(accion As Func(Of Task)) As Task
        Dim unused = Await ConVersionAlDiaAsync(Of Boolean)(
            Async Function()
                Await accion()
                Return True
            End Function)
    End Function

    Private Async Function LeerVersionSinFallarAsync(empresa As String, numero As Integer) As Task(Of VersionPedidoModel)
        Try
            Return Await servicio.LeerVersionPedido(empresa, numero)
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function MismaVersion(a As String, b As String) As Boolean
        Return String.Equals(If(a, String.Empty).Trim(), If(b, String.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
    End Function
End Class
