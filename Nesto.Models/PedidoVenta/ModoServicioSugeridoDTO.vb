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
End Class
