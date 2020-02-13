Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports System.Collections.ObjectModel
Imports System.Windows

Public Interface IAgencia
    Function cargarEstado(envio As EnviosAgencia) As XDocument
    Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio
    'Function calcularMensajeError(numeroError As Integer) As String
    Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String
    Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String)
    'Function construirXMLdeSalida() As XDocument
    Sub llamadaWebService(servicio As IAgenciaService)
    Sub imprimirEtiqueta()
    ReadOnly Property visibilidadSoloImprimir As Visibility
    ReadOnly Property retornoSoloCobros As Integer
    ReadOnly Property servicioSoloCobros As Integer
    ReadOnly Property horarioSoloCobros As Integer
    ReadOnly Property retornoSinRetorno As Integer ' Especifica la forma de retorno = NO, es decir, cuando no debe mostrarse en la lista de retornos pendientes
    ReadOnly Property retornoObligatorio As Integer ' Forma de retorno = SI (obligatorio)
    ReadOnly Property paisDefecto As Integer
    Function EnlaceSeguimiento(envio As EnviosAgencia) As String
    ReadOnly Property ListaPaises As ObservableCollection(Of Pais)
    ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion)
    ReadOnly Property ListaServicios As ObservableCollection(Of tipoIdDescripcion)
    ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion)
    ReadOnly Property ServicioDefecto As Integer
    ReadOnly Property HorarioDefecto As Integer
End Interface
