''' <summary>
''' Nesto#571: respuesta de GET api/RepartoPropio/Envios/{envioId}/PruebaEntrega (NestoAPI#739; 204 si no hay). Mismos
''' nombres que el servidor (PascalCase): si uno deja de casar, Newtonsoft lo deja por defecto sin avisar. La firma va
''' aparte (…/PruebaEntrega/Firma, image/png) y solo si <see cref="TieneFirma"/>.
''' </summary>
Public Class PruebaEntregaRepartoDto
    Public Property EnvioId As Integer
    Public Property FechaHora As Date
    Public Property Receptor As String
    Public Property DniReceptor As String
    ''' <summary>"Efectivo", "Tarjeta" o "NoCobrado"; vacío si el envío no llevaba reembolso.</summary>
    Public Property CobroReembolso As String
    Public Property ImporteCobrado As Decimal?
    Public Property Usuario As String
    Public Property TieneFirma As Boolean
    Public Property Latitud As Double?
    Public Property Longitud As Double?
End Class
