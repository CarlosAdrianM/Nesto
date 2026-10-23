Imports Nesto.Infrastructure.Contracts

''' <summary>
''' Nesto#552 (sugerencia 717, Manuel): una línea de pedido pendiente de entregar, tal y como la devuelve
''' GET api/PedidosVenta/PendientesEntregar. Filtrable con la barra de filtro de siempre.
''' </summary>
Public Class LineaPendienteEntregar
    Implements IFiltrableItem

    Public Property Pedido As Integer
    Public Property Linea As Integer
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property NombreCliente As String
    Public Property Vendedor As String
    Public Property Almacen As String
    Public Property FechaEntrega As Date
    Public Property Producto As String
    Public Property Texto As String
    Public Property Cantidad As Short
    Public Property Bruto As Decimal
    Public Property Usuario As String

    Public Function Contains(filtro As String) As Boolean Implements IFiltrableItem.Contains
        If String.IsNullOrWhiteSpace(filtro) Then
            Return True
        End If
        Dim f As String = filtro.Trim().ToLower()
        Return Pedido.ToString() = f OrElse
            (Cliente IsNot Nothing AndAlso Cliente.ToLower() = f) OrElse
            (Producto IsNot Nothing AndAlso Producto.ToLower() = f) OrElse
            (NombreCliente IsNot Nothing AndAlso NombreCliente.ToLower().Contains(f)) OrElse
            (Texto IsNot Nothing AndAlso Texto.ToLower().Contains(f)) OrElse
            (Vendedor IsNot Nothing AndAlso Vendedor.ToLower() = f) OrElse
            (Almacen IsNot Nothing AndAlso Almacen.ToLower() = f) OrElse
            (Usuario IsNot Nothing AndAlso Usuario.ToLower().Contains(f))
    End Function
End Class
