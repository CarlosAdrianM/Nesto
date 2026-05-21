Imports System.Collections.ObjectModel
Imports System.Windows
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

' Nesto#359: Canteras es la agencia que hace los envíos a Canarias. No tiene integración
' (ni web service, ni impresión de etiquetas): el flujo es 100% manual — se les manda un
' correo con los datos del envío y nos contestan con el nº de envío, que el usuario tiene
' que pegar en el campo CodigoBarras desde la pestaña de envíos. La validación de mínimos
' (400€ pedido o 100€ portes) y la prohibición de contra reembolso se hacen en NestoAPI
' al crear la etiqueta (NestoAPI#204).
Public Class AgenciaCanteras
    Implements IAgencia

    Public Sub New()
        ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "NO")
        }
        ListaServicios = New ObservableCollection(Of ITarifaAgencia)
        ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "")
        }
        ListaPaises = rellenarPaises()
    End Sub

    Public ReadOnly Property NumeroCliente As String Implements IAgencia.NumeroCliente
        Get
            Return String.Empty
        End Get
    End Property

    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        Throw New Exception("Canteras no permite integración. Consulta el estado por correo con Canteras o usa el nº de envío que indicaron.")
    End Function

    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Return If(IsNothing(envio), Nothing, New estadoEnvio)
    End Function

    Public Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String Implements IAgencia.calcularCodigoBarras
        ' Canteras devuelve el nº de envío por correo. El usuario lo pega manualmente; el
        ' cálculo automático no aplica.
        Return String.Empty
    End Function

    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        nemonico = "CN"
        nombrePlaza = "Canteras"
        telefonoPlaza = String.Empty
        emailPlaza = String.Empty
    End Sub

    Public Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgencia.LlamadaWebService
        ' Sin integración. Mismo patrón que OnTime: devolvemos "OK" para que la máquina de
        ' estados de Agencias avance, pero el tramitado real lo hace el usuario por correo.
        Dim respuesta As New RespuestaAgencia With {
            .Agencia = "Canteras",
            .Fecha = Date.Now,
            .UrlLlamada = String.Empty,
            .Exito = True,
            .CuerpoLlamada = String.Empty,
            .CuerpoRespuesta = String.Empty,
            .TextoRespuestaError = "OK"
        }
        Return Task.FromResult(Of RespuestaAgencia)(respuesta)
    End Function

    Public Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        ' Canteras no imprime etiquetas en local — la etiqueta viaja con el correo a Canteras.
        Throw New Exception("Canteras no genera etiqueta desde Nesto. El envío se notifica por correo.")
    End Sub

    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Collapsed
        End Get
    End Property

    Public ReadOnly Property retornoSoloCobros As Byte Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' NO (Canteras no admite contra reembolso)
        End Get
    End Property

    Public ReadOnly Property servicioSoloCobros As Byte Implements IAgencia.servicioSoloCobros
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property horarioSoloCobros As Byte Implements IAgencia.horarioSoloCobros
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property retornoSinRetorno As Byte Implements IAgencia.retornoSinRetorno
        Get
            Return 0 ' NO
        End Get
    End Property

    Public ReadOnly Property retornoObligatorio As Byte Implements IAgencia.retornoObligatorio
        Get
            Return 0 ' NO
        End Get
    End Property

    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 34 ' España (Canarias)
        End Get
    End Property

    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA")
        }
    End Function

    Private Function IAgencia_EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        ' Canteras no tiene web de tracking; el usuario consulta el estado por correo.
        Return String.Empty
    End Function

    Public Function RespuestaYaTramitada(respuesta As String) As Boolean Implements IAgencia.RespuestaYaTramitada
        Return False
    End Function

    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of ITarifaAgencia) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios

    Public ReadOnly Property ServicioDefecto As Byte Implements IAgencia.ServicioDefecto
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Byte Implements IAgencia.HorarioDefecto
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property ServicioAuxiliar As Byte Implements IAgencia.ServicioAuxiliar
        Get
            Return Byte.MaxValue
        End Get
    End Property

    Public ReadOnly Property ServicioCreaEtiquetaRetorno As Byte Implements IAgencia.ServicioCreaEtiquetaRetorno
        Get
            Return Byte.MaxValue
        End Get
    End Property

    Public ReadOnly Property PermiteEditarCodigoBarras As Boolean Implements IAgencia.PermiteEditarCodigoBarras
        Get
            ' Canteras devuelve el nº de envío por correo; el usuario lo pega a mano.
            Return True
        End Get
    End Property

End Class
