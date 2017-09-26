Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports System.Text
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Nesto.Contratos
Imports Nesto.Modulos.Rapports
Imports Nesto.Modulos.Rapports.RapportsModel
Imports Newtonsoft.Json

Public Class RapportService
    Inherits ViewModelBase
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

    Public Async Function crearRapport(rapport As SeguimientoClienteDTO) As Task(Of String) Implements IRapportService.crearRapport
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(rapport), Encoding.UTF8, "application/json")

            Try
                If rapport.Id = 0 Then
                    response = Await client.PostAsync("SeguimientosClientes", content)
                Else
                    response = Await client.PutAsync("SeguimientosClientes", content)
                End If


                If response.IsSuccessStatusCode Then
                    If rapport.Id = 0 Then
                        Dim pathNumeroOrden = response.Headers.Location.LocalPath
                        Dim numOrdenRapport As String = pathNumeroOrden.Substring(pathNumeroOrden.LastIndexOf("/") + 1)
                        rapport.Id = Convert.ToInt32(numOrdenRapport)
                        Return "Rapport creado correctamente"
                    Else
                        Return "Rapport modificado correctamente"
                    End If
                Else
                    Dim respuestaError As String = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As String = JsonConvert.DeserializeObject(Of String)(respuestaError)
                    Return "Se ha producido un error al guardar el rapport"
                End If
            Catch ex As Exception
                Throw ex
            Finally

            End Try

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
