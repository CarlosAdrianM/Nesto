Imports System.Collections.ObjectModel
Imports Nesto.Infrastructure.Shared

' Nesto#571 (NestoAPI#739): «Reparto propio», el reparto con nuestras furgonetas como una agencia más. Mismo flujo
' «registrar al imprimir» que Innovatrans y CTT (AgenciaGestionadaPorApi): NestoAPI da de alta el envío, devuelve la
' etiqueta ZPL (una por bulto, con el logo y el código RP{EnvioId}-{N}) y aquí solo se manda a la Zebra. Se parece más a
' Innovatrans: un solo servicio, sin retornos y por la misma impresora (la de bolsas).
'
' La entrega (firma/DNI y cobro del reembolso) y las incidencias las registra Ariadna en la API; el envío pasa a
' Entregado o Incidentado como con las demás agencias. Facturar rutas (rutas AT y 16) no cambia.
Public Class AgenciaRepartoPropio
    Inherits AgenciaGestionadaPorApi

    Public Overrides ReadOnly Property NombreAgencia As String
        Get
            Return Constantes.Agencias.NOMBRE_AGENCIA_REPARTO_PROPIO
        End Get
    End Property

    Public Overrides ReadOnly Property AgenciaId As Integer
        Get
            Return Constantes.Agencias.AGENCIA_REPARTO_PROPIO
        End Get
    End Property

    Protected Overrides ReadOnly Property NemonicoPlaza As String
        Get
            Return "RP"
        End Get
    End Property

    ' Solo repartimos en España (nuestras rutas).
    Protected Overrides Function PaisesSoportados() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA", "ES")
        }
    End Function

    ' Sin portal de seguimiento externo: lo que pasa con el envío se ve en su historia y en la prueba de entrega.
    Protected Overrides Function EnlaceSeguimientoDe(albaran As String) As String
        Return String.Empty
    End Function
End Class
