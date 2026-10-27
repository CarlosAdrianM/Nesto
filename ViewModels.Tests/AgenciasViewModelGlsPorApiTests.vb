Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Collections.ObjectModel
Imports System.Text
Imports System.Threading.Tasks

''' <summary>
''' NestoAPI#552 (fase 4): con el parámetro GlsPorApi = "API", la etiqueta de GLS (agencia 1) la monta la API y Tramitar /
''' Tramitar todos van por POST api/EnviosAgencias/TramitarTodos. Sin el parámetro, AgenciaASM como siempre. Todo con fakes.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelGlsPorApiTests
    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private agenciaAsm As IAgencia
    Private impresiones As List(Of String)

    <TestInitialize()>
    Public Sub Initialize()
        configuracion = A.Fake(Of IConfiguracion)
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        agenciaAsm = A.Fake(Of IAgencia)
        impresiones = New List(Of String)
        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.MandarZplAImpresoraGls = Function(envio, zpl)
                                               impresiones.Add(zpl)
                                               Return Task.CompletedTask
                                           End Function
        A.CallTo(Function() servicio.CargarListaEnvios(A(Of Integer).Ignored)).Returns(New ObservableCollection(Of EnviosAgencia))
    End Sub

    Private Sub ParametroGlsPorApi(valor As String)
        A.CallTo(Function() configuracion.leerParametro("1", Parametros.Claves.GlsPorApi)).Returns(Task.FromResult(valor))
    End Sub

    Private Shared Function Envio(numero As Integer, Optional agencia As Integer = 1) As EnviosAgencia
        Return New EnviosAgencia With {.Numero = numero, .Agencia = agencia, .Empresa = "1  ", .Pedido = 928000 + numero Mod 1000,
            .Direccion = "C/ INVENTADA, 1", .CodPostal = "28043", .CodigoBarras = "6119714" & numero.ToString("D7")}
    End Function

    Private Shared Function Resultado(ParamArray envios As ResultadoTramitarEnvioGlsDto()) As ResultadoTramitarEnviosGlsDto
        Return New ResultadoTramitarEnviosGlsDto With {.Envios = envios.ToList()}
    End Function

    Private Shared Function Tramitado(numero As Integer) As ResultadoTramitarEnvioGlsDto
        Return New ResultadoTramitarEnvioGlsDto With {.Numero = numero, .Pedido = 928000 + numero Mod 1000,
            .Resultado = ResultadoTramitarEnvioGlsDto.TRAMITADO, .Mensaje = $"Envío del pedido {928000 + numero Mod 1000} tramitado correctamente."}
    End Function

    ' ---------------------------------------------------------------- el parámetro

    <DataTestMethod()>
    <DataRow("API", True)>
    <DataRow(" api ", True)>
    <DataRow("NESTO", False)>
    <DataRow(Nothing, False)>
    Public Async Function GlsPorApiActivo_SoloConApi(valor As String, esperado As Boolean) As Task
        ParametroGlsPorApi(valor)

        Assert.AreEqual(esperado, Await viewModel.GlsPorApiActivo())
    End Function

    <TestMethod()>
    Public Async Function GlsPorApiActivo_SiNoSePuedeLeer_ElCaminoDeSiempre() As Task
        A.CallTo(Function() configuracion.leerParametro("1", Parametros.Claves.GlsPorApi)).Throws(New InvalidOperationException("sin red"))

        Assert.IsFalse(Await viewModel.GlsPorApiActivo())
    End Function

    <TestMethod()>
    Public Async Function DebeTramitarGlsPorApi_LaAgencia1YLaDeLaEmpresaEspejo() As Task
        ParametroGlsPorApi("API")

        Assert.IsTrue(Await viewModel.DebeTramitarGlsPorApi(1))
        Assert.IsTrue(Await viewModel.DebeTramitarGlsPorApi(5), "Agencia 5 (GLS de la empresa espejo): a la API igual que la 1 (Carlos, 27/10/26)")
        Assert.IsFalse(Await viewModel.DebeTramitarGlsPorApi(13))
    End Function

    <TestMethod()>
    Public Async Function DebeTramitarGlsPorApi_SinElParametro_NiLa1NiLa5() As Task
        ParametroGlsPorApi("NESTO")

        Assert.IsFalse(Await viewModel.DebeTramitarGlsPorApi(1))
        Assert.IsFalse(Await viewModel.DebeTramitarGlsPorApi(5))
    End Function

    ' ---------------------------------------------------------------- la etiqueta

    <TestMethod()>
    Public Async Function Etiqueta_ConElParametro_LaMontaLaApiYNoAgenciaAsm() As Task
        ParametroGlsPorApi("API")
        Dim zpl As String = "^XA^FDNUEVA VISIÓN^FS^XZ"
        A.CallTo(Function() servicio.LeerEtiquetaGls(249723)).Returns(Task.FromResult(New EtiquetaEnvioGlsDto With {
            .Envio = 249723, .EtiquetaTipo = "ZPL", .EtiquetaCodificacion = "base64",
            .EtiquetaContenido = Convert.ToBase64String(CodePagesEncodingProvider.Instance.GetEncoding(1252).GetBytes(zpl))}))

        Await viewModel.ImprimirEtiquetaClasica(agenciaAsm, Envio(249723))

        Assert.AreEqual(zpl, impresiones.Single(), "Los mismos caracteres que montaba AgenciaASM (la Ó en 1252)")
        A.CallTo(Sub() agenciaAsm.imprimirEtiqueta(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Etiqueta_SinElParametro_AgenciaAsmComoSiempre() As Task
        ParametroGlsPorApi("NESTO")
        Dim envioGls = Envio(249723)

        Await viewModel.ImprimirEtiquetaClasica(agenciaAsm, envioGls)

        A.CallTo(Sub() agenciaAsm.imprimirEtiqueta(envioGls)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.LeerEtiquetaGls(A(Of Integer).Ignored)).MustNotHaveHappened()
        Assert.AreEqual(0, impresiones.Count)
    End Function

    <TestMethod()>
    Public Async Function Etiqueta_EmpresaEspejoConElParametro_TambienLaMontaLaApi() As Task
        ParametroGlsPorApi("API")
        Dim envioEspejo = Envio(249723, agencia:=5)
        A.CallTo(Function() servicio.LeerEtiquetaGls(249723)).Returns(Task.FromResult(New EtiquetaEnvioGlsDto With {
            .Envio = 249723, .EtiquetaTipo = "ZPL", .EtiquetaCodificacion = "texto", .EtiquetaContenido = "^XA^XZ"}))

        Await viewModel.ImprimirEtiquetaClasica(agenciaAsm, envioEspejo)

        Assert.AreEqual("^XA^XZ", impresiones.Single())
        A.CallTo(Sub() agenciaAsm.imprimirEtiqueta(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
    End Function

    ' ---------------------------------------------------------------- Tramitar todos

    <TestMethod()>
    Public Async Function TramitarTodos_MandaLosEnviosEnLotesYJuntaElResultado() As Task
        Dim lotes As New List(Of List(Of Integer))
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).ReturnsLazily(
            Function(numeros As List(Of Integer), agencia As Integer)
                lotes.Add(numeros.ToList())
                Return Task.FromResult(Resultado(numeros.Select(Function(n) Tramitado(n)).ToArray()))
            End Function)
        Dim envios = Enumerable.Range(1, 7).Select(Function(i) Envio(249700 + i)).ToList()

        Dim total = Await viewModel.TramitarGlsPorApi(envios)

        Assert.AreEqual(2, lotes.Count)
        Assert.AreEqual(AgenciasViewModel.TAMANO_LOTE_TRAMITAR_GLS, lotes(0).Count)
        Assert.AreEqual(7, total.Tramitados)
        Assert.AreEqual("Se han tramitado 7 envíos de GLS.", viewModel.mensajeError)
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.CargarListaEnvios(1)).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function TramitarTodos_ConUnFallo_LoEnsenaEnUnDialogoConElPedido() As Task
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).Returns(Task.FromResult(Resultado(
            Tramitado(249701),
            New ResultadoTramitarEnvioGlsDto With {.Numero = 249702, .Pedido = 928702, .Resultado = ResultadoTramitarEnvioGlsDto.YA_TRAMITADO},
            New ResultadoTramitarEnvioGlsDto With {.Numero = 249703, .Pedido = 928703, .Resultado = ResultadoTramitarEnvioGlsDto.ERROR,
                .Mensaje = "GLS no ha aceptado el envío 249703: Ya se ha enviado este pedido para esta fecha y cliente"})))

        Dim unused = Await viewModel.TramitarGlsPorApi({Envio(249701), Envio(249702), Envio(249703)})

        StringAssert.StartsWith(viewModel.mensajeError, "Se ha tramitado 1 envío de GLS. 1 ya estaba tramitado.")
        StringAssert.Contains(viewModel.mensajeError, "- Pedido 928703 (envío 249703): GLS no ha aceptado el envío 249703")
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("No se ha podido tramitar 1 envío:"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function TramitarTodos_SiSeCortaLaPeticion_DiceQueSePuedeRepetir() As Task
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).Throws(New Exception("NestoAPI no ha tramitado los envíos de GLS (500)"))

        Dim unused = Await viewModel.TramitarGlsPorApi({Envio(249701)})

        StringAssert.Contains(viewModel.mensajeError, "Vuelva a pulsar «Tramitar todos»")
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function TramitarTodos_UnoANuestraDireccionSinConfirmar_NoSeManda() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(False)
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).Returns(Task.FromResult(Resultado(Tramitado(249701))))
        Dim propia = Envio(249702)
        propia.Direccion = "C/ Río Tiétar, 11"
        propia.CodPostal = "28110"

        Dim unused = Await viewModel.TramitarGlsPorApi({Envio(249701), propia})

        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).That.Matches(Function(l) l.Count = 1 AndAlso l(0) = 249701), 1)) _
            .MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function TramitarUno_DevuelveElMensajeDelServidor() As Task
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).Returns(Task.FromResult(Resultado(Tramitado(249701))))

        Dim unused = Await viewModel.TramitarGlsPorApi({Envio(249701)})

        Assert.AreEqual("Envío del pedido 928701 tramitado correctamente.", viewModel.mensajeError)
    End Function

    <TestMethod()>
    Public Async Function TramitarTodos_CadaEnvioVaConSuAgencia() As Task
        Dim llamadas As New List(Of (Agencia As Integer, Numeros As List(Of Integer)))
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).ReturnsLazily(
            Function(numeros As List(Of Integer), agencia As Integer)
                llamadas.Add((agencia, numeros.ToList()))
                Return Task.FromResult(Resultado(numeros.Select(Function(n) Tramitado(n)).ToArray()))
            End Function)

        Dim unused = Await viewModel.TramitarGlsPorApi({Envio(249701, agencia:=5), Envio(249702, agencia:=5)})

        Assert.AreEqual(5, llamadas.Single().Agencia)
        CollectionAssert.AreEqual({249701, 249702}, llamadas.Single().Numeros.ToArray())
    End Function

    ' ---------------------------------------------------------------- -33 con datos cambiados (fase 5)

    <TestMethod()>
    Public Async Function TramitarTodos_ConAvisoDeGls_LoEnsenaParaRevisarlo() As Task
        Dim conAviso = Tramitado(249702)
        conAviso.YaEstabaEnGls = True
        conAviso.Aviso = "GLS ya tenía grabado el envío del pedido 928702, pero con otros datos: Bultos: «2» → «3». Se ha dado por tramitado: revíselo con GLS para que lo entreguen con los datos de ahora."
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).Returns(Task.FromResult(Resultado(Tramitado(249701), conAviso)))

        Dim unused = Await viewModel.TramitarGlsPorApi({Envio(249701), Envio(249702)})

        StringAssert.StartsWith(viewModel.mensajeError, "Se han tramitado 2 envíos de GLS.")
        StringAssert.Contains(viewModel.mensajeError, "Revise con GLS 1 envío:")
        StringAssert.Contains(viewModel.mensajeError, "- Pedido 928702 (envío 249702): GLS ya tenía grabado el envío del pedido 928702, pero con otros datos: Bultos: «2» → «3».")
        A.CallTo(Sub() dialogService.ShowNotification("Revise con GLS", A(Of String).That.Contains("Bultos: «2» → «3»"))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub MensajeTramitarGls_UnoSoloConAviso_NoSeQuedaEnElMensajeCorto()
        Dim conAviso = Tramitado(249701)
        conAviso.Aviso = "revíselo con GLS"

        Dim mensaje = AgenciasViewModel.MensajeTramitarGls(Resultado(conAviso), Nothing)

        StringAssert.Contains(mensaje, "Revise con GLS 1 envío:")
        StringAssert.Contains(mensaje, "revíselo con GLS")
    End Sub

    <TestMethod()>
    Public Async Function TramitarTodos_SinAvisos_NoSaleNingunDialogo() As Task
        A.CallTo(Function() servicio.TramitarEnviosGls(A(Of List(Of Integer)).Ignored, A(Of Integer).Ignored)).Returns(Task.FromResult(Resultado(Tramitado(249701), Tramitado(249702))))

        Dim unused = Await viewModel.TramitarGlsPorApi({Envio(249701), Envio(249702)})

        A.CallTo(Sub() dialogService.ShowNotification(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    ' ---------------------------------------------------------------- el contrato con la API

    <TestMethod()>
    Public Sub Contrato_CuerpoYRutas()
        Assert.AreEqual("{""Agencia"":1,""Envios"":[249701,249702]}", AgenciaService.CuerpoTramitarTodos(New List(Of Integer) From {249701, 249702}, 1))
        Assert.AreEqual("{""Agencia"":5,""Envios"":[249701]}", AgenciaService.CuerpoTramitarTodos(New List(Of Integer) From {249701}, 5))
        Assert.AreEqual("Aviso", NameOf(ResultadoTramitarEnvioGlsDto.Aviso), "Mismo nombre que ResultadoTramitarEnvioDTO.Aviso de la API")
        Assert.AreEqual("EnviosAgencias/TramitarTodos", AgenciaService.RUTA_TRAMITAR_TODOS)
        Assert.AreEqual("EnviosAgencias/249701/EtiquetaZpl", AgenciaService.RutaEtiquetaGls(249701))
    End Sub
End Class
