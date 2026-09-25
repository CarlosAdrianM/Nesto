''' <summary>
''' Nesto#493 / NestoAPI#542: el modo de facturación del pedido, que sustituye a la casilla «Mantener junto».
''' Réplica de Constantes.Pedidos.ModosFacturacion de NestoAPI. El servidor normaliza en POST/PUT:
''' sin modo deriva de mantenerJunto (sobre el modo ya guardado, para no pisar un 3); con modo, mantenerJunto
''' pasa a ser su derivado (solo el 2 es True) y un modo no permitido se rechaza con 400.
''' </summary>
Public NotInheritable Class ModosFacturacion
    Private Sub New()
    End Sub

    ''' <summary>Cada albarán lleva su factura (la antigua casilla «Mantener junto» desmarcada).</summary>
    Public Const POR_ENTREGAS As Byte = 1
    ''' <summary>Una sola factura cuando esté todo el pedido servido (la antigua casilla marcada).</summary>
    Public Const AL_COMPLETAR As Byte = 2
    ''' <summary>Se factura el pedido entero con el primer albarán; lo que no se entrega queda «a recoger»
    ''' y pasa a una nota de entrega (ya facturada) que hereda el modo de servicio.</summary>
    Public Const TODO_AHORA_Y_LO_PENDIENTE_DESPUES As Byte = 3

    Private Shared ReadOnly _lista As IReadOnlyList(Of ModoItem) = New List(Of ModoItem) From {
        New ModoItem(POR_ENTREGAS, "Por entregas", "Cada albarán lleva su factura (la antigua casilla «Mantener junto» desmarcada)."),
        New ModoItem(AL_COMPLETAR, "Al completar el pedido", "Una sola factura cuando esté todo el pedido servido (la antigua casilla «Mantener junto» marcada)."),
        New ModoItem(TODO_AHORA_Y_LO_PENDIENTE_DESPUES, "Todo ahora, lo pendiente se entrega después", "Se factura el pedido entero con la primera entrega; lo que no haya queda a recoger y sale después en una nota de entrega, ya facturada.")
    }

    ''' <summary>Los modos que se pueden elegir en pantalla, en el orden del selector.</summary>
    Public Shared ReadOnly Property Lista As IReadOnlyList(Of ModoItem)
        Get
            Return _lista
        End Get
    End Property

    ''' <summary>
    ''' El modo que rige de verdad (misma regla que NestoAPI): mantenerJunto marcado SIEMPRE es «al completar»;
    ''' desmarcado, rige el 3 si está guardado (es lo único que el bit no puede decir) o, si no, el 1.
    ''' El bool es la autoridad del «al completar» porque el Nesto viejo, NestoApp y los triggers de plazos
    ''' solo saben hablar en bool.
    ''' </summary>
    Public Shared Function Efectivo(modoFacturacion As Byte?, mantenerJunto As Boolean) As Byte
        If mantenerJunto Then
            Return AL_COMPLETAR
        End If
        If modoFacturacion.HasValue AndAlso modoFacturacion.Value = TODO_AHORA_Y_LO_PENDIENTE_DESPUES Then
            Return TODO_AHORA_Y_LO_PENDIENTE_DESPUES
        End If
        Return POR_ENTREGAS
    End Function

    Public Shared Function EsValido(modo As Byte) As Boolean
        Return modo >= POR_ENTREGAS AndAlso modo <= TODO_AHORA_Y_LO_PENDIENTE_DESPUES
    End Function

    ''' <summary>Solo el modo 2 es «mantener junto» en el sentido de la columna y de la facturación.</summary>
    Public Shared Function EsAlCompletar(modo As Byte) As Boolean
        Return modo = AL_COMPLETAR
    End Function

    Public Shared Function EsTodoAhora(modo As Byte) As Boolean
        Return modo = TODO_AHORA_Y_LO_PENDIENTE_DESPUES
    End Function

    Public Shared Function Nombre(modo As Byte) As String
        Dim item = _lista.FirstOrDefault(Function(m) m.Codigo = modo)
        Return If(item?.Nombre, $"Modo {modo}")
    End Function

    ''' <summary>Aviso fijo del modo 3 (Carlos, 25/09/26): el cliente paga la factura entera ya, así que el mínimo
    ''' de portes se mira sobre el pedido completo; si no llega, se pueden cobrar portes.</summary>
    Public Const AVISO_PORTES_TODO_AHORA As String = "Se factura todo ahora; si el pedido no llega al mínimo de portes pagados, se cobrarán portes."
End Class
