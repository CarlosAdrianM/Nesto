''' <summary>
''' Nesto#483 / NestoAPI#506: lo que contesta POST api/PedidosVenta/ModoServicioSugerido, el modo de
''' servicio con el que el servidor haría nacer el pedido que se está montando, mirando el stock real de
''' sus líneas (o el que fuerce el parámetro ModoServicioPorDefecto del usuario). Los recuentos son de
''' grupos producto+almacén, no de líneas sueltas (NestoAPI#515).
''' El cliente no interpreta nada: preselecciona Modo y enseña Motivo.
''' </summary>
Public Class ModoServicioSugeridoDTO
    Implements ISugerenciaModos

    Public Property Modo As Byte
    Public Property Nombre As String
    Public Property LineasVerdes As Integer
    Public Property LineasRosas As Integer
    Public Property LineasRojas As Integer
    Public Property Motivo As String
    ''' <summary>NestoAPI#518: los modos que se pueden elegir para este pedido (el sugerido siempre está).
    ''' Vacío o Nothing (API anterior) = todos, como hasta ahora.</summary>
    Public Property ModosPermitidos As List(Of Byte) Implements ISugerenciaModos.ModosPermitidos
    ''' <summary>NestoAPI#518: los cuatro modos con su permiso y el motivo de los que no se pueden elegir.</summary>
    Public Property Modos As List(Of ModoServicioPermitidoDTO)

    ''' <summary>Por qué no vale un modo, según la API (Nothing si no lo dice).</summary>
    Public Function MotivoDe(modo As Byte) As String Implements ISugerenciaModos.MotivoDe
        Return Modos?.FirstOrDefault(Function(m) m.Modo = modo)?.Motivo
    End Function

    ''' <summary>Sugerencia 1340: ¿la API ofrece este modo? Solo tiene sentido para los que van marcados
    ''' <see cref="ModoItem.SoloSiSeOfrece"/> (el 6): la API solo lo manda cuando el cliente tiene algo pendiente con
    ''' lo que juntarse o el pedido ya está en ese modo.</summary>
    Public Function Ofrece(modo As Byte) As Boolean
        Return (ModosPermitidos IsNot Nothing AndAlso ModosPermitidos.Contains(modo)) OrElse
            (Modos IsNot Nothing AndAlso Modos.Any(Function(m) m IsNot Nothing AndAlso m.Modo = modo))
    End Function

    ''' <summary>Sugerencia 1340: con qué sale junto (o que ya no queda nada), según la API. Nothing si no lo dice.</summary>
    Public Function ExplicacionDe(modo As Byte) As String
        Dim texto As String = Modos?.FirstOrDefault(Function(m) m IsNot Nothing AndAlso m.Modo = modo)?.Explicacion
        Return If(String.IsNullOrWhiteSpace(texto), Nothing, texto.Trim())
    End Function
End Class

''' <summary>NestoAPI#518: un modo de servicio y, si no tiene sentido para el pedido, por qué (lo redacta la API).</summary>
Public Class ModoServicioPermitidoDTO
    Public Property Modo As Byte
    Public Property Nombre As String
    Public Property Permitido As Boolean
    Public Property Motivo As String
    ''' <summary>Sugerencia 1340 (solo en el 6): «Saldrá junto con la nota de entrega 929701.» o, si ya no queda nada,
    ''' «Ya no queda nada pendiente del cliente: saldrá como «Todo junto».».</summary>
    Public Property Explicacion As String
    ''' <summary>Sugerencia 1340 (solo en el 6): los pedidos (y notas de entrega) con los que saldría.</summary>
    Public Property PedidosConLosQueSeJunta As List(Of Integer)
End Class
