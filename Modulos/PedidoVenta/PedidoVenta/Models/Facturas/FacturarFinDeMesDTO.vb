Imports System
Imports System.Collections.Generic

Namespace Models.Facturas
    ''' <summary>
    ''' NestoAPI#727: body de POST api/FacturacionRutas/FinDeMes (mismo contrato que FacturarFinDeMesRequestDTO de la API).
    ''' </summary>
    Public Class FacturarFinDeMesRequestDTO
        ''' <summary>Por defecto (Nothing), la empresa por defecto.</summary>
        Public Property Empresa As String
        ''' <summary>yyyy-MM-dd. Entran los albaranes de ese día incluido. Nothing = último día del mes anterior.</summary>
        Public Property FechaCorte As String
        ''' <summary>Opcional: solo este cliente.</summary>
        Public Property Cliente As String
        ''' <summary>Opcional (exige Cliente): solo los pedidos de este contacto.</summary>
        Public Property Contacto As String
    End Class

    ''' <summary>
    ''' NestoAPI#727: resultado de facturar el fin de mes (ResultadoFacturacionFinDeMes de la API). Cada cliente va aislado:
    ''' un error en uno no para a los demás.
    ''' </summary>
    Public Class ResultadoFacturacionFinDeMesDTO
        Public Property Empresa As String
        Public Property FechaCorte As DateTime
        Public Property Facturas As List(Of FacturaFinDeMesDTO) = New List(Of FacturaFinDeMesDTO)
        ''' <summary>Clientes que no se han tocado y por qué (no hay nada que mover, P.O. obligatorio, etc.).</summary>
        Public Property Saltados As List(Of ClienteFinDeMesSaltadoDTO) = New List(Of ClienteFinDeMesSaltadoDTO)
        Public Property Errores As List(Of ErrorFinDeMesDTO) = New List(Of ErrorFinDeMesDTO)
    End Class

    Public Class FacturaFinDeMesDTO
        Public Property Empresa As String
        Public Property Cliente As String
        Public Property PedidoDestino As Integer
        Public Property PedidosReunidos As List(Of Integer) = New List(Of Integer)
        Public Property LineasMovidas As Integer
        Public Property NumeroFactura As String
        ''' <summary>Algo que revisar aunque la factura esté hecha (o Nothing).</summary>
        Public Property Aviso As String

        Public ReadOnly Property TextoPedidosReunidos As String
            Get
                Return If(PedidosReunidos Is Nothing, String.Empty, String.Join(", ", PedidosReunidos))
            End Get
        End Property
    End Class

    Public Class ClienteFinDeMesSaltadoDTO
        Public Property Empresa As String
        Public Property Cliente As String
        Public Property Pedidos As List(Of Integer) = New List(Of Integer)
        Public Property Motivo As String

        Public ReadOnly Property TextoPedidos As String
            Get
                Return If(Pedidos Is Nothing, String.Empty, String.Join(", ", Pedidos))
            End Get
        End Property
    End Class

    Public Class ErrorFinDeMesDTO
        Public Property Empresa As String
        Public Property Cliente As String
        ''' <summary>El pedido en el que ya se han reunido los albaranes (si el error es al facturar), o Nothing.</summary>
        Public Property PedidoDestino As Integer?
        Public Property Mensaje As String
    End Class
End Namespace
