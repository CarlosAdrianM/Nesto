''' <summary>
''' Nesto#493: una opción de un selector de modos del pedido (modo de servicio, modo de facturación):
''' el código que viaja a la API, el nombre que ve el usuario y la descripción para el tooltip.
''' Lo común a <see cref="ModosServicio.Lista"/> y <see cref="ModosFacturacion.Lista"/>.
''' </summary>
Public Class ModoItem
    Public Sub New(codigo As Byte, nombre As String, descripcion As String)
        Me.Codigo = codigo
        Me.Nombre = nombre
        Me.Descripcion = descripcion
    End Sub

    Public ReadOnly Property Codigo As Byte
    Public ReadOnly Property Nombre As String
    Public ReadOnly Property Descripcion As String

    Public Overrides Function ToString() As String
        Return Nombre
    End Function
End Class
