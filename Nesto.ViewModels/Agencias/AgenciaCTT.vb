' CTT Express (NestoAPI#493): agencia "registrar al imprimir", mismo flujo que Innovatrans. La
' integración con la API de CTT vive en NestoAPI; aquí solo lo que la distingue.
'
' TODAVÍA NO ESTÁ EN EL FACTORY de AgenciasViewModel: mientras CTT sea agencia sombra
' (AgenciasTransporte.EsSombra = 1) no debe aparecer en el desplegable. Al salir a producción:
' factory.Add("CTT", Function() New AgenciaCTT()) y añadir "CTT" en EsAgenciaDelComparador.
Public Class AgenciaCTT
    Inherits AgenciaGestionadaPorApi

    Public Overrides ReadOnly Property NombreAgencia As String
        Get
            Return "CTT"
        End Get
    End Property

    Public Overrides ReadOnly Property AgenciaId As Integer
        Get
            Return 13
        End Get
    End Property

    Protected Overrides ReadOnly Property NemonicoPlaza As String
        Get
            Return "CT"
        End Get
    End Property

    ' Pendiente de la documentación de CTT: URL pública de seguimiento por albarán. Hasta entonces
    ' no se ofrece enlace (el DTO del servidor lo traerá cuando exista RegistroSeguimientoAgencias.CTT).
    Protected Overrides Function EnlaceSeguimientoDe(albaran As String) As String
        Return String.Empty
    End Function
End Class
