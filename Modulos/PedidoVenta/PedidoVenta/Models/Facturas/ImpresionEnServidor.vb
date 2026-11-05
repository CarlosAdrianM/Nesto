Imports System.Collections.Generic
Imports System.Linq

''' <summary>
''' Nesto#620 (NestoAPI#806): la API imprime en el servidor (interruptor Facturacion:ImprimirDocumentosEnServidor) el documento en
''' papel al facturar y lo dice en DatosImpresion.ImpresoEnServidor (la impresora). Una sola regla y un solo texto para los tres
''' sitios que imprimen en Nesto (Facturar rutas, Agencias y el detalle del pedido), para que ninguno lo imprima dos veces.
''' </summary>
Public Module ImpresionEnServidor
    ''' <summary>Lo que dice el parámetro ImpresoraDocumentos cuando es la impresora predeterminada de Windows en el servidor.</summary>
    Public Const IMPRESORA_PREDETERMINADA As String = "(predeterminada)"

    ''' <summary>¿Lleva papel y no lo ha impreso el servidor? Entonces lo imprime Nesto.</summary>
    Public Function HayQueImprimirEnLocal(datos As DocumentoParaImprimir) As Boolean
        Return datos IsNot Nothing AndAlso Not datos.EstaImpresoEnServidor
    End Function

    ''' <summary>«en «HP LaserJet 600»» (o la predeterminada del servidor); varias, separadas por comas.</summary>
    Public Function DondeSeHaImpreso(impresoras As IEnumerable(Of String)) As String
        Dim nombres = If(impresoras, Enumerable.Empty(Of String)()).
            Where(Function(i) Not String.IsNullOrWhiteSpace(i)).
            Select(Function(i) i.Trim()).
            Distinct(StringComparer.OrdinalIgnoreCase).
            Select(Function(i) If(String.Equals(i, IMPRESORA_PREDETERMINADA, StringComparison.OrdinalIgnoreCase),
                                  "la impresora predeterminada del servidor", $"«{i}»")).
            ToList()
        If Not nombres.Any() Then
            Return String.Empty
        End If
        Return "en " & String.Join(", ", nombres)
    End Function

    ''' <summary>
    ''' «N documento(s) impresos en el servidor en «X»», o vacío si ninguno vino impreso.
    ''' </summary>
    Public Function TextoImpresos(documentos As IEnumerable(Of DocumentoParaImprimir)) As String
        Dim impresos = If(documentos, Enumerable.Empty(Of DocumentoParaImprimir)()).
            Where(Function(d) d IsNot Nothing AndAlso d.EstaImpresoEnServidor).ToList()
        If Not impresos.Any() Then
            Return String.Empty
        End If
        Return $"{impresos.Count} documento(s) impresos en el servidor {DondeSeHaImpreso(impresos.Select(Function(d) d.ImpresoEnServidor))}"
    End Function
End Module
