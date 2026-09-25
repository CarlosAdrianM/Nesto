''' <summary>
''' Nesto#493 / NestoAPI#542: lo que contesta POST api/PedidosVenta/ModoFacturacionSugerido: el modo de
''' facturación que rige en el pedido tal cual está en pantalla (o «al completar» si el que rige no se puede
''' elegir), y qué modos se pueden elegir. La única regla de negocio es la de los plazos de pago (si no son
''' los de la ficha del cliente, no se permite «por entregas»); una nota de entrega no admite ninguno.
''' No depende del stock. El cliente no interpreta nada: preselecciona Modo y deshabilita el resto.
''' </summary>
Public Class ModoFacturacionSugeridoDTO
    Implements ISugerenciaModos

    Public Property Modo As Byte
    Public Property Nombre As String
    ''' <summary>Por qué el modo que regía no se puede elegir. Nothing si sí se puede.</summary>
    Public Property Motivo As String
    ''' <summary>Los modos que se pueden elegir para este pedido (el sugerido siempre está).
    ''' Vacío o Nothing = todos.</summary>
    Public Property ModosPermitidos As List(Of Byte) Implements ISugerenciaModos.ModosPermitidos
    ''' <summary>Los tres modos con su permiso y el motivo de los que no se pueden elegir.</summary>
    Public Property Modos As List(Of ModoFacturacionPermitidoDTO)

    ''' <summary>Por qué no vale un modo, según la API (Nothing si no lo dice).</summary>
    Public Function MotivoDe(modo As Byte) As String Implements ISugerenciaModos.MotivoDe
        Return Modos?.FirstOrDefault(Function(m) m.Modo = modo)?.Motivo
    End Function
End Class

''' <summary>NestoAPI#542: un modo de facturación y, si no se puede elegir para el pedido, por qué (lo redacta la API).</summary>
Public Class ModoFacturacionPermitidoDTO
    Public Property Modo As Byte
    Public Property Nombre As String
    Public Property Permitido As Boolean
    Public Property Motivo As String
End Class
