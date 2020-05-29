Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.Windows
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports System.Transactions
Imports Nesto.Contratos
Imports Nesto.Models.Nesto.Models
Imports System.Threading.Tasks

Public Class AgenciaGlovo
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
                New tipoIdDescripcion(0, "NO")
            }
            ListaServicios = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(0, "Business")
            }
            ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(0, "Urgente")
            }
            ListaPaises = rellenarPaises()
        End If

    End Sub

    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Hidden
        End Get
    End Property

    Public ReadOnly Property retornoSoloCobros As Byte Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' No
        End Get
    End Property

    Public ReadOnly Property servicioSoloCobros As Byte Implements IAgencia.servicioSoloCobros
        Get
            Return 0 ' business
        End Get
    End Property

    Public ReadOnly Property horarioSoloCobros As Byte Implements IAgencia.horarioSoloCobros
        Get
            Return 0 'urgente
        End Get
    End Property

    Public ReadOnly Property retornoSinRetorno As Byte Implements IAgencia.retornoSinRetorno
        Get
            Return 0 'no
        End Get
    End Property

    Public ReadOnly Property retornoObligatorio As Byte Implements IAgencia.retornoObligatorio
        Get
            Return 0 ' no
        End Get
    End Property

    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 34 ' España
        End Get
    End Property

    Public Sub calcularPlaza(codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        Throw New NotImplementedException()
    End Sub

    Public Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of String) Implements IAgencia.LlamadaWebService
        Return Task.FromResult(Of String)("OK")
    End Function

    Public Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        Throw New NotImplementedException()
    End Sub

    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        Throw New NotImplementedException()
    End Function

    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Throw New NotImplementedException()
    End Function

    Public Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String Implements IAgencia.calcularCodigoBarras
        Return agenciaVM.envioActual.Numero.ToString("D7")
    End Function

    Public Function EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        Return ""
    End Function
    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA")
        }
    End Function
    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios
    Public ReadOnly Property ServicioDefecto As Byte Implements IAgencia.ServicioDefecto
        Get
            Return 0 ' Business
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Byte Implements IAgencia.HorarioDefecto
        Get
            Return 0 'Urgente
        End Get
    End Property

    Public ReadOnly Property ServicioAuxiliar As Byte Implements IAgencia.ServicioAuxiliar
        Get
            Return Byte.MaxValue ' no existe servicio auxiliar
        End Get
    End Property

    Public ReadOnly Property ServicioCreaEtiquetaRetorno As Byte Implements IAgencia.ServicioCreaEtiquetaRetorno
        Get
            Return Byte.MaxValue ' ningún servicio imprime etiqueta de retorno
        End Get
    End Property
End Class