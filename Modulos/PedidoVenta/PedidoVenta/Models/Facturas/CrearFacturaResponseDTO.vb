''' <summary>
''' DTO de respuesta para la creación de facturas
''' Incluye el número de factura y la empresa donde se facturó
''' (que puede ser diferente a la empresa original si hubo traspaso)
''' </summary>
Public Class CrearFacturaResponseDTO
    ''' <summary>
    ''' Número de la factura creada
    ''' </summary>
    Public Property NumeroFactura As String

    ''' <summary>
    ''' Empresa donde se facturó el pedido
    ''' Puede ser diferente a la empresa original si hubo traspaso a empresa espejo
    ''' </summary>
    Public Property Empresa As String

    ''' <summary>
    ''' Número del pedido facturado
    ''' </summary>
    Public Property NumeroPedido As Integer

    ''' <summary>
    ''' NestoAPI#327: avisos operativos de la facturación (p. ej. NIF no registrado en la
    ''' AEAT durante el periodo de gracia hasta el 01/12/2026). La factura SE HA creado;
    ''' hay que mostrarlos a quien factura.
    ''' </summary>
    Public Property Avisos As List(Of String) = New List(Of String)

    ''' <summary>
    ''' Nesto#620 (NestoAPI#806): la factura en papel («Factura física»… en los comentarios o en el de picking), con la misma regla que
    ''' Facturar rutas. Nothing = no lleva papel. Si DatosImpresion.ImpresoEnServidor viene relleno, la API ya la ha impreso.
    ''' </summary>
    Public Property DatosImpresion As DocumentoParaImprimir
End Class
