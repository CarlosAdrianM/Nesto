Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' NestoAPI#870 (envío 250950, reenvío del 250875 de CTT incidentado: «Tramitar todos» se lo llevó sin estar aquí): el reenvío
''' nace en Pendientes y la API no deja imprimir su etiqueta, pasarlo a en curso ni tramitarlo hasta que la caja del envío original
''' haya vuelto (409 ESPERANDO_CAJA). En Nesto: la columna «Caja» y el botón «Ya está aquí» en Devueltos; al imprimir o tramitar
''' uno, el texto de la API y «Ya está aquí, imprimir/tramitar» (repite con yaEstaAqui=true); en «Tramitar todos», se salta y se
''' resume al final.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelEsperandoCajaTests
    Private Const MENSAJE_API As String = "La caja del envío 250875 todavía no ha vuelto al almacén: el envío 250950 es su reenvío y no puede salir hasta que esté aquí. Cuando llegue, léala en Ariadna («Ha vuelto un paquete») o pulse «Ya está aquí» en Agencias › Devueltos."
    Private Const CUERPO_409 As String = "{""Message"":""" & MENSAJE_API & """,""Codigo"":""ESPERANDO_CAJA"",""Mensaje"":""" & MENSAJE_API & """,""EnvioOrigen"":250875,""EnvioNuevo"":250950}"

    Private Shared Function Esperando() As EsperandoCajaException
        Return New EsperandoCajaException(MENSAJE_API, 250875, 250950)
    End Function

    ' ---------------------------------------------------------------- contrato

    <TestMethod()>
    Public Sub DesdeRespuesta_El409EsperandoCaja()
        Dim ex = EsperandoCajaException.DesdeRespuesta(409, CUERPO_409)

        Assert.IsNotNull(ex)
        Assert.AreEqual(MENSAJE_API, ex.Message, "El texto de la API tal cual")
        Assert.AreEqual(250875, ex.EnvioOrigen)
        Assert.AreEqual(250950, ex.EnvioNuevo)
    End Sub

    <TestMethod()>
    Public Sub DesdeRespuesta_OtrosConflictosNo()
        Assert.IsNull(EsperandoCajaException.DesdeRespuesta(400, CUERPO_409))
        Assert.IsNull(EsperandoCajaException.DesdeRespuesta(409, "{""Message"":""otro"",""Codigo"":""CAMBIO_SIN_AGENCIA""}"))
        Assert.IsNull(EsperandoCajaException.DesdeRespuesta(409, """Ya decidido"""))
        Assert.IsNull(EsperandoCajaException.DesdeRespuesta(409, "<html>"))
    End Sub

    <TestMethod()>
    Public Sub LeerRespuestaModificar_409EsperandoCaja_Lanza()
        Dim envio As New EnviosAgencia With {.Numero = 250950}

        Dim ex = Assert.ThrowsException(Of EsperandoCajaException)(Sub() AgenciaService.LeerRespuestaModificar(envio, 409, CUERPO_409))

        Assert.AreEqual(250875, ex.EnvioOrigen)
    End Sub

    <TestMethod()>
    Public Sub Rutas_YaEstaAquiEnLaQuery()
        Assert.AreEqual("EnviosAgencias/250950?yaEstaAqui=true", AgenciaService.RutaModificarYaEstaAqui(250950, False))
        Assert.AreEqual("EnviosAgencias/250950?quitarRetorno=true&yaEstaAqui=true", AgenciaService.RutaModificarYaEstaAqui(250950, True))
        Assert.AreEqual("EnviosAgencias/250950/Tramitar", AgenciaService.RutaTramitarRemoto(250950, False))
        Assert.AreEqual("EnviosAgencias/250950/Tramitar?yaEstaAqui=true", AgenciaService.RutaTramitarRemoto(250950, True))
        Assert.AreEqual("EnviosAgencias/250950/EtiquetaZpl?yaEstaAqui=true", AgenciaService.RutaEtiquetaGls(250950, True))
        Assert.AreEqual("EnviosAgencias/250950/ConfirmarTramitacion", AgenciaService.RutaConfirmarTramitacion(250950, False))
        Assert.AreEqual("EnviosAgencias/250950/ConfirmarTramitacion?yaEstaAqui=true", AgenciaService.RutaConfirmarTramitacion(250950, True))
        Assert.AreEqual("EnviosAgencias/250875/HaVuelto", AgenciaService.RutaHaVuelto(250875))
    End Sub

    ' ---------------------------------------------------------------- columna «Caja»

    <TestMethod()>
    Public Sub TextoCaja_EsperandoHaVueltoOVacio()
        Assert.AreEqual("Esperando la caja", New EnvioDevueltoModel With {.ReenvioEsperandoCaja = True, .EnvioNuevo = 250950}.TextoCaja)
        Assert.AreEqual("Ha vuelto (16/11 09:05)", New EnvioDevueltoModel With {.HaVuelto = True, .FechaHaVuelto = New Date(2026, 11, 16, 9, 5, 0)}.TextoCaja)
        Assert.AreEqual(String.Empty, New EnvioDevueltoModel().TextoCaja)
    End Sub

    <TestMethod()>
    Public Sub DetalleCaja_QuienYComo()
        Dim vuelto As New EnvioDevueltoModel With {.HaVuelto = True, .FechaHaVuelto = New Date(2026, 11, 16, 9, 5, 0),
            .UsuarioHaVuelto = "NUEVAVISION\Alfredo", .ComoHaVuelto = "Lectura"}

        Assert.AreEqual("Ha vuelto el 16/11/2026 09:05 por NUEVAVISION\Alfredo (leída en Ariadna)", vuelto.DetalleCaja)
        StringAssert.Contains(New EnvioDevueltoModel With {.ReenvioEsperandoCaja = True, .EnvioNuevo = 250950}.DetalleCaja, "El reenvío 250950 no puede salir")
        Assert.IsNull(New EnvioDevueltoModel().DetalleCaja)
    End Sub

    ' ---------------------------------------------------------------- botón «Ya está aquí»

    Private Function ViewModelDevueltos(servicio As IAgenciaService, dialogos As IServicioDialogos, devuelto As EnvioDevueltoModel) As AgenciasViewModel
        Dim vm As New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogos, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        vm.empresaSeleccionada = New Empresas With {.Número = "1  "}
        vm.listaDevueltos = New ObservableCollection(Of EnvioDevueltoModel) From {devuelto}
        vm.DevueltoSeleccionado = devuelto
        Return vm
    End Function

    <TestMethod()>
    Public Async Function YaEstaAqui_LlamaEnsenaElMensajeYRefresca() As Task
        Dim servicio = A.Fake(Of IAgenciaService)
        Dim dialogos = A.Fake(Of IServicioDialogos)
        Dim devuelto As New EnvioDevueltoModel With {.Numero = 250875, .Decision = EnvioDevueltoModel.REENVIAR, .EnvioNuevo = 250950, .ReenvioEsperandoCaja = True}
        Dim refrescado As New EnvioDevueltoModel With {.Numero = 250875, .Decision = EnvioDevueltoModel.REENVIAR, .EnvioNuevo = 250950, .HaVuelto = True}
        A.CallTo(Function() servicio.MarcarHaVuelto(250875)).Returns(Task.FromResult(New EnvioHaVueltoModel With {
            .Envio = 250875, .EnvioNuevo = 250950, .Mensaje = "Ha vuelto la caja del envío 250875: ya puede imprimir la etiqueta del reenvío 250950."}))
        A.CallTo(Function() servicio.CargarListaDevueltos("1  ")).Returns(Task.FromResult(New List(Of EnvioDevueltoModel) From {refrescado}))
        Dim vm = ViewModelDevueltos(servicio, dialogos, devuelto)
        Assert.IsTrue(vm.YaEstaAquiDevueltoCommand.CanExecute(Nothing))

        Await vm.YaEstaAquiDevueltoSeleccionado()

        A.CallTo(Function() servicio.MarcarHaVuelto(250875)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogos.ShowNotification("Ya está aquí", "Ha vuelto la caja del envío 250875: ya puede imprimir la etiqueta del reenvío 250950.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CargarListaDevueltos("1  ")).MustHaveHappenedOnceExactly()
        Assert.AreSame(refrescado, vm.DevueltoSeleccionado)
        Assert.IsFalse(vm.YaEstaAquiDevueltoCommand.CanExecute(Nothing), "Ya ha vuelto: el botón se apaga")
        Assert.IsFalse(vm.EstaOcupado)
    End Function

    <TestMethod()>
    Public Async Function YaEstaAqui_LaApiRechaza_SuMensajeTalCualYNoRefresca() As Task
        Dim servicio = A.Fake(Of IAgenciaService)
        Dim dialogos = A.Fake(Of IServicioDialogos)
        A.CallTo(Function() servicio.MarcarHaVuelto(A(Of Integer).Ignored)).Throws(New Exception("El envío 250875 todavía no ha salido del almacén."))
        Dim vm = ViewModelDevueltos(servicio, dialogos, New EnvioDevueltoModel With {.Numero = 250875})

        Await vm.YaEstaAquiDevueltoSeleccionado()

        A.CallTo(Sub() dialogos.ShowError("El envío 250875 todavía no ha salido del almacén.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CargarListaDevueltos(A(Of String).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(vm.EstaOcupado)
    End Function

    <TestMethod()>
    Public Async Function YaEstaAqui_YaHaVuelto_NiSeOfreceNiLlama() As Task
        Dim servicio = A.Fake(Of IAgenciaService)
        Dim vm = ViewModelDevueltos(servicio, A.Fake(Of IServicioDialogos), New EnvioDevueltoModel With {.Numero = 250875, .HaVuelto = True})

        Assert.IsFalse(vm.YaEstaAquiDevueltoCommand.CanExecute(Nothing))
        Await vm.YaEstaAquiDevueltoSeleccionado()

        A.CallTo(Function() servicio.MarcarHaVuelto(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    ' ---------------------------------------------------------------- tramitar (agencias clásicas)

    Private Shared Function EnvioEnCurso(numero As Integer, pedido As Integer) As EnviosAgencia
        Return New EnviosAgencia With {.Numero = numero, .Agencia = 7, .Empresa = "1  ", .Pedido = pedido, .Estado = 0,
            .Direccion = "C/ INVENTADA, 1", .CodPostal = "28043"}
    End Function

    Private Shared Function AgenciaQueRegistra() As IAgencia
        Dim agencia = A.Fake(Of IAgencia)
        A.CallTo(Function() agencia.LlamadaWebService(A(Of EnviosAgencia).Ignored, A(Of IAgenciaService).Ignored)).
            ReturnsLazily(Function() Task.FromResult(New RespuestaAgencia With {.Exito = True}))
        Return agencia
    End Function

    Private Function ViewModelTramitar(servicio As IAgenciaService, dialogos As IServicioDialogos) As AgenciasViewModel
        A.CallTo(Function() servicio.CargarListaEnvios(A(Of Integer).Ignored)).Returns(New ObservableCollection(Of EnviosAgencia))
        Return New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogos, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
    End Function

    <TestMethod()>
    Public Async Function TramitarTodos_ElReenvioSinCajaSeSaltaSinPreguntarYSeResume() As Task
        Dim servicio = A.Fake(Of IAgenciaService)
        Dim dialogos = A.Fake(Of IServicioDialogos)
        Dim vm = ViewModelTramitar(servicio, dialogos)
        Dim reenvio = EnvioEnCurso(250950, 925137)
        Dim normal = EnvioEnCurso(250951, 925138)
        A.CallTo(Function() servicio.TramitarEnvio(reenvio, False)).Throws(Esperando())
        A.CallTo(Function() servicio.TramitarEnvio(normal, False)).Returns("Envío del pedido 925138 tramitado correctamente.")

        Dim saltados = Await vm.TramitarEnviosClasicos(New List(Of EnviosAgencia) From {reenvio, normal}, AgenciaQueRegistra())

        CollectionAssert.AreEqual(New List(Of EnviosAgencia) From {reenvio}, saltados)
        A.CallTo(Function() servicio.TramitarEnvio(normal, False)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.TramitarEnvio(A(Of EnviosAgencia).Ignored, True)).MustNotHaveHappened()
        A.CallTo(Function() dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogos.ShowNotification("Esperando la caja", A(Of String).That.StartsWith("1 envío esperando que vuelva su caja: 250950 (pedido 925137)."))).
            MustHaveHappenedOnceExactly()
        StringAssert.StartsWith(vm.mensajeError, "1 envío esperando que vuelva su caja: 250950 (pedido 925137).")
    End Function

    <TestMethod()>
    Public Async Function TramitarTodos_SinReenvios_NiAvisoNiResumen() As Task
        Dim servicio = A.Fake(Of IAgenciaService)
        Dim dialogos = A.Fake(Of IServicioDialogos)
        Dim vm = ViewModelTramitar(servicio, dialogos)
        A.CallTo(Function() servicio.TramitarEnvio(A(Of EnviosAgencia).Ignored, False)).Returns("Tramitado correctamente.")

        Dim saltados = Await vm.TramitarEnviosClasicos(New List(Of EnviosAgencia) From {EnvioEnCurso(1, 2), EnvioEnCurso(3, 4)}, AgenciaQueRegistra())

        Assert.AreEqual(0, saltados.Count)
        A.CallTo(Function() servicio.TramitarEnvio(A(Of EnviosAgencia).Ignored, False)).MustHaveHappenedTwiceExactly()
        A.CallTo(Sub() dialogos.ShowNotification(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Tramitar_UnoSuelto_YaEstaAqui_RepiteForzando() As Task
        Dim servicio = A.Fake(Of IAgenciaService)
        Dim dialogos = A.Fake(Of IServicioDialogos)
        Dim vm = ViewModelTramitar(servicio, dialogos)
        Dim reenvio = EnvioEnCurso(250950, 925137)
        A.CallTo(Function() servicio.TramitarEnvio(reenvio, False)).Throws(Esperando())
        A.CallTo(Function() servicio.TramitarEnvio(reenvio, True)).Returns("Envío del pedido 925137 tramitado correctamente.")
        A.CallTo(Function() dialogos.ShowConfirmationAnswer("Ya está aquí, tramitar", A(Of String).Ignored, True)).Returns(True)

        Dim saltado = Await vm.TramitarUnEnvio(reenvio, AgenciaQueRegistra(), enBucle:=False)

        Assert.IsNull(saltado)
        A.CallTo(Function() dialogos.ShowConfirmationAnswer("Ya está aquí, tramitar", A(Of String).That.Contains(MENSAJE_API), True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.TramitarEnvio(reenvio, True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CargarListaEnvios(A(Of Integer).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Tramitar_UnoSuelto_NoEstaAqui_NoSeTramitaYSeQuedaElMensaje() As Task
        Dim servicio = A.Fake(Of IAgenciaService)
        Dim dialogos = A.Fake(Of IServicioDialogos)
        Dim vm = ViewModelTramitar(servicio, dialogos)
        Dim reenvio = EnvioEnCurso(250950, 925137)
        A.CallTo(Function() servicio.TramitarEnvio(reenvio, False)).Throws(Esperando())
        A.CallTo(Function() dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(False)

        Dim unused = Await vm.TramitarUnEnvio(reenvio, AgenciaQueRegistra(), enBucle:=False)

        A.CallTo(Function() servicio.TramitarEnvio(reenvio, True)).MustNotHaveHappened()
        Assert.AreEqual(MENSAJE_API, vm.mensajeError)
        A.CallTo(Sub() dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    ' ---------------------------------------------------------------- GLS por la API

    <TestMethod()>
    Public Async Function TramitarTodosGls_LosEsperandoCaja_UnSoloAvisoConElResumen() As Task
        Dim servicio = A.Fake(Of IAgenciaService)
        Dim dialogos = A.Fake(Of IServicioDialogos)
        Dim vm = ViewModelTramitar(servicio, dialogos)
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).Returns(Task.FromResult(
            New ResultadoTramitarEnviosGlsDto With {.Envios = New List(Of ResultadoTramitarEnvioGlsDto) From {
                New ResultadoTramitarEnvioGlsDto With {.Numero = 250901, .Pedido = 925001, .Resultado = ResultadoTramitarEnvioGlsDto.TRAMITADO, .Mensaje = "ok"},
                New ResultadoTramitarEnvioGlsDto With {.Numero = 250950, .Pedido = 925137, .Resultado = ResultadoTramitarEnvioGlsDto.ESPERANDO_CAJA, .Mensaje = MENSAJE_API},
                New ResultadoTramitarEnvioGlsDto With {.Numero = 250952, .Pedido = 925140, .Resultado = ResultadoTramitarEnvioGlsDto.ESPERANDO_CAJA, .Mensaje = MENSAJE_API}}}))
        Dim envios As New List(Of EnviosAgencia) From {EnvioEnCurso(250901, 925001), EnvioEnCurso(250950, 925137), EnvioEnCurso(250952, 925140)}
        For Each e In envios
            e.Agencia = 1
        Next

        Dim total = Await vm.TramitarGlsPorApi(envios)

        Assert.AreEqual(2, total.EnviosEsperandoCaja.Count())
        A.CallTo(Sub() dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogos.ShowNotification("Esperando la caja", A(Of String).That.Contains(
            "2 envíos esperando que vuelva su caja: 250950 (pedido 925137), 250952 (pedido 925140)."))).MustHaveHappenedOnceExactly()
        StringAssert.StartsWith(vm.mensajeError, "Se ha tramitado 1 envío de GLS.")
    End Function

    <TestMethod()>
    Public Sub MensajeTramitarGls_UnoSoloEsperandoCaja_ElTextoDeLaApi()
        Dim resultado As New ResultadoTramitarEnviosGlsDto With {.Envios = New List(Of ResultadoTramitarEnvioGlsDto) From {
            New ResultadoTramitarEnvioGlsDto With {.Numero = 250950, .Pedido = 925137, .Resultado = ResultadoTramitarEnvioGlsDto.ESPERANDO_CAJA, .Mensaje = MENSAJE_API}}}

        Assert.AreEqual(MENSAJE_API, AgenciasViewModel.MensajeTramitarGls(resultado, Nothing))
    End Sub

    ' ---------------------------------------------------------------- imprimir (CTT/Innovatrans por la API)

    <TestMethod()>
    Public Async Function InsertarYEtiquetar_EsperandoCaja_LanzaParaQueSePregunte_YForzadoPasaYaEstaAqui() As Task
        Dim servicio = A.Fake(Of IAgenciaService)()
        A.CallTo(Function() servicio.TramitarEnvioRemoto(250950, False)).Throws(Esperando())
        A.CallTo(Function() servicio.TramitarEnvioRemoto(250950, True)).
            Returns(Task.FromResult(New TramitarEnvioResultadoDto With {.Albaran = "CTT0001", .Bultos = 1}))
        Dim envio As New EnviosAgencia With {.Numero = 250950, .Estado = 0}

        Await Assert.ThrowsExceptionAsync(Of EsperandoCajaException)(Function() New AgenciaCTT().InsertarYEtiquetar(envio, servicio))
        Dim unused = Await New AgenciaCTT().InsertarYEtiquetar(envio, servicio, yaEstaAqui:=True)

        A.CallTo(Function() servicio.TramitarEnvioRemoto(250950, True)).MustHaveHappenedOnceExactly()
        Assert.AreEqual("CTT0001", envio.CodigoBarras)
    End Function
End Class

''' <summary>
''' NestoAPI#870: imprimir una etiqueta PENDIENTE que es un reenvío cuya caja no ha vuelto (el PUT que la pasa a en curso contesta
''' 409 ESPERANDO_CAJA). Mismo montaje que AgenciasViewModelReembolsoPendienteTests (InsertarRegistro con el servicio falso).
''' </summary>
<TestClass()>
Public Class AgenciasViewModelEsperandoCajaPendienteTests
    Private Const CTT As Integer = 13
    Private Const PEDIDO As Integer = 925137
    Private Const ENVIO As Integer = 250950
    Private Const MENSAJE_API As String = "La caja del envío 250875 todavía no ha vuelto al almacén: el envío 250950 es su reenvío y no puede salir hasta que esté aquí."

    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private comparador As IServicioComparadorAgencias
    Private respuestaComparador As TaskCompletionSource(Of OpcionEnvioAgencia)
    Private viewModel As AgenciasViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IServicioDialogos)
        comparador = A.Fake(Of IServicioComparadorAgencias)
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        A.CallTo(Function() comparador.MasEconomica(A(Of String).Ignored, A(Of String).Ignored, A(Of Decimal).Ignored, A(Of Decimal).Ignored, A(Of String).Ignored)) _
            .ReturnsLazily(Function() respuestaComparador.Task)

        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, "UltNumPedidoVta")).Returns("1")

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(New ObservableCollection(Of Empresas) From {empresa})
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).ReturnsLazily(
            Function() New ObservableCollection(Of AgenciasTransporte) From {Agencia()})
        A.CallTo(Function() servicio.CargarAgencia(CTT)).ReturnsLazily(Function() Agencia())
        A.CallTo(Function() servicio.HayAlgunaLineaConPicking(A(Of String).Ignored, A(Of Integer).Ignored)).Returns(True)

        Dim pedidoAgencia = New PedidoAgenciaModel With {
            .Empresa = "1  ", .Número = PEDIDO, .Nº_Cliente = "40211", .Contacto = "0", .Vendedor = "MAN", .Comentarios = "",
            .Clientes = New ClienteAgenciaModel With {.Nombre = "PELUQUERÍA EJEMPLO", .Dirección = "C/ REAL OVIEDO, 6", .Población = "OVIEDO", .Provincia = "ASTURIAS", .CodPostal = "33013", .Teléfono = "985230561"}
        }
        A.CallTo(Function() servicio.LeerPedidoParaAgencia(A(Of String).Ignored, PEDIDO)).Returns(pedidoAgencia)
        A.CallTo(Function() servicio.LeerPedidoParaAgenciaPorNumero(PEDIDO, A(Of Boolean).Ignored)).Returns(pedidoAgencia)
        A.CallTo(Function() servicio.CargarEnvio(A(Of String).Ignored, PEDIDO)).ReturnsLazily(Function() Pendiente())
        A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, PEDIDO)).ReturnsLazily(
            Function() New ObservableCollection(Of EnviosAgencia) From {Pendiente()})

        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.ComparadorAgencias = comparador
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute(Nothing)
        viewModel.pedidoSeleccionado = servicio.LeerPedidoParaAgencia("1  ", PEDIDO)
        viewModel.Peso = 9D
        Dim tcs = respuestaComparador
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        tcs.TrySetResult(New OpcionEnvioAgencia With {.AgenciaId = CTT, .Coste = 4.5D})
        Fake.ClearRecordedCalls(dialogService)
        ' El PUT que lo pasa a en curso: la API lo para.
        A.CallTo(Function() servicio.Modificar(EsElRegistro())).Throws(New EsperandoCajaException(MENSAJE_API, 250875, ENVIO))
    End Sub

    Private Shared Function Agencia() As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = CTT, .Nombre = "CTT"}
    End Function

    Private Shared Function Pendiente() As EnviosAgencia
        Return New EnviosAgencia With {.Numero = ENVIO, .Empresa = "1  ", .Agencia = CTT, .Pedido = PEDIDO,
                .Estado = -1, .Servicio = 48, .Horario = 0, .Retorno = 0, .Peso = 0D, .Bultos = 0, .CodPostal = "33013",
                .Reembolso = 0D, .AgenciasTransporte = Agencia()}
    End Function

    Private Shared Function EsElRegistro() As EnviosAgencia
        Return A(Of EnviosAgencia).That.Matches(Function(e) e.Numero = ENVIO AndAlso e.Estado >= 0)
    End Function

    <TestMethod()>
    Public Sub Imprimir_YaEstaAqui_RepiteElMismoPutForzandoEImprime()
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Ya está aquí, imprimir", A(Of String).Ignored, True)).Returns(True)
        A.CallTo(Function() servicio.ModificarYaEstaAqui(EsElRegistro(), False)).Returns(New List(Of String)())

        viewModel.InsertarRegistro(False, 0D)

        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Ya está aquí, imprimir", A(Of String).That.Contains(MENSAJE_API), True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.ModificarYaEstaAqui(EsElRegistro(), False)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        Assert.IsTrue(viewModel.HayQueImprimirEtiqueta)
    End Sub

    <TestMethod()>
    Public Sub Imprimir_NoEstaAqui_NoSeImprimeNiSeRepiteYSigueEnPendientes()
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(False)
        Dim restaurado As EnviosAgencia = Nothing
        A.CallTo(Function() servicio.Modificar(A(Of EnviosAgencia).That.Matches(Function(e) e.Estado < 0))).Invokes(Sub(e As EnviosAgencia) restaurado = e)

        Assert.ThrowsException(Of EnvioNoTramitadoException)(Sub() viewModel.InsertarRegistro(False, 0D),
            "Quien imprime no tiene que imprimir ni enseñar más mensajes")

        A.CallTo(Function() servicio.ModificarYaEstaAqui(A(Of EnviosAgencia).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(viewModel.HayQueImprimirEtiqueta)
        Assert.IsNotNull(restaurado, "Se deja como estaba (pendiente)")
    End Sub
End Class
