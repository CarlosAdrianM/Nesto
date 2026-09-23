''' <summary>
''' Nesto#483 / NestoAPI#506: lo que contesta POST api/PedidosVenta/ModoServicioSugerido, el modo de
''' servicio con el que el servidor haría nacer el pedido que se está montando, mirando el stock real de
''' sus líneas (o el que fuerce el parámetro ModoServicioPorDefecto del usuario). Los recuentos son de
''' grupos producto+almacén, no de líneas sueltas (NestoAPI#515).
''' El cliente no interpreta nada: preselecciona Modo y enseña Motivo.
''' </summary>
Public Class ModoServicioSugeridoDTO
    Public Property Modo As Byte
    Public Property Nombre As String
    Public Property LineasVerdes As Integer
    Public Property LineasRosas As Integer
    Public Property LineasRojas As Integer
    Public Property Motivo As String
    ''' <summary>NestoAPI#518: los modos que se pueden elegir para este pedido (el sugerido siempre está).
    ''' Vacío o Nothing (API anterior) = todos, como hasta ahora.</summary>
    Public Property ModosPermitidos As List(Of Byte)
    ''' <summary>NestoAPI#518: los cuatro modos con su permiso y el motivo de los que no se pueden elegir.</summary>
    Public Property Modos As List(Of ModoServicioPermitidoDTO)
End Class

''' <summary>NestoAPI#518: un modo de servicio y, si no tiene sentido para el pedido, por qué (lo redacta la API).</summary>
Public Class ModoServicioPermitidoDTO
    Public Property Modo As Byte
    Public Property Nombre As String
    Public Property Permitido As Boolean
    Public Property Motivo As String
End Class
