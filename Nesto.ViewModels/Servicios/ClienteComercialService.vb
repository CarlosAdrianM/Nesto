Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class ClienteComercialService
    Implements IClienteComercialService
    Public ReadOnly Property Configuracion As IConfiguracion
    Public Sub New(configuracion As IConfiguracion)
        Me.Configuracion = configuracion
    End Sub

    Public Async Function ModificarExtractoCliente(extracto As ExtractoClienteDTO) As Task Implements IClienteComercialService.ModificarExtractoCliente

        If IsNothing(extracto) Then
            Throw New Exception("No se puede actualizar un extracto nulo")
        End If
        Using client As New HttpClient
            client.BaseAddress = New Uri(Configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Dim urlConsulta As String = "ExtractosCliente"
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(extracto), Encoding.UTF8, "application/json")

            response = client.PutAsync(urlConsulta, content).Result


            If response.IsSuccessStatusCode Then
                respuesta = response.Content.ReadAsStringAsync().Result
            Else
                Dim respuestaError = response.Content.ReadAsStringAsync().Result
                Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                Dim contenido As String = detallesError("ExceptionMessage")
                While Not IsNothing(detallesError("InnerException"))
                    detallesError = detallesError("InnerException")
                    Dim contenido2 As String = detallesError("ExceptionMessage")
                    If Not contenido2.Contains("See the inner exception") Then
                        contenido = contenido + vbCr + contenido2
                    End If
                End While
                Throw New Exception(contenido)
            End If
        End Using

    End Function
End Class
