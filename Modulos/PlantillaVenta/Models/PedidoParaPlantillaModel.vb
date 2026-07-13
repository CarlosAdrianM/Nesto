''' <summary>
''' Nesto#397: el pedido YA en forma de plantilla, tal y como lo devuelve
''' GET api/PedidosVenta/ParaPlantilla. La inversión (colapsar ofertas, clasificar Ganavisiones)
''' la hace NestoAPI; aquí solo se deserializa y se mapea a BorradorPlantillaVenta.
''' </summary>
Public Class PedidoParaPlantillaModel
    Public Property Empresa As String
    Public Property Cliente As String
    Public Property Contacto As String
    ''' <summary>Nº del pedido en edición: la plantilla guardará con PUT sobre este número.</summary>
    Public Property NumeroPedido As Integer
    Public Property EsPresupuesto As Boolean
    Public Property FormaPago As String
    Public Property PlazosPago As String
    Public Property ComentarioPicking As String
    Public Property Comentarios As String
    Public Property Ruta As String
    Public Property ServirJunto As Boolean
    Public Property MantenerJunto As Boolean
    Public Property FechaEntrega As Date?
    Public Property Almacen As String
    Public Property Lineas As New List(Of LineaParaPlantillaModel)
    Public Property Regalos As New List(Of RegaloParaPlantillaModel)
End Class

Public Class LineaParaPlantillaModel
    Public Property Producto As String
    Public Property Texto As String
    Public Property Cantidad As Integer
    Public Property Precio As Decimal
    Public Property Descuento As Decimal
    Public Property AplicarDescuento As Boolean
    Public Property CantidadOferta As Integer
    Public Property PersonalizarOferta As Boolean
    Public Property PrecioOferta As Decimal
    Public Property DescuentoOferta As Decimal
    ''' <summary>Ids de LinPedidoVta originales para que el PUT actualice en vez de recrear.</summary>
    Public Property IdLineaPago As Integer
    Public Property IdLineaOferta As Integer?
    ''' <summary>
    ''' Las líneas con PICKING ya están preparadas en el almacén: no se pueden modificar ni
    ''' quitar desde la plantilla (se valida contra datos frescos antes del PUT).
    ''' </summary>
    Public Property PagoTienePicking As Boolean
    Public Property OfertaTienePicking As Boolean
End Class

Public Class RegaloParaPlantillaModel
    Public Property Producto As String
    Public Property Texto As String
    Public Property Cantidad As Integer
    Public Property IdLinea As Integer
    Public Property TienePicking As Boolean
End Class
