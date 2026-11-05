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

    ''' <summary>
    ''' Nesto#620 (NestoAPI#806): lleva papel y la API NO lo ha impreso en el servidor → lo imprime Nesto.
    ''' </summary>
    Public ReadOnly Property HayQueImprimirEnLocal As Boolean
        Get
            Return ImpresionEnServidor.HayQueImprimirEnLocal(DatosImpresion)
        End Get
    End Property

    ''' <summary>
    ''' Nesto#620 (NestoAPI#806): lleva papel y la API ya lo ha impreso en el servidor → Nesto no lo imprime ni lo pregunta.
    ''' </summary>
    Public ReadOnly Property EstaImpresoEnServidor As Boolean
        Get
            Return DatosImpresion IsNot Nothing AndAlso DatosImpresion.EstaImpresoEnServidor
        End Get
    End Property
End Class
