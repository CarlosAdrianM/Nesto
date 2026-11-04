Namespace Models.Facturas
    ''' <summary>
    ''' Nesto#615 (NestoAPI#810): un envío del reparto propio sin entregar cuyo reembolso no coincide con lo que queda por cobrar
    ''' de las facturas de su pedido. Viene en FacturarRutasResponseDTO.ReembolsosDesfasados (Facturar rutas y FacturarPedido).
    ''' Caso 928723: Envio 250518, ReembolsoActual 480,02, ReembolsoFacturado 510,15.
    ''' </summary>
    Public Class ReembolsoDesfasadoDTO
        ''' <summary>Número del envío (EnviosAgencia.Numero): el {id} de PUT api/EnviosAgencias/{id}/Reembolso.</summary>
        Public Property Envio As Integer
        Public Property Pedido As Integer
        ''' <summary>Lo que lleva ahora el envío.</summary>
        Public Property ReembolsoActual As Decimal
        ''' <summary>Lo que debería llevar: lo pendiente en efectivo de las facturas del pedido.</summary>
        Public Property ReembolsoFacturado As Decimal
    End Class

    ''' <summary>Nesto#615 (NestoAPI#810): cuerpo de PUT api/EnviosAgencias/{id}/Reembolso. Reembolso = el importe nuevo.</summary>
    Public Class CambiarReembolsoEnvioDTO
        Public Property Reembolso As Decimal
        Public Property Observaciones As String
    End Class

    ''' <summary>Nesto#615 (NestoAPI#810): respuesta de PUT api/EnviosAgencias/{id}/Reembolso.</summary>
    Public Class ReembolsoEnvioCambiadoDTO
        Public Property Numero As Integer
        Public Property Pedido As Integer?
        Public Property ReembolsoAnterior As Decimal
        Public Property Reembolso As Decimal
        ''' <summary>False si el importe pedido era el que ya tenía (no se ha escrito nada).</summary>
        Public Property Cambiado As Boolean
    End Class
End Namespace
