Imports System.Collections.Generic

Public Class CopiarFacturaResponseDTO
    Public Sub New()
        LineasCopiadas = New List(Of LineaCopiadaDTO)()
    End Sub

    ''' &lt;summary&gt;
    ''' Número del pedido donde se copiaron las líneas
    ''' &lt;/summary&gt;
    Public Property NumeroPedido As Integer

    ''' &lt;summary&gt;
    ''' Número de albarán creado (si se solicitó)
    ''' &lt;/summary&gt;
    Public Property NumeroAlbaran As Integer?

    ''' &lt;summary&gt;
    ''' Número de factura creada (si se solicitó)
    ''' &lt;/summary&gt;
    Public Property NumeroFactura As String

    ''' &lt;summary&gt;
    ''' Detalle de las líneas copiadas
    ''' &lt;/summary&gt;
    Public Property LineasCopiadas As List(Of LineaCopiadaDTO)

    ''' &lt;summary&gt;
    ''' Mensaje descriptivo del resultado
    ''' &lt;/summary&gt;
    Public Property Mensaje As String

    ''' &lt;summary&gt;
    ''' Indica si la operación fue exitosa
    ''' &lt;/summary&gt;
    Public Property Exitoso As Boolean

    ''' &lt;summary&gt;
    ''' Datos del abono (rectificativa al cliente origen).
    ''' Solo se rellena cuando CrearAbonoYCargo=true.
    ''' &lt;/summary&gt;
    Public Property Abono As OperacionFacturaDTO

    ''' &lt;summary&gt;
    ''' Datos del cargo (factura nueva al cliente destino).
    ''' Solo se rellena cuando CrearAbonoYCargo=true.
    ''' &lt;/summary&gt;
    Public Property Cargo As OperacionFacturaDTO
End Class

''' &lt;summary&gt;
''' Datos de una operación individual (abono o cargo) dentro de AbonoYCargo.
''' &lt;/summary&gt;
Public Class OperacionFacturaDTO
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property NumeroPedido As Integer
    Public Property NumeroAlbaran As Integer?
    Public Property NumeroFactura As String
    Public Property Lineas As List(Of LineaCopiadaDTO)

    Public Sub New()
        Lineas = New List(Of LineaCopiadaDTO)()
    End Sub
End Class

Public Class LineaCopiadaDTO
    Public Property NumeroLineaNueva As Integer
    Public Property Producto As String
    Public Property Descripcion As String
    Public Property FacturaOrigen As String
    Public Property LineaOrigen As Integer
    Public Property CantidadOriginal As Decimal
    Public Property CantidadCopiada As Decimal
    Public Property PrecioUnitario As Decimal
    Public Property BaseImponible As Decimal
End Class
