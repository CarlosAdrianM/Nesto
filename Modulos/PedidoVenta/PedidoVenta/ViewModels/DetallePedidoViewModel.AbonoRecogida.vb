Imports CommunityToolkit.Mvvm.Input
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

''' <summary>
''' Nesto#581 (NestoAPI#760, corte 4 de NestoAPI#755): cuando el repartidor trae el bulto con la etiqueta de recogida
''' NR{Pedido}-{N}, Alfredo o Santiago la leen aquí («Leer etiqueta de recogida…»), la API crea el pedido de ABONO (serie RV,
''' sin líneas) del cliente de la nota y Nesto lo abre en MODO ABONO: cada línea nueva nace con cantidad −1 y el cursor va a
''' Producto para ir leyendo con el lector lo que viene. La factura se hace como con cualquier abono («Crear albarán y factura»).
''' Leer otra vez la misma nota (otro bulto) abre el mismo abono.
''' </summary>
Partial Public Class DetallePedidoViewModel

    Public Property LeerEtiquetaRecogidaCommand As AsyncRelayCommand

    ''' <summary>La vista pone el foco en Producto de la línea nueva (pestaña Líneas).</summary>
    Public Event FocoEnProductoSolicitado As EventHandler

    Private _abonoRecogida As AbonoRecogidaDTO

    Private Sub InicializarAbonoRecogida()
        LeerEtiquetaRecogidaCommand = New AsyncRelayCommand(AddressOf LeerEtiquetaRecogidaAsync, Function() PuedeLeerEtiquetaRecogida)
    End Sub

    ''' <summary>Los mismos que crean notas de recogida: Almacén, Administración, Dirección e Informática (como la API).</summary>
    Public ReadOnly Property PuedeLeerEtiquetaRecogida As Boolean
        Get
            Return EsGrupoQuePuedeCrearNotas
        End Get
    End Property

    ''' <summary>El pedido en pantalla es el abono de una recogida que se acaba de leer (otro pedido en pantalla lo apaga).</summary>
    Public ReadOnly Property EsModoAbonoRecogida As Boolean
        Get
            Return _abonoRecogida IsNot Nothing AndAlso pedido IsNot Nothing AndAlso
                pedido.numero = _abonoRecogida.Pedido AndAlso
                String.Equals(If(pedido.empresa, String.Empty).Trim(), If(_abonoRecogida.Empresa, String.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    ''' <summary>El aviso fijo de la cabecera: «Abono por recogida de la nota 929554 — {texto}».</summary>
    Public ReadOnly Property TextoAvisoAbonoRecogida As String
        Get
            If Not EsModoAbonoRecogida Then
                Return Nothing
            End If
            Dim texto As String = $"Abono por recogida de la nota {_abonoRecogida.PedidoNota}"
            If Not String.IsNullOrWhiteSpace(_abonoRecogida.TextoNota) Then
                texto &= " — " & _abonoRecogida.TextoNota.Trim()
            End If
            If Not String.IsNullOrWhiteSpace(_abonoRecogida.FacturaOriginal) Then
                texto &= $" (factura {_abonoRecogida.FacturaOriginal.Trim()})"
            End If
            Return texto & ". Lea los productos que vienen: cada línea entra con cantidad −1."
        End Get
    End Property

    Friend Sub NotificarCambioAbonoRecogida()
        OnPropertyChanged(NameOf(EsModoAbonoRecogida))
        OnPropertyChanged(NameOf(TextoAvisoAbonoRecogida))
        OnPropertyChanged(NameOf(PuedeLeerEtiquetaRecogida))
        LeerEtiquetaRecogidaCommand?.NotifyCanExecuteChanged()
    End Sub

    ''' <summary>
    ''' Lo que la vista hace con cada línea que crea la rejilla: en modo abono, de producto y con cantidad −1 (hoy nacen con 1).
    ''' </summary>
    Public Sub PrepararLineaNueva(linea As LineaPedidoVentaWrapper)
        If linea Is Nothing OrElse Not EsModoAbonoRecogida Then
            Return
        End If
        linea.tipoLinea = CByte(1)
        linea.Cantidad = -1
    End Sub

    ''' <summary>«Leer etiqueta de recogida…»: el lector escribe el código y termina en Intro (o se escribe NR929554 a mano).</summary>
    Friend Async Function LeerEtiquetaRecogidaAsync() As Task
        If Not PuedeLeerEtiquetaRecogida Then
            Return
        End If
        If TieneCambiosSinGuardar AndAlso
            Not Await dialogService.ShowConfirmationAsync("Cambios sin guardar",
                "El pedido que tiene en pantalla tiene cambios sin guardar y se perderán." & vbCrLf & vbCrLf & "¿Quiere leer la etiqueta de recogida igualmente?") Then
            Return
        End If
        Dim codigo As String = dialogService.GetCodigoBarras("Leer etiqueta de recogida",
            "Lea con el lector la etiqueta del bulto (NR…) o escriba el número de la nota de recogida (por ejemplo, NR929554).")
        If String.IsNullOrWhiteSpace(codigo) Then
            Return
        End If
        Await AbrirAbonoDeRecogidaAsync(codigo.Trim())
    End Function

    ''' <summary>
    ''' GET de la recogida → confirmación con la nota (si aún no tiene abono) → POST (con la pregunta y Forzar si el
    ''' repartidor no la ha marcado como recogida) → carga el abono en modo abono.
    ''' </summary>
    Friend Async Function AbrirAbonoDeRecogidaAsync(codigo As String) As Task
        Dim recogida As RecogidaDTO
        Try
            recogida = Await servicio.LeerRecogida(codigo).ConfigureAwait(True)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return
        End Try
        If recogida Is Nothing Then
            dialogService.ShowError($"No se ha encontrado la recogida {codigo}.")
            Return
        End If

        If Not recogida.PedidoAbono.HasValue AndAlso
            Not Await dialogService.ShowConfirmationAsync("Crear abono", DescribirRecogida(recogida)).ConfigureAwait(True) Then
            Return
        End If

        Dim abono As AbonoRecogidaDTO = Await CrearAbonoPreguntandoAsync(codigo, recogida.PedidoNota).ConfigureAwait(True)
        If abono Is Nothing Then
            Return
        End If

        Await CargarPedidoAsync(New ResumenPedido With {.empresa = abono.Empresa, .numero = abono.Pedido}).ConfigureAwait(True)
        If pedido Is Nothing OrElse pedido.numero <> abono.Pedido Then
            Return ' la carga ha fallado (ya ha dicho por qué)
        End If
        _abonoRecogida = abono
        NotificarCambioAbonoRecogida()
        If abono.YaExistia Then
            dialogService.ShowNotification("Abono por recogida", $"El abono de la nota {abono.PedidoNota} ya estaba creado: es el pedido {abono.Pedido}.")
        End If
        RaiseEvent FocoEnProductoSolicitado(Me, EventArgs.Empty)
    End Function

    Private Async Function CrearAbonoPreguntandoAsync(codigo As String, pedidoNota As Integer) As Task(Of AbonoRecogidaDTO)
        Dim noRecogida As RecogidaNoRecogidaException = Nothing
        Try
            Return Await servicio.CrearAbonoRecogida(codigo, False).ConfigureAwait(True)
        Catch ex As RecogidaNoRecogidaException
            noRecogida = ex
        Catch ex As Exception
            dialogService.ShowError($"No se ha podido crear el abono: {ex.Message}")
            Return Nothing
        End Try
        If Not Await dialogService.ShowConfirmationAsync("Recogida sin marcar",
            $"La recogida de la nota {pedidoNota} no figura como recogida ({noRecogida.EstadoLegible}). ¿Crear el abono igualmente?").ConfigureAwait(True) Then
            Return Nothing
        End If
        Try
            Return Await servicio.CrearAbonoRecogida(codigo, True).ConfigureAwait(True)
        Catch ex As Exception
            dialogService.ShowError($"No se ha podido crear el abono: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>Lo que se enseña antes de crear el abono: cliente, texto, bultos, factura y quién la entregó y cuándo.</summary>
    Friend Shared Function DescribirRecogida(recogida As RecogidaDTO) As String
        Dim texto As New System.Text.StringBuilder()
        texto.AppendLine($"Nota de recogida {recogida.PedidoNota}")
        texto.AppendLine($"Cliente: {recogida.Cliente?.Trim()}/{recogida.Contacto?.Trim()}" &
                         If(String.IsNullOrWhiteSpace(recogida.Nombre), String.Empty, " — " & recogida.Nombre.Trim()))
        If Not String.IsNullOrWhiteSpace(recogida.TextoNota) Then
            texto.AppendLine($"Texto: {recogida.TextoNota.Trim()}")
        End If
        texto.AppendLine($"Bultos: {recogida.Bultos}")
        texto.AppendLine("Factura original: " & If(String.IsNullOrWhiteSpace(recogida.FacturaOriginal), "(ninguna; se vinculará por las últimas compras)", recogida.FacturaOriginal.Trim()))
        If recogida.EstadoEnvio = "Entregado" Then
            Dim quien As String = If(String.IsNullOrWhiteSpace(recogida.Receptor), String.Empty, $" (la entregó {recogida.Receptor.Trim()})")
            Dim cuando As String = If(recogida.FechaRecogida.HasValue, $" el {recogida.FechaRecogida.Value:dd/MM/yyyy} a las {recogida.FechaRecogida.Value:HH:mm}", String.Empty)
            texto.AppendLine($"Recogida{cuando}{quien}.")
        Else
            texto.AppendLine("El repartidor aún no la ha marcado como recogida" & If(String.IsNullOrWhiteSpace(recogida.EstadoEnvio), " (sin envío)", $" (estado {recogida.EstadoEnvio})") & ".")
        End If
        texto.AppendLine()
        texto.Append("¿Crear el pedido de abono?")
        Return texto.ToString()
    End Function

End Class
