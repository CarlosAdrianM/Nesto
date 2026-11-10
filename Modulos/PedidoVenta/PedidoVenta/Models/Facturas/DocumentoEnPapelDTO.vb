Imports System.Collections.Generic

''' <summary>
''' NestoAPI#828: un documento en papel (factura física, albarán físico o nota en papel) que sale en el packing, al hacer la
''' etiqueta del envío. Lo devuelve POST api/FacturacionRutas/FacturarEtiqueta y, en Facturar rutas, las listas del packing.
''' </summary>
Public Class DocumentoEnPapelDTO
    Public Const ESTADO_IMPRESO As String = "Impreso"
    Public Const ESTADO_PENDIENTE As String = "Pendiente"
    Public Const TIPO_FACTURA As String = "Factura"
    Public Const TIPO_ALBARAN As String = "Albaran"
    Public Const TIPO_NOTA_ENTREGA As String = "NotaEntrega"

    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property Envio As Integer?
    ''' <summary>Factura, Albaran o NotaEntrega.</summary>
    Public Property Tipo As String
    ''' <summary>El número de la factura, del albarán o (en la nota) el del pedido.</summary>
    Public Property Documento As String
    ''' <summary>Impreso o Pendiente.</summary>
    Public Property Estado As String
    Public Property Impresora As String
    Public Property Mensaje As String
    Public Property YaEstabaImpreso As Boolean
    Public Property Reimpreso As Boolean
    ''' <summary>«la factura NV2617986», «el albarán 123456»…</summary>
    Public Property Descripcion As String

    Public ReadOnly Property EstaPendiente As Boolean
        Get
            Return String.Equals(Estado, ESTADO_PENDIENTE, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property
End Class

''' <summary>
''' NestoAPI#828: la respuesta de POST api/FacturacionRutas/FacturarEtiqueta: la de FacturarPedido más los documentos en papel del
''' packing (que Nesto imprime en local y confirma) y los avisos del núcleo (la ruta propia no se factura aquí, lo ya impreso…).
''' </summary>
Public Class FacturarEtiquetaResponseDTO
    Inherits FacturarRutasResponseDTO

    Public Property DocumentosEnPapel As New List(Of DocumentoEnPapelDTO)
    Public Property AvisosPapel As New List(Of String)
End Class

''' <summary>NestoAPI#828: el cuerpo de POST api/FacturacionRutas/DocumentosEnPapel/Impresion (tras imprimir en local).</summary>
Public Class ImpresionDocumentoEnPapelDTO
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property Envio As Integer?
    Public Property Tipo As String
    Public Property Documento As String
    Public Property Impreso As Boolean
    Public Property Impresora As String
    Public Property Mensaje As String
End Class
