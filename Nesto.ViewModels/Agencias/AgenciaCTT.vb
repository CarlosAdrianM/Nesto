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

    ' NestoAPI#505: EnviosAgencia.Servicio de CTT. 48 = el económico, el que propone el comparador y el
    ' que pone el servidor por defecto (PerfilAgenciaCTT.DefaultsEnvio); 24 = urgente, solo si el
    ' usuario lo fuerza (cuesta más). La API manda a CTT el código de la zona (C24/CBA24/CCA24).
    Public Const SERVICIO_48H As Byte = 48
    Public Const SERVICIO_24H As Byte = 24

    ' NestoAPI#494: EnviosAgencia.Retorno de CTT (AgenciaRemotaCTT.RETORNO_* en la API). Mientras el
    ' parámetro CTTRetornosActivos no esté a 1, la API rechaza los retornos con un mensaje claro.
    Public Const RETORNO_CON_RETORNO As Byte = 1
    Public Const RETORNO_RECOGIDA_EN_ORIGEN As Byte = 2

    Public Overrides ReadOnly Property ServicioDefecto As Byte
        Get
            Return SERVICIO_48H
        End Get
    End Property

    Protected Overrides Function ServiciosDisponibles() As IEnumerable(Of tipoIdDescripcion)
        Return {
            New tipoIdDescripcion(SERVICIO_48H, "CTT 48h"),
            New tipoIdDescripcion(SERVICIO_24H, "CTT 24h (urgente)")
        }
    End Function

    Protected Overrides Function TiposRetornoDisponibles() As IEnumerable(Of tipoIdDescripcion)
        Return {
            New tipoIdDescripcion(0, "NO"),
            New tipoIdDescripcion(RETORNO_CON_RETORNO, "Con retorno"),
            New tipoIdDescripcion(RETORNO_RECOGIDA_EN_ORIGEN, "Recogida en origen")
        }
    End Function

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
