' Innovatrans (DataTrans DTX): agencia "registrar al imprimir". A diferencia de las clásicas, NO
' montamos la etiqueta ni numeramos en local: NestoAPI inserta el envío en la plataforma, devuelve
' el albarán y la etiqueta ZPL, y nosotros solo la mandamos a la Zebra. Toda la integración SOAP
' vive en el servidor (server-side). El flujo común está en AgenciaGestionadaPorApi (NestoAPI#493);
' aquí solo lo que distingue a Innovatrans.
Public Class AgenciaInnovatrans
    Inherits AgenciaGestionadaPorApi

    Public Overrides ReadOnly Property NombreAgencia As String
        Get
            Return "Innovatrans"
        End Get
    End Property

    Public Overrides ReadOnly Property AgenciaId As Integer
        Get
            Return 12
        End Get
    End Property

    Protected Overrides ReadOnly Property NemonicoPlaza As String
        Get
            Return "IN"
        End Get
    End Property

    ' Portal TIP-SA: id fijo de cliente (028040028040) + albarán (CodigoBarras) de DataTrans.
    Protected Overrides Function EnlaceSeguimientoDe(albaran As String) As String
        Return "https://aplicaciones.tip-sa.com/cliente/datos_env.php?id=028040028040" & albaran
    End Function
End Class
