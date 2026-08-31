''' <summary>
''' Nesto#340 (slice A3): una fila de GET api/EnviosAgencias/{id}/Historia.
'''
''' Es el POCO que llega por la red; el servicio lo convierte a la entidad EnviosHistoria porque
''' es lo que la rejilla de Agencias tiene enlazado. Cuando esa pantalla deje de depender de las
''' entidades de Nesto.Models, esta conversión sobra y el modelo se usa directamente.
''' </summary>
Public Class EnvioHistoriaModel

    Public Property Numero As Integer
    Public Property NumeroEnvio As Integer

    ''' <summary>Qué campo del envío se cambió: "Reembolso", "Estado", "Retorno"...</summary>
    Public Property Campo As String

    Public Property ValorAnterior As String
    Public Property Observaciones As String
    Public Property Usuario As String
    Public Property FechaModificacion As Date

End Class
