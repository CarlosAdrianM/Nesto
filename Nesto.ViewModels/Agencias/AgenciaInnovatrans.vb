Imports System.Collections.ObjectModel
Imports System.Text
Imports System.Windows
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

' Innovatrans (DataTrans DTX): agencia "registrar al imprimir". A diferencia de las clásicas, NO
' montamos la etiqueta ni numeramos en local: NestoAPI inserta el envío en la plataforma, devuelve
' el albarán y la etiqueta ZPL, y nosotros solo la mandamos a la Zebra. Toda la integración SOAP
' vive en el servidor (server-side); aquí solo llamamos al endpoint Tramitar e imprimimos.
' Las reglas de consistencia (idempotencia, etc.) las garantiza el servidor.
Public Class AgenciaInnovatrans
    Implements IAgenciaConGestionRemota

    Private ReadOnly agenciaVM As AgenciasViewModel

    Public Sub New(agencia As AgenciasViewModel)
        agenciaVM = agencia
        ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "NO")
        }
        ' Las tarifas de Innovatrans están en NestoAPI (el comparador es server-side). Aquí solo
        ' una tarifa placeholder con ServicioId = ServicioDefecto para que AgenciasViewModel no
        ' falle al hacer ListaServicios.Single(ServicioId = ServicioDefecto) al seleccionar la agencia.
        ListaServicios = New ObservableCollection(Of ITarifaAgencia) From {
            New TarifaInnovatransPlaceholder()
        }
        ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "")
        }
        ListaPaises = New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA", "ES"),
            New Pais(351, "PORTUGAL", "PT")
        }
    End Sub

    ' ---- IAgenciaConGestionRemota (flujo registrar al imprimir) ----

    Public Async Function InsertarYEtiquetar(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgenciaConGestionRemota.InsertarYEtiquetar
        Try
            Dim resultado As TramitarEnvioResultadoDto = Await servicio.TramitarEnvioRemoto(envio.Numero).ConfigureAwait(False)

            ' La plataforma asigna el albarán (va a CodigoBarras) y los bultos.
            envio.CodigoBarras = resultado.Albaran
            If resultado.Bultos > 0 Then
                envio.Bultos = CShort(resultado.Bultos)
            End If

            Await ImprimirZpl(envio, resultado).ConfigureAwait(False)

            Return RespuestaCorrecta(envio, $"Albarán {resultado.Albaran}, {resultado.Bultos} bulto(s)")
        Catch ex As Exception
            Return RespuestaError(ex.Message)
        End Try
    End Function

    Public Async Function Reimprimir(envio As EnviosAgencia, servicio As IAgenciaService, bultoDesde As Integer, bultoHasta As Integer) As Task(Of RespuestaAgencia) Implements IAgenciaConGestionRemota.Reimprimir
        ' El endpoint Tramitar es idempotente: si el envío ya tiene albarán, reimprime sin reinsertar.
        ' (bultoDesde/bultoHasta de momento no se usan; el servidor reimprime la etiqueta completa.)
        Try
            Dim resultado As TramitarEnvioResultadoDto = Await servicio.TramitarEnvioRemoto(envio.Numero).ConfigureAwait(False)
            Await ImprimirZpl(envio, resultado).ConfigureAwait(False)
            Return RespuestaCorrecta(envio, $"Reimpresión albarán {resultado.Albaran}")
        Catch ex As Exception
            Return RespuestaError(ex.Message)
        End Try
    End Function

    Public Function ModificarEnAgencia(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgenciaConGestionRemota.ModificarEnAgencia
        ' Pendiente: NestoAPI aún no expone ModificarEnvios. Hasta entonces, anular y volver a tramitar.
        Return Task.FromResult(RespuestaError("Modificar un envío de Innovatrans ya tramitado todavía no está disponible. Anula y vuelve a tramitar."))
    End Function

    Public Function AnularEnAgencia(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgenciaConGestionRemota.AnularEnAgencia
        ' Pendiente: NestoAPI aún no expone BorrarEnvios.
        Return Task.FromResult(RespuestaError("Anular un envío de Innovatrans ya tramitado todavía no está disponible."))
    End Function

    Private Async Function ImprimirZpl(envio As EnviosAgencia, resultado As TramitarEnvioResultadoDto) As Task
        If String.IsNullOrEmpty(resultado.EtiquetaContenido) Then
            Throw New Exception("Innovatrans no devolvió la etiqueta ZPL del envío.")
        End If

        ' El ZPL viene en base64. Lo decodificamos con la codificación ANSI del sistema (la misma
        ' que usa RawPrinterHelper.SendStringToPrinter al reconvertir), para un round-trip fiel.
        Dim zpl As String
        If String.Equals(resultado.EtiquetaCodificacion, "base64", StringComparison.OrdinalIgnoreCase) Then
            zpl = Encoding.[Default].GetString(Convert.FromBase64String(resultado.EtiquetaContenido))
        Else
            zpl = resultado.EtiquetaContenido
        End If

        ' Misma impresora Zebra que Correos Express (parámetro ImpresoraBolsas).
        Dim mainViewModel As New MainViewModel
        Dim puerto As String = Await mainViewModel.leerParametro(envio.Empresa, Parametros.Claves.ImpresoraBolsas).ConfigureAwait(False)
        Dim unused = RawPrinterHelper.SendStringToPrinter(puerto, zpl)
    End Function

    Private Shared Function RespuestaCorrecta(envio As EnviosAgencia, detalle As String) As RespuestaAgencia
        Return New RespuestaAgencia With {
            .Agencia = "Innovatrans",
            .Fecha = Date.Now,
            .UrlLlamada = String.Empty,
            .Exito = True,
            .CuerpoLlamada = $"Tramitar envío {envio.Numero}",
            .CuerpoRespuesta = detalle,
            .TextoRespuestaError = String.Empty
        }
    End Function

    Private Shared Function RespuestaError(mensaje As String) As RespuestaAgencia
        Return New RespuestaAgencia With {
            .Agencia = "Innovatrans",
            .Fecha = Date.Now,
            .UrlLlamada = String.Empty,
            .Exito = False,
            .CuerpoLlamada = String.Empty,
            .CuerpoRespuesta = String.Empty,
            .TextoRespuestaError = mensaje
        }
    End Function

    ' ---- IAgencia ----

    Public ReadOnly Property NumeroCliente As String Implements IAgencia.NumeroCliente
        Get
            Return String.Empty
        End Get
    End Property

    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        ' El seguimiento de estados de Innovatrans (ConsultarEstados) será un proceso aparte.
        Throw New Exception("El seguimiento de estados de Innovatrans todavía no está disponible.")
    End Function

    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Return If(IsNothing(envio), Nothing, New estadoEnvio)
    End Function

    Public Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String Implements IAgencia.calcularCodigoBarras
        ' El albarán lo asigna la plataforma al tramitar (InsertarYEtiquetar); no se calcula en local.
        Return String.Empty
    End Function

    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        nemonico = "IN"
        nombrePlaza = "Innovatrans"
        telefonoPlaza = String.Empty
        emailPlaza = String.Empty
    End Sub

    Public Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgencia.LlamadaWebService
        ' "Tramitar todos" en una agencia de plataforma = cerrar el día / pedir recogida; ya no envía
        ' datos (el envío se insertó al imprimir). No-op correcto: no hay nada que tramitar aquí.
        Return Task.FromResult(RespuestaCorrecta(envio, "El envío ya se registró al imprimir."))
    End Function

    Public Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        ' En Innovatrans la impresión va acoplada al registro (InsertarYEtiquetar/Reimprimir), que
        ' son async y necesitan el servicio. Este Sub síncrono del flujo clásico no aplica.
    End Sub

    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Collapsed
        End Get
    End Property

    Public ReadOnly Property retornoSoloCobros As Byte Implements IAgencia.retornoSoloCobros
        Get
            Return 0
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
            Return 0
        End Get
    End Property

    Public ReadOnly Property retornoObligatorio As Byte Implements IAgencia.retornoObligatorio
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 34 ' España
        End Get
    End Property

    Private Function IAgencia_EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        ' Pendiente: URL de tracking del Cliente Web de DataTrans (si la habilitan).
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
            ' El albarán lo pone la plataforma; no se debe editar a mano.
            Return False
        End Get
    End Property

    Public ReadOnly Property DimensionesBultosObligatorias As Boolean Implements IAgencia.DimensionesBultosObligatorias
        Get
            ' Innovatrans no pide medidas: el servidor manda una caja estándar por defecto.
            Return False
        End Get
    End Property

    Public ReadOnly Property FlujoTramitacion As TipoFlujoTramitacion Implements IAgencia.FlujoTramitacion
        Get
            Return TipoFlujoTramitacion.RegistrarAlImprimir
        End Get
    End Property

    ' Tarifa placeholder: las tarifas reales de Innovatrans están en NestoAPI (comparador server-side).
    Private Class TarifaInnovatransPlaceholder
        Implements ITarifaAgencia

        Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
            Get
                Return 12
            End Get
        End Property

        Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
            Get
                Return 0
            End Get
        End Property

        Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
            Get
                Return "Innovatrans"
            End Get
        End Property

        Public ReadOnly Property HorarioDefectoId As Byte Implements ITarifaAgencia.HorarioDefectoId
            Get
                Return 0
            End Get
        End Property

        Public ReadOnly Property CosteEnvio As List(Of (Decimal, ZonasEnvioAgencia, Decimal)) Implements ITarifaAgencia.CosteEnvio
            Get
                Return New List(Of (Decimal, ZonasEnvioAgencia, Decimal))
            End Get
        End Property

        Public Function CosteKiloAdicional(zona As ZonasEnvioAgencia) As Decimal Implements ITarifaAgencia.CosteKiloAdicional
            Return 0D
        End Function

        Public Function CosteReembolso(reembolso As Decimal) As Decimal Implements ITarifaAgencia.CosteReembolso
            Return 0D
        End Function
    End Class

End Class
