Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports Nesto.Contratos
Imports Nesto.Modulos.Rapports
Imports Nesto.Modulos.Rapports.RapportsModel
Imports Newtonsoft.Json

Public Class RapportService
    Implements IRapportService

    Private ReadOnly configuracion As IConfiguracion

    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub

    Public Async Function cargarListaRapports(vendedor As String, fecha As Date) As Task(Of ObservableCollection(Of SeguimientoClienteDTO)) Implements IRapportService.cargarListaRapports
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""


            Try
                Dim urlConsulta As String = "SeguimientosClientes"
                urlConsulta += "?vendedor=" + vendedor
                urlConsulta += "&fecha=" + fecha.ToString("O").Substring(0, 19)

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido recuperar la lista de rapports del día " + fecha.ToShortDateString)
            Finally

            End Try

            Dim listaRapports As ObservableCollection(Of SeguimientoClienteDTO) = JsonConvert.DeserializeObject(Of ObservableCollection(Of SeguimientoClienteDTO))(respuesta)

            Return listaRapports

        End Using
    End Function

    Private Async Function cargarListaRapports(empresa As String, cliente As String, contacto As String) As Task(Of ObservableCollection(Of SeguimientoClienteDTO)) Implements IRapportService.cargarListaRapports

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""


            Try
                Dim urlConsulta As String = "SeguimientosClientes"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&cliente=" + cliente
                urlConsulta += "&contacto=" + contacto

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido recuperar la lista de rapports del cliente " + cliente)
            Finally

            End Try

            Dim listaRapports As ObservableCollection(Of SeguimientoClienteDTO) = JsonConvert.DeserializeObject(Of ObservableCollection(Of SeguimientoClienteDTO))(respuesta)

            Return listaRapports

        End Using
    End Function

End Class
