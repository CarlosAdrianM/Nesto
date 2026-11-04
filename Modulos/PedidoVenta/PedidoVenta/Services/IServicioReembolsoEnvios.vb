Imports System.Threading.Tasks
Imports Nesto.Modulos.PedidoVenta.Models.Facturas

Namespace Services
    ''' <summary>
    ''' Nesto#615 (NestoAPI#810): cambia el reembolso de un envío del reparto propio sin entregar
    ''' (PUT api/EnviosAgencias/{id}/Reembolso): solo el campo, con historia y sin contabilidad.
    ''' </summary>
    Public Interface IServicioReembolsoEnvios
        ''' <summary>Lanza una excepción con el mensaje de la API si no se puede (400 no es reparto propio o negativo, 409 ya entregado).</summary>
        Function CambiarReembolso(envio As Integer, datos As CambiarReembolsoEnvioDTO) As Task(Of ReembolsoEnvioCambiadoDTO)
    End Interface
End Namespace
