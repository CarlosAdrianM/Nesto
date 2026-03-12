Public Class ResultadoPortesDTO
    Public Property ImportePortes As Decimal
    Public Property ComisionReembolso As Decimal
    Public Property ImporteMinimoPedidoSinPortes As Decimal
    Public Property ImporteActualPedido As Decimal
    Public Property ImporteFaltaParaPortesGratis As Decimal
    Public Property PortesGratis As Boolean
    Public Property EsContraReembolso As Boolean
    Public Property CuentaPortes As String
    Public Property CuentaReembolso As String
End Class

Public Class PedidoPortesInputDTO
    Public Property CodigoPostal As String
    Public Property Ruta As String
    Public Property FormaPago As String
    Public Property PlazosPago As String
    Public Property CCC As String
    Public Property PeriodoFacturacion As String
    Public Property NotaEntrega As Boolean
    Public Property EsTiendaOnline As Boolean
    Public Property EsPrecioPublicoFinal As Boolean
    Public Property Iva As String
    Public Property BaseImponibleProductos As Decimal
    Public Property AnadirPortes As Boolean = True
End Class
