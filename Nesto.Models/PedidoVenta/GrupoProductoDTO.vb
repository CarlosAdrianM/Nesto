''' <summary>
''' NestoAPI#352: grupo de producto para elegir por cuál comisiona una línea de
''' inmovilizado (GET api/PedidosVenta/GruposProducto).
''' </summary>
Public Class GrupoProductoDTO
    Public Property Codigo As String
    Public Property Nombre As String
    Public ReadOnly Property TextoFormateado As String
        Get
            Return $"{Codigo} - {Nombre}"
        End Get
    End Property
End Class
