Imports System.Collections.ObjectModel
Imports System.Text
Imports System.Windows
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

' NestoAPI#493: lo común de las agencias "registrar al imprimir" (Innovatrans, CTT). Toda la
' integración con la plataforma de la agencia vive en NestoAPI: aquí solo llamamos a los endpoints
' Tramitar / Modificar / Anular y mandamos a la Zebra la etiqueta ZPL que devuelve el servidor.
' Las reglas de consistencia (idempotencia, etc.) las garantiza el servidor.
'
' Cada agencia concreta aporta lo poco que la distingue: nombre (clave del factory y de la
' auditoría), id de AgenciasTransporte, nemónico de plaza y enlace público de seguimiento.
' Nada de la operativa se copia: si algo del flujo cambia, cambia aquí para todas.
Public MustInherit Class AgenciaGestionadaPorApi
    Implements IAgenciaConGestionRemota

    ' ---- Lo que aporta cada agencia ----

    ''' <summary>Nombre tal como está en AgenciasTransporte.Nombre (y clave del factory del ViewModel).</summary>
    Public MustOverride ReadOnly Property NombreAgencia As String

    ''' <summary>AgenciasTransporte.Numero (el AgenciaId de las tarifas del servidor).</summary>
    Public MustOverride ReadOnly Property AgenciaId As Integer

    ''' <summary>Nemónico de plaza de dos letras (calcularPlaza).</summary>
    Protected MustOverride ReadOnly Property NemonicoPlaza As String

    ''' <summary>Enlace público de seguimiento para un albarán. String.Empty si la agencia no tiene portal.</summary>
    Protected MustOverride Function EnlaceSeguimientoDe(albaran As String) As String

    ''' <summary>Parámetro de usuario con la impresora Zebra a la que va el ZPL. Por defecto la de bolsas (Innovatrans, CEX).</summary>
    Public Overridable ReadOnly Property ClaveImpresora As String
        Get
            Return Parametros.Claves.ImpresoraBolsas
        End Get
    End Property

    ' Recién integradas: logging detallado ON para vigilarlas (NestoAPI#259). La agencia lo pone a
    ' False cuando esté rodada.
    Public Overridable ReadOnly Property LoggingDetallado As Boolean Implements IAgencia.LoggingDetallado
        Get
            Return True
        End Get
    End Property

    Protected Overridable Function PaisesSoportados() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA", "ES"),
            New Pais(351, "PORTUGAL", "PT")
        }
    End Function

    Protected Sub New()
        ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "NO")
        }
        ' Las tarifas reales están en NestoAPI (el comparador es server-side). Aquí solo una tarifa
        ' placeholder con ServicioId = ServicioDefecto para que AgenciasViewModel no falle al hacer
        ' ListaServicios.Single(ServicioId = ServicioDefecto) al seleccionar la agencia.
        ListaServicios = New ObservableCollection(Of ITarifaAgencia) From {
            New TarifaPlaceholder(AgenciaId, NombreAgencia)
        }
        ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "")
        }
        ListaPaises = PaisesSoportados()
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

    Public Async Function ModificarEnAgencia(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgenciaConGestionRemota.ModificarEnAgencia
        ' Nesto#411 (NestoAPI#317): manda los datos ACTUALES del envío (los que el usuario ha
        ' corregido) y el servidor los aplica en la agencia (misma canalización que el insert) y
        ' persiste. La etiqueta vuelve reimpresa (lleva CP/población impresos) y se manda a la Zebra.
        Try
            Dim datos As New ModificarEnvioAgenciaDto With {
                .Nombre = envio.Nombre,
                .Direccion = envio.Direccion,
                .CodigoPostal = envio.CodPostal,
                .Poblacion = envio.Poblacion,
                .Provincia = envio.Provincia,
                .Telefono = envio.Telefono,
                .Movil = envio.Movil,
                .Observaciones = envio.Observaciones
            }
            Dim resultado As TramitarEnvioResultadoDto = Await servicio.ModificarEnvioRemoto(envio.Numero, datos).ConfigureAwait(False)
            Await ImprimirZpl(envio, resultado).ConfigureAwait(False)
            Return RespuestaCorrecta(envio, $"Envío modificado en la agencia (albarán {resultado.Albaran}); etiqueta reimpresa")
        Catch ex As Exception
            Return RespuestaError(ex.Message)
        End Try
    End Function

    Public Async Function AnularEnAgencia(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgenciaConGestionRemota.AnularEnAgencia
        ' Nesto#411 (NestoAPI#316): anula en la agencia; el servidor devuelve el envío a etiqueta
        ' PENDIENTE (Estado -1, sin albarán). Reflejamos ese estado en el objeto local para que la UI
        ' y el flujo de borrado vean el envío ya anulado. Si la agencia rechaza (p. ej. ventana de
        ' edición del día cerrada), NO se toca nada y el motivo llega tal cual.
        Try
            Await servicio.AnularEnvioRemoto(envio.Numero).ConfigureAwait(False)
            envio.CodigoBarras = String.Empty
            envio.Estado = CShort(-1)
            Return RespuestaCorrecta(envio, "Envío anulado en la agencia; queda como etiqueta pendiente")
        Catch ex As Exception
            Return RespuestaError(ex.Message)
        End Try
    End Function

    Private Async Function ImprimirZpl(envio As EnviosAgencia, resultado As TramitarEnvioResultadoDto) As Task
        If String.IsNullOrEmpty(resultado.EtiquetaContenido) Then
            Throw New Exception($"{NombreAgencia} no devolvió la etiqueta ZPL del envío.")
        End If

        ' El ZPL viene en base64. Lo decodificamos con la codificación ANSI del sistema (la misma
        ' que usa RawPrinterHelper.SendStringToPrinter al reconvertir), para un round-trip fiel.
        Dim zpl As String
        If String.Equals(resultado.EtiquetaCodificacion, "base64", StringComparison.OrdinalIgnoreCase) Then
            zpl = Encoding.[Default].GetString(Convert.FromBase64String(resultado.EtiquetaContenido))
        Else
            zpl = resultado.EtiquetaContenido
        End If

        Dim mainViewModel As New MainViewModel
        Dim puerto As String = Await mainViewModel.leerParametro(envio.Empresa, ClaveImpresora).ConfigureAwait(False)
        Dim unused = RawPrinterHelper.SendStringToPrinter(puerto, zpl)
    End Function

    Private Function RespuestaCorrecta(envio As EnviosAgencia, detalle As String) As RespuestaAgencia
        Return New RespuestaAgencia With {
            .Agencia = NombreAgencia,
            .Fecha = Date.Now,
            .UrlLlamada = String.Empty,
            .Exito = True,
            .CuerpoLlamada = $"Tramitar envío {envio.Numero}",
            .CuerpoRespuesta = detalle,
            .TextoRespuestaError = String.Empty
        }
    End Function

    Private Function RespuestaError(mensaje As String) As RespuestaAgencia
        Return New RespuestaAgencia With {
            .Agencia = NombreAgencia,
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
        ' El seguimiento de estados lo hace el servidor (poll de Hangfire); en el cliente no se consulta.
        Throw New Exception($"El seguimiento de estados de {NombreAgencia} lo hace el servidor; no está disponible desde aquí.")
    End Function

    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Return If(IsNothing(envio), Nothing, New estadoEnvio)
    End Function

    Public Function calcularCodigoBarras(envio As EnviosAgencia, agencia As AgenciasTransporte) As String Implements IAgencia.calcularCodigoBarras
        ' El albarán lo asigna la plataforma al tramitar (InsertarYEtiquetar); no se calcula en local.
        Return String.Empty
    End Function

    Public Sub calcularPlaza(ByVal codPostal As String, codPais As Integer, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        nemonico = NemonicoPlaza
        nombrePlaza = NombreAgencia
        telefonoPlaza = String.Empty
        emailPlaza = String.Empty
    End Sub

    Public Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgencia.LlamadaWebService
        ' "Tramitar todos" en una agencia de plataforma = cerrar el día / pedir recogida; ya no envía
        ' datos (el envío se insertó al imprimir). No-op correcto: no hay nada que tramitar aquí.
        Return Task.FromResult(RespuestaCorrecta(envio, "El envío ya se registró al imprimir."))
    End Function

    Public Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        ' La impresión va acoplada al registro (InsertarYEtiquetar/Reimprimir), que son async y
        ' necesitan el servicio. Este Sub síncrono del flujo clásico no aplica.
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
        If IsNothing(envio) OrElse String.IsNullOrWhiteSpace(envio.CodigoBarras) Then
            Return String.Empty
        End If
        ' DEUDA TEMPORAL: duplica la versión canónica de NestoAPI (RegistroSeguimientoAgencias). Se
        ' eliminará cuando la ventana de Agencias consuma el EnlaceSeguimiento del DTO del servidor
        ' (migración Nesto -> NestoAPI), en vez de calcularlo en local.
        Return EnlaceSeguimientoDe(envio.CodigoBarras.Trim())
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
            ' No se piden medidas: el servidor manda una caja estándar por defecto.
            Return False
        End Get
    End Property

    Public ReadOnly Property FlujoTramitacion As TipoFlujoTramitacion Implements IAgencia.FlujoTramitacion
        Get
            Return TipoFlujoTramitacion.RegistrarAlImprimir
        End Get
    End Property

    ' Tarifa placeholder: las tarifas reales están en NestoAPI (comparador server-side).
    Private Class TarifaPlaceholder
        Implements ITarifaAgencia

        Private ReadOnly _agenciaId As Integer
        Private ReadOnly _nombre As String

        Public Sub New(agenciaId As Integer, nombre As String)
            _agenciaId = agenciaId
            _nombre = nombre
        End Sub

        Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
            Get
                Return _agenciaId
            End Get
        End Property

        Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
            Get
                Return 0
            End Get
        End Property

        Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
            Get
                Return _nombre
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
