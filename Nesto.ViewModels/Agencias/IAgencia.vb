Imports Microsoft.Office.Core
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports System.Windows

Public Interface IAgencia
    Function cargarEstado(envio As EnviosAgencia) As XDocument
    Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio
    'Function calcularMensajeError(numeroError As Integer) As String
    Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String
    Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String)
    ' Carlos 14/02/2020: el parámetro servicio de LlamadaWebService hay que quitarlo cuando EF solucione el error de Numero y Número (solo se usa para coger la empresa)
    Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of String) ' Devuelve "OK" en caso de que no haya error o el texto del error
    Sub imprimirEtiqueta(envio As EnviosAgencia)
    ReadOnly Property visibilidadSoloImprimir As Visibility
    ReadOnly Property retornoSoloCobros As Byte
    ReadOnly Property servicioSoloCobros As Byte
    ReadOnly Property horarioSoloCobros As Byte
    ReadOnly Property retornoSinRetorno As Byte ' Especifica la forma de retorno = NO, es decir, cuando no debe mostrarse en la lista de retornos pendientes
    ReadOnly Property retornoObligatorio As Byte ' Forma de retorno = SI (obligatorio)
    ReadOnly Property paisDefecto As Integer
    Function EnlaceSeguimiento(envio As EnviosAgencia) As String
    ReadOnly Property ListaPaises As ObservableCollection(Of Pais)
    ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion)
    ReadOnly Property ListaServicios As ObservableCollection(Of tipoIdDescripcion)
    ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion)
    ReadOnly Property ServicioDefecto As Byte
    ReadOnly Property HorarioDefecto As Byte
    ReadOnly Property ServicioAuxiliar As Byte ' este servicio se ignorará y no se mostrará en los tramitados
    ReadOnly Property ServicioCreaEtiquetaRetorno As Byte ' el servicio que imprimie etiqueta de retorno
    Function RespuestaYaTramitada(respuesta As String) As Boolean
End Interface
