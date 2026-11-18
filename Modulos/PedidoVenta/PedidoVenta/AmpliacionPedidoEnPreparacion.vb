Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models

''' <summary>
''' Caso 931007 (NestoAPI, Carlos 18/11/26): al añadir productos a un pedido EN PREPARACIÓN la API contesta PEDIDO_EN_PREPARACION sin
''' guardar nada. Se enseña su mensaje con «Hacer un pedido nuevo con lo añadido», «Pedir a Almacén que lo meta en el envío de hoy» (solo si
''' la API lo ofrece) y «Cancelar». Lo usan el detalle del pedido y la plantilla (modificar y ampliar).
''' </summary>
Public NotInheritable Class AmpliacionPedidoEnPreparacion
    Private Sub New()
    End Sub

    Public Const TITULO As String = "El pedido ya está en preparación"
    Public Const TEXTO_PEDIDO_NUEVO As String = "Hacer un pedido nuevo con lo añadido"
    Public Const TEXTO_PEDIR_ALMACEN As String = "Pedir a Almacén que lo meta en el envío de hoy"
    Public Const TEXTO_CANCELAR As String = "Cancelar"
    Public Const TITULO_PEDIDO_NUEVO As String = "Pedido nuevo con lo añadido"

    ''' <summary>«PedidoNuevo», «PedirAlmacen» o Nothing (ha cancelado).</summary>
    Public Shared Function Elegir(dialogos As IServicioDialogos, rechazo As PedidoEnPreparacionException) As String
        If rechazo Is Nothing Then
            Return Nothing
        End If
        If rechazo.PuedePedirAlmacen Then
            Select Case ConfirmacionConBotones.PreguntarTres(dialogos, TITULO, rechazo.Message, TEXTO_PEDIDO_NUEVO, TEXTO_PEDIR_ALMACEN, TEXTO_CANCELAR)
                Case ResultadoBoton.OK
                    Return PedidoEnPreparacionException.OPCION_PEDIDO_NUEVO
                Case ResultadoBoton.No
                    Return PedidoEnPreparacionException.OPCION_PEDIR_ALMACEN
                Case Else
                    Return Nothing
            End Select
        End If
        Return If(ConfirmacionConBotones.Preguntar(dialogos, TITULO, rechazo.Message, TEXTO_PEDIDO_NUEVO, TEXTO_CANCELAR),
                  PedidoEnPreparacionException.OPCION_PEDIDO_NUEVO, Nothing)
    End Function

    ''' <summary>El pedido nuevo de los avisos del PUT (Nothing si no lo hay).</summary>
    Public Shared Function PedidoNuevo(avisos As IEnumerable(Of AvisoPedidoModel)) As Integer?
        Return AmpliacionEnPreparacionPreguntando.PedidoNuevoDeLosAvisos(
            If(avisos, Enumerable.Empty(Of AvisoPedidoModel)()).Where(Function(a) a IsNot Nothing).Select(Function(a) (a.Tipo, a.Datos)))
    End Function

    ''' <summary>
    ''' Tras guardar con «PedidoNuevo»: el mensaje de la API (número, portes, prepago) y «¿Desea abrirlo?». Abre el pedido nuevo si dice
    ''' que sí. Devuelve True si lo ha abierto.
    ''' </summary>
    Public Shared Async Function OfrecerAbrirAsync(dialogos As IServicioDialogos, abridor As ControlesUsuario.Dialogs.IAbridorPedidos, empresa As String,
                                                   pedidoNuevo As Integer, mensaje As String) As Task(Of Boolean)
        Dim texto As String = If(String.IsNullOrWhiteSpace(mensaje), $"Lo añadido va en el pedido nuevo {pedidoNuevo}.", mensaje.Trim()) &
            vbCrLf & vbCrLf & $"¿Desea abrir el pedido {pedidoNuevo}?"
        If Not Await dialogos.ShowConfirmationAsync(TITULO_PEDIDO_NUEVO, texto) Then
            Return False
        End If
        abridor?.Abrir(empresa?.Trim(), pedidoNuevo)
        Return True
    End Function
End Class
