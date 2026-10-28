Imports System.Collections.Generic

''' <summary>
''' NestoAPI#698: la retención de un pedido «hasta nuevo aviso» (tabla PedidosRetenidos). Viene en
''' <see cref="PedidoVentaDTO.retencion"/> mientras el pedido está retenido (modoServicio = 5) y es la respuesta de
''' POST PedidosVenta/{empresa}/{numero}/Retener y /Liberar. Mismos nombres que el DTO de la API.
''' </summary>
Public Class RetencionPedidoDTO
    Public Property Id As Integer
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property Motivo As String
    ''' <summary>Nothing = hasta nuevo aviso.</summary>
    Public Property HastaFecha As Date?
    ''' <summary>El modo de servicio que tenía el pedido al retenerlo; al liberarlo vuelve a él.</summary>
    Public Property ModoServicioAnterior As Byte?
    ''' <summary>Quién lo retuvo, tal como lo resolvió el servidor (puede venir con dominio: NUEVAVISION\Alfredo).</summary>
    Public Property Usuario As String
    Public Property Fecha As Date
    Public Property Liberado As Date?
    Public Property UsuarioLiberacion As String
    Public Property MotivoLiberacion As String
    ''' <summary>Solo en la respuesta de Retener (NestoAPI#698, 28/10/26): los pickings que ya tenía el pedido en el almacén (vacía si ninguno).</summary>
    Public Property PickingsEnCurso As List(Of Integer) = New List(Of Integer)
    ''' <summary>Solo en la respuesta de Retener: lo que hay que enseñar al usuario (ya tenía picking; se ha avisado al almacén). Nothing si nada.</summary>
    Public Property Aviso As String
End Class
