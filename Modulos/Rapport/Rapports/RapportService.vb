Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports System.Text
Imports Microsoft.Graph
Imports Microsoft.Graph.Auth
Imports Microsoft.Identity.Client
Imports Microsoft.Office.Interop
Imports Nesto.Contratos
Imports Nesto.Modulos.Rapports.RapportsModel
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class RapportService
    Inherits ViewModelBase
    Implements IRapportService


    Private ReadOnly configuracion As IConfiguracion
    Private Property app As IPublicClientApplication


    Public Sub New(configuracion As IConfiguracion, app As IPublicClientApplication)
        Me.configuracion = configuracion
        Me.app = app
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
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
                        contenido = contenido + vbCr + contenido2
                    End While
                    Throw New Exception("Se ha producido un error al crear el rapport" + vbCr + contenido)
                End If
            Catch ex As Exception
                Throw ex
            Finally

            End Try

        End Using
    End Function

    Public Async Function CrearCita(rapport As SeguimientoClienteDTO, fechaAviso As Date) As Task(Of String) Implements IRapportService.CrearCita
        Dim scopes = {"Calendars.ReadWrite"}
        Dim authProvider As InteractiveAuthenticationProvider = New InteractiveAuthenticationProvider(app, scopes)
        Dim graphClient As GraphServiceClient = New GraphServiceClient(authProvider)

        If IsNothing(rapport.Cliente) OrElse IsNothing(rapport.Contacto) Then
            Return "No se puede crear el aviso si no se especifica un cliente y un contacto"
        End If

        Dim nuevaCita As New [Event]
        nuevaCita.Subject = "Aviso del cliente " + rapport.Cliente.Trim + "/" + rapport.Contacto.Trim
        nuevaCita.Body = New ItemBody With {
            .Content = rapport.Comentarios
        }

        Dim dateTimeFormat As String = "yyyy-MM-ddTHH:mm:ss"

        nuevaCita.Start = New DateTimeTimeZone With {
            .DateTime = fechaAviso.ToString(dateTimeFormat),
            .TimeZone = TimeZoneInfo.Local.Id
        }

        nuevaCita.End = New DateTimeTimeZone With {
            .DateTime = fechaAviso.AddMinutes(15).ToString(dateTimeFormat),
            .TimeZone = TimeZoneInfo.Local.Id
        }

        nuevaCita.IsReminderOn = True
        nuevaCita.ReminderMinutesBeforeStart = 0
        Await graphClient.Me.Calendar.Events.Request().AddAsync(nuevaCita)
        Return "Cita creada correctamente"
    End Function

    Public Async Function cargarListaRapportsFiltrada(vendedor As String, filtro As String) As Task(Of ObservableCollection(Of SeguimientoClienteDTO)) Implements IRapportService.cargarListaRapportsFiltrada
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""


            Try
                Dim urlConsulta As String = "SeguimientosClientes"
                urlConsulta += "?vendedor=" + vendedor
                urlConsulta += "&filtro=" + filtro

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido recuperar la lista de rapports filtrados por " + filtro)
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

    Public Function CargarListaEstados() As List(Of RapportsModel.SeguimientoClienteDTO.idShortDescripcion) Implements IRapportService.CargarListaEstados
        ' Hacer que lo lea de la BD
        Dim listaEstadosRapport = New List(Of idShortDescripcion)
        listaEstadosRapport.Add(New idShortDescripcion With {
                                .id = 0,
                                .descripcion = "Vigente"})
        listaEstadosRapport.Add(New idShortDescripcion With {
                                .id = 1,
                                .descripcion = "No Contactado"})
        listaEstadosRapport.Add(New idShortDescripcion With {
                                .id = 2,
                                .descripcion = "Gestión Administrativa"})
        listaEstadosRapport.Add(New idShortDescripcion With {
                                .id = -1,
                                .descripcion = "Nulo"})
        Return listaEstadosRapport
    End Function

    Public Function CargarListaTipos() As List(Of idDescripcion) Implements IRapportService.CargarListaTipos
        Dim listaTiposRapports = New List(Of idDescripcion)
        listaTiposRapports.Add(New idDescripcion With {
            .id = "V",
            .descripcion = "Visita"
        })
        listaTiposRapports.Add(New idDescripcion With {
            .id = "T",
            .descripcion = "Teléfono"
        })
        Return listaTiposRapports
    End Function
End Class
