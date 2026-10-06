''' <summary>
''' Nesto#510: unidades de un producto que viajan hacia un almacén en una reposición.
''' Las devuelve GET api/Reposiciones/EnTransito?empresa&amp;almacen&amp;productos.
''' </summary>
Public Class ProductoEnTransito
    Public Property Producto As String
    Public Property Unidades As Integer
    ''' <summary>Número de traspaso; Nothing si la reposición aún está en preparación en el origen.</summary>
    Public Property Traspaso As Integer?
    Public Property Origen As String
End Class
