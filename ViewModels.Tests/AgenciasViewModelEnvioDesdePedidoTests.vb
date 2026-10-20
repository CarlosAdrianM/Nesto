Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' NestoAPI#595 (slice 2): con el parámetro InsertarEnvioPorApi = "API", «Insertar» le pide el envío al servidor
''' (POST api/EnviosAgencias/DesdePedido). Con un 409 se pregunta cada pendiente con el diálogo de siempre y se repite;
''' con el 200 el envío queda en la pantalla; la etiqueta de recogida y las agencias que el servidor no soporta siguen por
''' el camino de siempre.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelEnvioDesdePedidoTests
    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private peticiones As List(Of CrearEnvioDesdePedidoDto)

    <TestInitialize()>
    Public Sub Initialize()
        configuracion = A.Fake(Of IConfiguracion)
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        peticiones = New List(Of CrearEnvioDesdePedidoDto)
    End Sub

    Private Shared Function Peticion() As CrearEnvioDesdePedidoDto
        Return New CrearEnvioDesdePedidoDto With {.Empresa = "1", .Pedido = 927700, .Bultos = 2, .Peso = 3.5D,
            .Cambios = New CambiosEnvioDesdePedidoDto With {.Agencia = 1, .Reembolso = 120D}}
    End Function

    ''' <summary>El servidor contesta, por orden, lo que se le diga (y apunta lo que le llega, copiando las confirmaciones).</summary>
    Private Sub ElServidorContesta(ParamArray respuestas As ResultadoEnvioDesdePedidoModel())
        Dim cola As New Queue(Of ResultadoEnvioDesdePedidoModel)(respuestas)
        A.CallTo(Function() servicio.CrearEnvioDesdePedido(A(Of CrearEnvioDesdePedidoDto).Ignored)) _
            .ReturnsLazily(Function(p As CrearEnvioDesdePedidoDto)
                               peticiones.Add(New CrearEnvioDesdePedidoDto With {
                                   .Confirmaciones = p.Confirmaciones.ToList(), .DescontarPrepagos = p.DescontarPrepagos, .Dimensiones = p.Dimensiones})
                               Return Task.FromResult(cola.Dequeue())
                           End Function)
    End Sub

    Private Shared Function Pendiente(codigo As String, tipo As String, texto As String) As ResultadoEnvioDesdePedidoModel
        Return New ResultadoEnvioDesdePedidoModel With {
            .Estado = EstadoEnvioDesdePedido.ConPendientes,
            .Pendientes = New List(Of AvisoEnvioDesdePedidoDto) From {New AvisoEnvioDesdePedidoDto With {.Codigo = codigo, .Tipo = tipo, .Texto = texto}}
        }
    End Function

    Private Shared Function Creado(numero As Integer, Optional origen As String = "Nuevo", Optional hayQueImprimir As Boolean = True) As ResultadoEnvioDesdePedidoModel
        Return New ResultadoEnvioDesdePedidoModel With {
            .Estado = EstadoEnvioDesdePedido.Creado,
            .Creado = New EnvioDesdePedidoDto With {
                .Envio = New EnviosAgencia With {.Numero = numero, .Agencia = 1, .Pedido = 927700, .CodigoBarras = "6119714" & numero.ToString("D7")},
                .Origen = origen, .HayQueImprimir = hayQueImprimir, .Flujo = "TramitarAlCerrar", .EtiquetaEnNesto = True}
        }
    End Function

    Private Shared Function Rechazado(codigo As String, mensaje As String) As ResultadoEnvioDesdePedidoModel
        Return New ResultadoEnvioDesdePedidoModel With {.Estado = EstadoEnvioDesdePedido.Rechazado, .CodigoError = codigo, .MensajeError = mensaje}
    End Function

    Private Sub ElUsuarioContesta(respuesta As Boolean)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(respuesta)
    End Sub

    ' ---------------------------------------------------------------- 409 → confirmar → 200

    <TestMethod()>
    Public Async Function SinPicking_SeConfirmaYSeRepite_ElEnvioQuedaEnLaPantalla() As Task
        ElUsuarioContesta(True)
        ElServidorContesta(Pendiente("SinPicking", "Confirmar", "Este pedido no tiene ninguna línea con picking."), Creado(250000))

        Dim hecho As Boolean = Await viewModel.InsertarRegistroPorApi(Peticion())

        Assert.IsTrue(hecho)
        Assert.AreEqual(2, peticiones.Count)
        Assert.AreEqual(0, peticiones(0).Confirmaciones.Count)
        CollectionAssert.AreEqual({"SinPicking"}, peticiones(1).Confirmaciones)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Pedido Sin Picking", A(Of String).That.Contains("ninguna línea con picking"))).MustHaveHappenedOnceExactly()
        Assert.AreEqual(250000, viewModel.envioActual.Numero)
        Assert.AreEqual(1, viewModel.listaEnvios.Count)
        Assert.AreEqual(1, viewModel.listaEnviosPedido.Count)
        Assert.IsTrue(viewModel.HayQueImprimirEtiqueta)
    End Function

    <TestMethod()>
    Public Async Function UsuarioCancela_NoSeRepiteNiSeGraba() As Task
        ElUsuarioContesta(False)
        ElServidorContesta(Pendiente("DireccionPropia", "Confirmar", "El envío va a la dirección de Nueva Visión."))

        Dim ex = Await Assert.ThrowsExceptionAsync(Of Exception)(Function() viewModel.InsertarRegistroPorApi(Peticion()))

        Assert.AreEqual("Cancelado por el usuario", ex.Message)
        Assert.AreEqual(1, peticiones.Count)
        Assert.IsNull(viewModel.envioActual)
    End Function

    <TestMethod()>
    Public Async Function DireccionPropiaConfirmada_NoSeVuelveAPreguntarAlImprimir() As Task
        ElUsuarioContesta(True)
        ElServidorContesta(Pendiente("DireccionPropia", "Confirmar", "El envío va a la dirección de Nueva Visión."), Creado(250001))
        Await viewModel.InsertarRegistroPorApi(Peticion())
        Dim envio As EnviosAgencia = viewModel.envioActual
        envio.Direccion = "C/ RÍO TIÉTAR, 11"
        envio.CodPostal = "28110"
        ElUsuarioContesta(False)

        Assert.IsTrue(viewModel.ConfirmarDireccionPropia(envio), "ya se confirmó antes de grabar")
    End Function

    <TestMethod()>
    Public Async Function Prepagos_ElUsuarioDescuenta_SeMandaDescontarYYaNoSePreguntaMas() As Task
        ElUsuarioContesta(True)
        ElServidorContesta(Pendiente("Prepagos", "Elegir", "El pedido tiene 50,00 € pagados por adelantado."), Creado(250002))

        Await viewModel.InsertarRegistroPorApi(Peticion())

        Assert.IsNull(peticiones(0).DescontarPrepagos)
        Assert.AreEqual(True, peticiones(1).DescontarPrepagos)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Prepago del pedido", A(Of String).Ignored)).MustHaveHappenedOnceExactly()
        Assert.IsFalse(viewModel.HayAvisoPrepagos)
    End Function

    <TestMethod()>
    Public Async Function DimensionesDeCanteras_SePidenYSeMandan() As Task
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored)).Returns(" 30x20x15 ")
        ElServidorContesta(Pendiente("Dimensiones", "Dato", "Canteras necesita las dimensiones."), Creado(250003))

        Await viewModel.InsertarRegistroPorApi(Peticion())

        Assert.AreEqual("30x20x15", peticiones(1).Dimensiones)
    End Function

    <TestMethod()>
    Public Async Function DimensionesSinFormato_NoSeGrabaNada() As Task
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored)).Returns("3")
        ElServidorContesta(Pendiente("Dimensiones", "Dato", "Canteras necesita las dimensiones."))

        Await Assert.ThrowsExceptionAsync(Of Exception)(Function() viewModel.InsertarRegistroPorApi(Peticion()))

        Assert.AreEqual(1, peticiones.Count)
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("número x número x número"))).MustHaveHappenedOnceExactly()
    End Function

    ' ---------------------------------------------------------------- ampliación

    <TestMethod()>
    Public Async Function AmpliacionConLosMismosBultos_NoHayQueImprimir_YSustituyeElEnvioDeLaLista() As Task
        Dim antiguo As New EnviosAgencia With {.Numero = 249600, .Bultos = 2}
        viewModel.listaEnvios = New ObservableCollection(Of EnviosAgencia) From {antiguo}
        viewModel.listaEnviosPedido = New ObservableCollection(Of EnviosAgencia)
        ElUsuarioContesta(True)
        ElServidorContesta(Pendiente("Ampliacion", "Confirmar", "Este pedido es una ampliación del envío 249600."),
                           Creado(249600, origen:="Ampliacion", hayQueImprimir:=False))

        Await viewModel.InsertarRegistroPorApi(Peticion())

        Assert.IsFalse(viewModel.HayQueImprimirEtiqueta)
        Assert.AreEqual(1, viewModel.listaEnvios.Count, "se sustituye, no se duplica")
        Assert.AreNotSame(antiguo, viewModel.listaEnvios.Single())
        Assert.AreEqual(249600, viewModel.envioActual.Numero)
    End Function

    ' ---------------------------------------------------------------- camino de siempre

    <TestMethod()>
    Public Async Function EtiquetaDeRecogida_SigueElCaminoDeSiempre() As Task
        ElServidorContesta(Rechazado("EtiquetaRecogida", "Haga la etiqueta de recogida desde Nesto."))

        Assert.IsFalse(Await viewModel.InsertarRegistroPorApi(Peticion()))
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function AgenciaNoSoportada_SigueElCaminoDeSiempre() As Task
        ElServidorContesta(Rechazado("AgenciaNoSoportada", "La agencia 7 no se puede insertar desde el servidor."))

        Assert.IsFalse(Await viewModel.InsertarRegistroPorApi(Peticion()))
    End Function

    <TestMethod()>
    Public Async Function OtroRechazo_LanzaConElMotivoDelServidor() As Task
        ElServidorContesta(Rechazado("AmpliacionRegistrada", "El envío 249600 ya está registrado en la agencia: anule el envío y vuelva a insertarlo."))

        Dim ex = Await Assert.ThrowsExceptionAsync(Of Exception)(Function() viewModel.InsertarRegistroPorApi(Peticion()))

        StringAssert.Contains(ex.Message, "anule el envío y vuelva a insertarlo")
    End Function

    ' ---------------------------------------------------------------- parámetro

    <DataTestMethod()>
    <DataRow("API", True)>
    <DataRow(" api ", True)>
    <DataRow("NESTO", False)>
    <DataRow("", False)>
    <DataRow(Nothing, False)>
    Public Async Function Parametro_SoloApiEnciendeElCaminoNuevo(valor As String, esperado As Boolean) As Task
        A.CallTo(Function() configuracion.leerParametro("1", Parametros.Claves.InsertarEnvioPorApi)).Returns(Task.FromResult(valor))

        Assert.AreEqual(esperado, Await viewModel.InsertarEnvioPorApiActivo())
    End Function

    <TestMethod()>
    Public Async Function Parametro_SiNoSePuedeLeer_CaminoDeSiempre() As Task
        A.CallTo(Function() configuracion.leerParametro("1", Parametros.Claves.InsertarEnvioPorApi)).Throws(New InvalidOperationException("sin red"))

        Assert.IsFalse(Await viewModel.InsertarEnvioPorApiActivo())
    End Function

End Class
