Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports System.Text
Imports Azure.Identity
Imports Microsoft.Graph
Imports Microsoft.Graph.Auth
Imports Microsoft.Identity.Client
Imports Microsoft.Office.Interop
Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.Cliente
Imports Nesto.Modulos.Rapports.RapportsModel
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class RapportService
    Inherits ViewModelBase
    Implements IRapportService


    Private ReadOnly configuracion As IConfiguracion
    Private Property InteractiveBrowserCredential As InteractiveBrowserCredential

    Public Sub New(configuracion As IConfiguracion, interactiveBrowserCredential As InteractiveBrowserCredential)
        Me.configuracion = configuracion
        Me.InteractiveBrowserCredential = interactiveBrowserCredential
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
        Dim graphClient As New GraphServiceClient(InteractiveBrowserCredential, scopes) 'you can pass the TokenCredential directly To the GraphServiceClient

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

    Public Async Function CrearTareaPlanner(rapport As SeguimientoClienteDTO) As Task(Of String) Implements IRapportService.CrearTareaPlanner
        Dim planId = Constantes.Planner.GestionCobro.PLAN_ID
        Dim bucketId = Constantes.Planner.GestionCobro.BUCKET_PENDIENTES
        Dim scopes = {"User.Read.All", "Group.ReadWrite.All"}
        Dim graphClient As New GraphServiceClient(InteractiveBrowserCredential, scopes) 'you can pass the TokenCredential directly To the GraphServiceClient

        Dim users = Await graphClient.Users.Request().GetAsync()

        Dim tareasBucket = Await graphClient.Planner.Buckets(bucketId).Tasks.Request().GetAsync()
        Dim plannerTask As PlannerTask

        Try
            Dim tituloTarea = String.Format("Impagados cliente {0}", rapport.Cliente.Trim)
            Dim etag As String
            If tareasBucket.Any(Function(t) t.Title = tituloTarea) Then
                plannerTask = tareasBucket.Where(Function(t) t.Title = tituloTarea).OrderByDescending(Function(t) t.CreatedDateTime).First
                etag = plannerTask.GetEtag
                'Else ' Si no existe, creamos la tarea
                '    Dim usuarioTareas As String = Await configuracion.leerParametro(rapport.Empresa, Parametros.Claves.UsuarioAvisoImpagadoDefecto)
                '    Dim usuarios As String() = usuarioTareas.Split(New Char() {";"c})
                '    Dim usuariosAsignar As New List(Of String)
                '    For Each nombreUsuario In usuarios
                '        Dim usuarioAsignar = users.FirstOrDefault(Function(c) c.Mail = nombreUsuario.Trim).Id
                '        If Not String.IsNullOrEmpty(usuarioAsignar) Then
                '            usuariosAsignar.Add(usuarioAsignar)
                '        End If
                '    Next
                '    Dim asignadas = New PlannerAssignments
                '    For Each nombreUsuario In usuariosAsignar
                '        asignadas.AddAssignee(nombreUsuario)
                '    Next
                '    asignadas.ODataType = Nothing
                '    plannerTask = New PlannerTask With
                '    {
                '        .PlanId = planId,
                '        .BucketId = bucketId,
                '        .Title = tituloTarea,
                '        .Assignments = asignadas
                '    }
                '    plannerTask = Await graphClient.Planner.Tasks.Request().AddAsync(plannerTask)
            Else
                Return "No se anota en planner porque no existe tarea de impagados"
            End If

            Dim plan As PlannerPlan = Await graphClient.Planner.Plans(planId).Request.GetAsync
            Dim grupoId = plan.Owner ' en beta es "Container"
            Dim hiloId = plannerTask.ConversationThreadId
            Dim post = New Post With {
                .Body = New ItemBody With {
                    .ContentType = BodyType.Text,
                    .Content = rapport.Comentarios
                }
            }
            If IsNothing(hiloId) Then
                Dim posts = New ConversationThreadPostsCollectionPage From {
                    post
                }
                Dim hilo As New ConversationThread With {
                    .Topic = $"Impagados cliente {rapport.Cliente}",
                    .Posts = posts
                }
                Dim hiloCreado = Await graphClient.Groups(grupoId).Threads().Request().AddAsync(hilo)
                Dim nuevaTask As New PlannerTask With {
                    .ConversationThreadId = hiloCreado.Id
                }
                Await graphClient.Planner.Tasks(plannerTask.Id).Request().Header("Prefer", "return=representation").Header("If-Match", etag).UpdateAsync(nuevaTask)
            Else
                Await graphClient.Groups(grupoId).Threads(hiloId).Reply(post).Request().PostAsync()
            End If

            Return "Comentario añadido correctamente a la tarea de planner"
        Catch ex As Exception
            Throw
        End Try
    End Function

    Public Async Function QuitarDeMiListado(rapport As SeguimientoClienteDTO, vendedorEstetica As String, vendedorPeluqueria As String) As Task(Of Boolean) Implements IRapportService.QuitarDeMiListado
        Dim clienteCrear As ClienteCrear = New ClienteCrear With {
            .Empresa = rapport.Empresa,
            .Cliente = rapport.Cliente,
            .Contacto = rapport.Contacto,
            .Usuario = rapport.Usuario
        }

        Dim vendedorUsuario As String = Await configuracion.leerParametro(rapport.Empresa, Parametros.Claves.Vendedor)

        If vendedorEstetica = vendedorUsuario Then
            clienteCrear.VendedorEstetica = Constantes.Vendedores.VENDEDOR_POR_DEFECTO
        End If
        If vendedorPeluqueria = Await configuracion.leerParametro(rapport.Empresa, Parametros.Claves.Vendedor) Then
            clienteCrear.VendedorPeluqueria = Constantes.Vendedores.VENDEDOR_POR_DEFECTO
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(clienteCrear), Encoding.UTF8, "application/json")

            Try
                response = Await client.PutAsync("Clientes/DejarDeVisitar", content)

                If Not response.IsSuccessStatusCode Then
                    Dim respuestaError As String = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
                        contenido = contenido + vbCr + contenido2
                    End While
                    Throw New Exception("Se ha producido un error al quitar el cliente del listado" + vbCr + contenido)
                End If
            Catch ex As Exception
                Throw New Exception("Se ha producido un error al quitar el cliente del listado", ex)
            End Try

        End Using
    End Function
End Class
