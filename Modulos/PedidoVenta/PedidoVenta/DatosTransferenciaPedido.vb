Imports System.Windows
Imports Nesto.Infrastructure.Shared

''' <summary>
''' Sugerencia 396 de Novedades (Paloma): en un pedido prepago por transferencia, copiar de una vez los
''' datos para que el cliente la haga (IBAN, beneficiario, concepto e importe). Los datos y el texto los
''' decide la API (GET PedidosVenta/{empresa}/{numero}/DatosTransferencia); aquí solo cuándo se ofrece.
''' </summary>
Public NotInheritable Class DatosTransferenciaPedido
    Private Sub New()
    End Sub

    Public Const TITULO As String = "Datos de transferencia"
    Public Const MENSAJE_COPIADOS As String = "Datos de transferencia copiados al portapapeles:"

    ''' <summary>Solo con forma de pago transferencia (TRN) y plazos prepago (PRE).</summary>
    Public Shared Function EsPrepagoPorTransferencia(formaPago As String, plazosPago As String) As Boolean
        Return String.Equals(formaPago?.Trim(), Constantes.FormasPago.TRANSFERENCIA, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(plazosPago?.Trim(), Constantes.PlazosPago.PREPAGO, StringComparison.OrdinalIgnoreCase)
    End Function
End Class

''' <summary>Respuesta de GET PedidosVenta/{empresa}/{numero}/DatosTransferencia.</summary>
Public Class DatosTransferenciaPedidoModel
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property Cliente As String
    Public Property Iban As String
    Public Property Titular As String
    Public Property Concepto As String
    Public Property Importe As Decimal
    ''' <summary>Los datos listos para pegar en un correo o un WhatsApp, una línea por dato.</summary>
    Public Property Texto As String
End Class

''' <summary>Escribir texto en el portapapeles, detrás de una interfaz para probar el ViewModel sin Clipboard.</summary>
Public Interface IPortapapelesTexto
    Sub CopiarTexto(texto As String)
End Interface

Public Class PortapapelesTextoWpf
    Implements IPortapapelesTexto

    Public Sub CopiarTexto(texto As String) Implements IPortapapelesTexto.CopiarTexto
        Clipboard.SetText(texto)
    End Sub
End Class
