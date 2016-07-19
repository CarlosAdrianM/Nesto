Imports System.Net.Http
Imports Nesto.Contratos
Imports Nesto.Modulos.CarteraPagos

Public Class CarteraPagosService
    Implements ICarteraPagosService

    Private ReadOnly configuracion As IConfiguracion

    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub

    Public Async Function crearFichero(empresa As String, numeroRemesa As Integer) As Task(Of String) Implements ICarteraPagosService.crearFichero

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "CabRemesasPago?empresa=" + empresa
                urlConsulta += "&id=" + numeroRemesa.ToString
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido crear el fichero de la remesa")
            Finally

            End Try

            Return respuesta

        End Using
    End Function

End Class
