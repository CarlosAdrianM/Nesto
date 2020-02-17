Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.Windows
Imports System.Net
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports System.Transactions
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Threading.Tasks
Imports Nesto.Contratos
Imports Nesto.Models.Nesto.Models

Public Class AgenciaOnTime
    Implements IAgencia


    ' Propiedades de Prism
    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    Public Sub New(agencia As AgenciasViewModel)
        If Not IsNothing(agencia) Then

            NotificationRequest = New InteractionRequest(Of INotification)
            'ConfirmationRequest = New InteractionRequest(Of IConfirmation)

            ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(0, "NO"),
                New tipoIdDescripcion(1, "SI")
            }
            ListaServicios = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(1, "Normal")
            }
            ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(0, ""),
                New tipoIdDescripcion(1, "Doble ciclo"),
                New tipoIdDescripcion(2, "14 Horas")
            }

            ListaPaises = rellenarPaises()
        End If



    End Sub

    ' Funciones
    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado

        NotificationRequest.Raise(New Notification() With {
             .Title = "Error",
            .Content = "OnTime no permite integración. Consulte el estado en la página web de OnTime."
        })
        Return Nothing

    End Function
    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Dim estado As New estadoEnvio
        Dim expedicion As New expedicion
        'Dim trackinglistxml As XElement
        'Dim tracking As tracking
        'Dim digitalizacionesxml As XElement
        'Dim digitalizacion As digitalizacion

        If IsNothing(envio) Then
            Return Nothing
        End If

        Return estado
    End Function
    Public Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String Implements IAgencia.calcularCodigoBarras
        Return agenciaVM.envioActual.Numero.ToString("D7")
    End Function
    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        nemonico = "OT"
        nombrePlaza = "OnTime"
        telefonoPlaza = "902112820"
        emailPlaza = "traficodistribucion@ontimelogistica.com"
    End Sub
    Public Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of String) Implements IAgencia.LlamadaWebService
        Return Task.FromResult(Of String)("OK")
    End Function
    Public Async Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        Dim mainViewModel As New MainViewModel
        Dim puerto As String = Await mainViewModel.leerParametro(envio.Empresa, "ImpresoraBolsas")

        Dim objFSO
        Dim objStream
        objFSO = CreateObject("Scripting.FileSystemObject")
        objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  
        Dim i As Integer



        Try
            For i = 1 To envio.Bultos
                objStream.Writeline("I8,A,034")
                objStream.Writeline("N")
                objStream.Writeline("A40,10,0,4,1,1,N,""" + envio.Nombre + """")
                objStream.Writeline("A40,50,0,4,1,1,N,""" + envio.Direccion + """")
                objStream.Writeline("A40,90,0,4,1,1,N,""" + envio.CodPostal + " " + envio.Poblacion + """")
                objStream.Writeline("A40,130,0,4,1,1,N,""" + envio.Provincia + """")
                objStream.Writeline("A40,170,0,4,1,1,N,""Bulto: " + i.ToString + "/" + envio.Bultos.ToString _
                                    + ". Cliente: " + envio.Cliente.Trim + ". Fecha: " + envio.Fecha + """")
                objStream.Writeline("A40,450,0,4,1,2,N,""" + envio.Nemonico + " " + envio.NombrePlaza + """")
                objStream.Writeline("A40,510,0,4,1,2,N,""" + ListaHorarios.Where(Function(x) x.id = envio.Horario).FirstOrDefault.descripcion + """")
                objStream.Writeline("A590,265,0,5,2,2,N,""" + envio.Nemonico + """")
                objStream.Writeline("P1")
                objStream.Writeline("")
            Next
        Catch ex As Exception
            NotificationRequest.Raise(New Notification() With {
                    .Title = "¡Error! Se ha producido un error y no se han grabado los datos",
                .Content = ex.InnerException.Message
            })
        Finally
            objStream.Close()
            objFSO = Nothing
            objStream = Nothing
        End Try
    End Sub
    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Visible
        End Get
    End Property
    Public ReadOnly Property retornoSoloCobros As Integer Implements IAgencia.retornoSoloCobros
        Get
            Return 0 'NO
        End Get
    End Property
    Public ReadOnly Property servicioSoloCobros As Integer Implements IAgencia.servicioSoloCobros
        Get
            Return 1 ' Normal
        End Get
    End Property
    Public ReadOnly Property horarioSoloCobros As Integer Implements IAgencia.horarioSoloCobros
        Get
            Return 2 ' 14 horas
        End Get
    End Property
    Public ReadOnly Property retornoSinRetorno As Integer Implements IAgencia.retornoSinRetorno
        Get
            Return 0 ' NO
        End Get
    End Property
    Public ReadOnly Property retornoObligatorio As Integer Implements IAgencia.retornoObligatorio
        Get
            Return 1 ' Retorno obligatorio
        End Get
    End Property
    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 34 'España
        End Get
    End Property
    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA")
        }
    End Function
    Private Async Function cambiarEstadoAsync(enviosAgencia As EnviosAgencia) As Task(Of HttpResponseMessage)
        Dim response As HttpResponseMessage
        Dim urlLlamada As String = "http://localhost:53364/api/EnviosAgencias/" + enviosAgencia.Numero.ToString

        Using cliente As New HttpClient
            cliente.BaseAddress = New Uri("http://localhost:53364/")
            cliente.DefaultRequestHeaders.Accept.Clear()
            cliente.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
            'enviosAgencia.Estado = AgenciasViewModel.ESTADO_TRAMITADO_ENVIO 'Enviado
            response = Await cliente.PutAsJsonAsync(urlLlamada, enviosAgencia)
        End Using

        Return response.EnsureSuccessStatusCode
    End Function

    Private Function IAgencia_EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        Dim referencia As String = WebUtility.UrlEncode(envio.Cliente.Trim() + "-" + envio.Pedido.ToString())
        Return "https://ontimegts.alertran.net/gts/pub/clielocserv.seam?cliente=02890107&referencia=" + referencia
    End Function

    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios

    Public ReadOnly Property ServicioDefecto As Integer Implements IAgencia.ServicioDefecto
        Get
            Return 1 'Normal
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Integer Implements IAgencia.HorarioDefecto
        Get
            Return 0 ' en blanco
        End Get
    End Property
End Class
