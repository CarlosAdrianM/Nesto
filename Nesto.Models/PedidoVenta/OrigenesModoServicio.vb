''' <summary>
''' NestoAPI#704 (caso 929152): de dónde salió el modo de servicio con el que se graba el pedido. Réplica de
''' OrigenesModoServicio de NestoAPI. La API no cambia nada con esto: solo lo registra junto al modo enviado,
''' el guardado y su propia sugerencia, para saber si un modo «raro» lo eligió el vendedor o venía puesto.
''' </summary>
Public NotInheritable Class OrigenesModoServicio
    Private Sub New()
    End Sub

    ''' <summary>Lo eligió el vendedor en el selector.</summary>
    Public Const USUARIO As String = "Usuario"
    ''' <summary>La sugerencia del servidor (ModoServicioSugerido), aplicada sin que el vendedor la tocara.</summary>
    Public Const SUGERIDO As String = "Sugerido"
    ''' <summary>El parámetro ModoServicioPorDefecto del usuario (o el defecto del cliente), sin sugerencia ni elección.</summary>
    Public Const POR_DEFECTO As String = "PorDefecto"
    ''' <summary>El vendedor eligió uno y Nesto lo cambió avisando (Nesto#484: ya no se podía elegir, o el servidor lo rechazó).</summary>
    Public Const CORREGIDO As String = "Corregido"
End Class
