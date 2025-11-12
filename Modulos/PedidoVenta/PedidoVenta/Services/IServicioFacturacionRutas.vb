Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports System.Threading.Tasks

Namespace Services
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

        ''' <summary>
        ''' Genera un preview (simulación) de facturación SIN crear nada en la BD.
        ''' Muestra qué albaranes, facturas y notas de entrega se crearían con sus importes.
        ''' </summary>
        ''' <param name="request">Parámetros de facturación (tipo ruta, fecha desde)</param>
        ''' <returns>Preview con contadores, bases imponibles y muestra de pedidos</returns>
        Function PreviewFacturarRutas(request As FacturarRutasRequestDTO) As Task(Of PreviewFacturacionRutasResponseDTO)

        ''' <summary>
        ''' Obtiene la lista de tipos de ruta disponibles
        ''' </summary>
        ''' <returns>Lista de tipos de ruta con ID y nombre</returns>
        Function ObtenerTiposRuta() As Task(Of List(Of TipoRutaInfoDTO))
    End Interface
End Namespace
