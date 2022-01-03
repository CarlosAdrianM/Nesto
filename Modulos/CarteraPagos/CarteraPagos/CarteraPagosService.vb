Imports System.Net.Http
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.CarteraPagos
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class CarteraPagosService
    Implements ICarteraPagosService

    Private ReadOnly configuracion As IConfiguracion

    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub

    Public Async Function CrearFichero(numeroRemesa As Integer) As Task(Of String) Implements ICarteraPagosService.CrearFichero

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "CabRemesasPago?remesaId=" + numeroRemesa.ToString
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
                        contenido = contenido + vbCr + contenido2
                    End While
                    Throw New Exception(contenido)
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido crear el fichero de la remesa." + vbCrLf + ex.Message)
            Finally

            End Try

            Return respuesta

        End Using
    End Function

    Public Async Function CrearFichero(extractoId As Integer, numeroBanco As String) As Task(Of String) Implements ICarteraPagosService.CrearFichero
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "CabRemesasPago?extractoId=" + extractoId.ToString
                urlConsulta += "&numeroBanco=" + numeroBanco
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
                        contenido = contenido + vbCr + contenido2
                    End While
                    Throw New Exception(contenido)
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido crear el fichero de la remesa." + vbCrLf + ex.Message)
            Finally

            End Try

            Return respuesta
        End Using
    End Function
End Class
