Imports System.Threading.Tasks

Namespace Services
    ''' <summary>
    ''' Nesto#640 (NestoAPI#864): el pago a plazos de un pedido ya creado, con la financiera que diga la API
    ''' (api/PedidosVenta/{empresa}/{pedido}/PagoAPlazos). Todos lanzan una excepción con el mensaje de la API tal cual.
    ''' </summary>
    Public Interface IServicioPagoAPlazos
        ''' <summary>GET …/PagoAPlazos/Disponible: si se puede ofrecer, con quién, cuánto y por qué canal.</summary>
        Function LeerDisponible(empresa As String, pedido As Integer) As Task(Of DisponibilidadPagoAPlazosModel)
        ''' <summary>POST …/PagoAPlazos {Canal, Correo}: abre la solicitud (o reenvía el aviso de la que ya hay).</summary>
        Function Solicitar(empresa As String, pedido As Integer, canal As String, correo As String) As Task(Of SolicitudPagoAPlazosModel)
        ''' <summary>GET …/PagoAPlazos/Estado.</summary>
        Function LeerEstado(empresa As String, pedido As Integer) As Task(Of EstadoPagoAPlazosModel)
        ''' <summary>DELETE …/PagoAPlazos?motivo=: cancela la solicitud viva.</summary>
        Function Cancelar(empresa As String, pedido As Integer, motivo As String) As Task(Of EstadoPagoAPlazosModel)
    End Interface
End Namespace
