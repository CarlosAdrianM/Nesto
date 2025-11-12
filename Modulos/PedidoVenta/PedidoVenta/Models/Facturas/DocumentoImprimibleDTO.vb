''' <summary>
''' Clase base abstracta para documentos que pueden ser impresos (facturas y albaranes).
''' Hereda de DocumentoCreadoDTO y añade la información de impresión.
''' </summary>
Public MustInherit Class DocumentoImprimibleDTO
    Inherits DocumentoCreadoDTO

    ''' <summary>
    ''' Información de impresión. Solo se rellena si el documento debe imprimirse.
    ''' Contiene: bytes del PDF, número de copias y bandeja de impresión.
    ''' </summary>
    Public Property DatosImpresion As DocumentoParaImprimir
End Class
