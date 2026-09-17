Imports Nesto.Infrastructure.Shared

' CTT Express (NestoAPI#493): agencia "registrar al imprimir", mismo flujo que Innovatrans. La
' integración con la API de CTT vive en NestoAPI; aquí solo lo que la distingue.
'
' Está en el factory de AgenciasViewModel y en el comparador, pero el desplegable oculta las agencias
' sombra (AgenciasTransporte.EsSombra = 1): el día de arranque basta quitar la sombra en la tabla.
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

    ' Tercera Zebra, la que usaba Sending (parámetro ImpresoraAgencia, \\RDS2016\etiquetas1), con rollos
    ' blancos de 100x150: la de bolsas lleva el papel preimpreso de Tipsa y la de GLS es más corta.
    Public Overrides ReadOnly Property ClaveImpresora As String
        Get
            Return Parametros.Claves.ImpresoraAgencia
        End Get
    End Property

    ' Localizador público de CTT Express (sc = shipping_code). DEUDA TEMPORAL como en Innovatrans:
    ' duplica RegistroSeguimientoAgencias.SeguimientoCTT del servidor hasta que la ventana consuma el
    ' enlace del DTO.
    Protected Overrides Function EnlaceSeguimientoDe(albaran As String) As String
        Return "https://www.cttexpress.com/localizador-de-envios?sc=" & albaran
    End Function
End Class
