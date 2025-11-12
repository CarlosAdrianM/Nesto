Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas

''' <summary>
''' Servicio para facturación masiva de pedidos por rutas
''' </summary>
Public Interface IServicioFacturacionRutas
    ''' <summary>
    ''' Factura pedidos por rutas (propia o agencias)
    ''' </summary>
    ''' <param name="request">Parámetros de facturación (tipo ruta, fecha desde)</param>
    ''' <returns>Resultado con contadores y errores</returns>
    Function FacturarRutas(request As FacturarRutasRequestDTO) As Task(Of FacturarRutasResponseDTO)
End Interface
