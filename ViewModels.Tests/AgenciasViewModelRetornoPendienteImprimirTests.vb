Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' Sugerencia 906 (Alfredo, 30/10/26): el pedido 929682 tenía la etiqueta pendiente de CTT creada con retorno
''' («Recoger producto», Retorno = 1) y al imprimirla en la pestaña Pedidos se grabó sin retorno (envío 250390).
''' Igual 250391, 250271, 250177, 250162, 250161 y 250074. Aquí se reproduce la impresión ENTERA
''' (InsertarRegistro, lo que va en el PUT) y no solo AplicarServicioYRetornoAlPendiente.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelRetornoPendienteImprimirTests
    Private Const ASM As Integer = 1
    Private Const CTT As Integer = 13
    Private Const PEDIDO_CON_PENDIENTE As Integer = 929682
    Private Const OTRO_PEDIDO_CON_PENDIENTE As Integer = 929686
    Private Const PEDIDO_SIN_PENDIENTE As Integer = 929600

    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private comparador As IServicioComparadorAgencias
    Private respuestaComparador As TaskCompletionSource(Of OpcionEnvioAgencia)
    Private viewModel As AgenciasViewModel
    Private retornoGrabado As Byte?
    Private ReadOnly yaImpresos As New HashSet(Of Integer)

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
            Function() New ObservableCollection(Of AgenciasTransporte) From {Agencia(ASM, "ASM"), Agencia(CTT, "CTT")})
        A.CallTo(Function() servicio.CargarAgencia(ASM)).ReturnsLazily(Function() Agencia(ASM, "ASM"))
        A.CallTo(Function() servicio.CargarAgencia(CTT)).ReturnsLazily(Function() Agencia(CTT, "CTT"))
        A.CallTo(Function() servicio.HayAlgunaLineaConPicking(A(Of String).Ignored, A(Of Integer).Ignored)).Returns(True)
        A.CallTo(Sub() servicio.Modificar(A(Of EnviosAgencia).Ignored)).Invokes(Sub(e As EnviosAgencia)
                                                                                    retornoGrabado = e.Retorno
                                                                                    If e.Pedido.HasValue Then yaImpresos.Add(e.Pedido.Value)
                                                                                End Sub)

        ConfigurarPedido(PEDIDO_CON_PENDIENTE, 250390)
        ConfigurarPedido(OTRO_PEDIDO_CON_PENDIENTE, 250391)
        ConfigurarPedido(PEDIDO_SIN_PENDIENTE, Nothing)
    End Sub

    Private Shared Function Agencia(numero As Integer, nombre As String) As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = numero, .Nombre = nombre}
    End Function

    Private Function Pendiente(numeroEnvio As Integer, pedido As Integer) As EnviosAgencia
        Return New EnviosAgencia With {.Numero = numeroEnvio, .Empresa = "1  ", .Agencia = CTT, .Pedido = pedido,
                .Estado = -1, .Servicio = 48, .Horario = 0, .Retorno = 1, .Peso = 0D, .Bultos = 0, .CodPostal = "33013",
                .Observaciones = "Recoger producto", .AgenciasTransporte = Agencia(CTT, "CTT")}
    End Function

    Private Sub ConfigurarPedido(numero As Integer, numeroEnvioPendiente As Integer?)
        Dim pedido = New PedidoAgenciaModel With {
            .Empresa = "1  ", .Número = numero, .Nº_Cliente = "40211", .Contacto = "0", .Vendedor = "MAN",
            .Comentarios = "Recoger producto",
            .Clientes = New ClienteAgenciaModel With {.Nombre = "GUIBETH, CB", .Dirección = "C/ REAL OVIEDO, 6", .Población = "OVIEDO", .Provincia = "ASTURIAS", .CodPostal = "33013", .Teléfono = "985230561"}
        }
        A.CallTo(Function() servicio.LeerPedidoParaAgencia(A(Of String).Ignored, numero)).Returns(pedido)
        A.CallTo(Function() servicio.LeerPedidoParaAgenciaPorNumero(numero, A(Of Boolean).Ignored)).Returns(pedido)
        If numeroEnvioPendiente.HasValue Then
            ' Como la API: la pendiente sale mientras no se haya impreso, y también en la lista de envíos del pedido.
            A.CallTo(Function() servicio.CargarEnvio(A(Of String).Ignored, numero)).ReturnsLazily(
                Function() If(yaImpresos.Contains(numero), Nothing, Pendiente(numeroEnvioPendiente.Value, numero)))
            A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, numero)).ReturnsLazily(
                Function() New ObservableCollection(Of EnviosAgencia) From {Pendiente(numeroEnvioPendiente.Value, numero)})
        Else
            A.CallTo(Function() servicio.CargarEnvio(A(Of String).Ignored, numero)).Returns(Nothing)
            A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, numero)).ReturnsLazily(
                Function() New ObservableCollection(Of EnviosAgencia))
        End If
    End Sub

    Private Sub ContestaElComparador(agenciaId As Integer)
        Dim tcs = respuestaComparador
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        tcs.TrySetResult(New OpcionEnvioAgencia With {.AgenciaId = agenciaId, .Coste = 4.5D})
    End Sub

    Private Sub AbrirVentana()
        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.ComparadorAgencias = comparador
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute(Nothing)
    End Sub

    Private Sub AbrirPedido(numero As Integer)
        viewModel.pedidoSeleccionado = servicio.LeerPedidoParaAgencia("1  ", numero)
    End Sub

    Private Function Imprimir() As Byte
        retornoGrabado = Nothing
        viewModel.InsertarRegistro(False, 0D)
        Assert.IsTrue(retornoGrabado.HasValue, "Se tiene que haber grabado el envío")
        Return retornoGrabado.Value
    End Function

    ' ---- Las secuencias reales de la pestaña Pedidos (verdes también antes del arreglo; quedan de regresión) ----

    <TestMethod()>
    Public Sub Imprimir_PesoYComparadorCTT_ConservaElRetorno()
        AbrirVentana()
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        viewModel.Peso = 9D
        ContestaElComparador(CTT)
        Assert.AreEqual(CByte(1), Imprimir())
    End Sub

    <TestMethod()>
    Public Sub Imprimir_ComparadorDelServidorDiceOtraAgencia_ConservaElRetorno()
        AbrirVentana()
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        viewModel.Peso = 9D
        ContestaElComparador(ASM)
        Assert.AreEqual(CByte(1), Imprimir())
    End Sub

    <TestMethod()>
    Public Sub Imprimir_DosPendientesSeguidas_ConservanElRetorno()
        AbrirVentana()
        AbrirPedido(OTRO_PEDIDO_CON_PENDIENTE)
        viewModel.Peso = 17D
        ContestaElComparador(CTT)
        Assert.AreEqual(CByte(1), Imprimir(), "primera")
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        viewModel.Peso = 9D
        ContestaElComparador(CTT)
        Assert.AreEqual(CByte(1), Imprimir(), "segunda")
    End Sub

    <TestMethod()>
    Public Sub Imprimir_PesoTecleadoMientrasCargaElPedido_ConservaElRetorno()
        AbrirVentana()
        AbrirPedido(PEDIDO_SIN_PENDIENTE)
        Dim reembolso As New TaskCompletionSource(Of Decimal)()
        A.CallTo(Function() servicio.ImporteReembolso(A(Of String).Ignored, PEDIDO_CON_PENDIENTE)).Returns(reembolso.Task)
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        viewModel.Peso = 9D
        reembolso.SetResult(0D)
        ContestaElComparador(CTT)
        Assert.AreEqual(CByte(1), Imprimir())
    End Sub

    <TestMethod()>
    Public Sub Imprimir_RespuestasDelComparadorDesordenadas_ConservaElRetorno()
        AbrirVentana()
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        viewModel.Peso = 1D
        Dim primera = respuestaComparador
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        viewModel.Peso = 17D
        ContestaElComparador(CTT)
        primera.TrySetResult(New OpcionEnvioAgencia With {.AgenciaId = ASM, .Coste = 3D})
        Assert.AreEqual(CByte(1), Imprimir())
    End Sub

    ' ---- Los caminos que SÍ lo perdían (rojos antes del arreglo) ----

    <TestMethod()>
    Public Sub Imprimir_ReintentoTrasUnFallo_ConLaPantallaPasandoPorOtraAgencia_ConservaElRetorno()
        ' Al imprimir se olvidaba el pendiente (_envioPendienteDelPedido = Nothing) ANTES de grabar. Si el PUT falla, la
        ' compensación lo deja pendiente en la BD, pero la pantalla ya no sabe que lo es: al pasar por otra agencia y volver
        ' a CTT se quedaban los defectos (sin retorno) y el reintento lo grababa así.
        AbrirVentana()
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        viewModel.Peso = 9D
        ContestaElComparador(CTT)
        Dim primeraVez As Boolean = True
        A.CallTo(Sub() servicio.Modificar(A(Of EnviosAgencia).Ignored)).Invokes(Sub(e As EnviosAgencia)
                                                                                    retornoGrabado = e.Retorno
                                                                                    If primeraVez Then
                                                                                        primeraVez = False
                                                                                        Throw New Exception("timeout")
                                                                                    End If
                                                                                End Sub)
        viewModel.InsertarRegistro(False, 0D)
        viewModel.Peso = 10D
        ContestaElComparador(ASM)
        viewModel.Peso = 9D
        ContestaElComparador(CTT)

        Assert.AreEqual(CByte(1), Imprimir())
    End Sub

    <TestMethod()>
    Public Sub AplicarAlPendiente_PantallaSinRetornoQueNoHaElegidoElUsuario_ConservaElDelPendiente()
        ' Cualquier setter que deje la pantalla en «sin retorno» por el camino (aquí, la agencia cambiada en la pestaña
        ' En curso, que pone los defectos sin mirar el pendiente) no puede quitarle el retorno al pendiente.
        AbrirVentana()
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        viewModel.PestannaNombre = Pestannas.EN_CURSO
        viewModel.agenciaSeleccionada = viewModel.listaAgencias.Single(Function(a) a.Numero = ASM)
        viewModel.agenciaSeleccionada = viewModel.listaAgencias.Single(Function(a) a.Numero = CTT)
        Assert.AreEqual(CByte(0), viewModel.retornoActual.id, "Precondición: la pantalla se ha quedado sin retorno sin que lo toque el usuario")

        Dim envioPendiente As EnviosAgencia = Pendiente(250390, PEDIDO_CON_PENDIENTE)
        viewModel.AplicarServicioYRetornoAlPendiente(envioPendiente, New AgenciaCTT(), CTT)

        Assert.AreEqual(CByte(1), envioPendiente.Retorno)
        StringAssert.Contains(viewModel.TrazaRetorno, "retorno 1->0", "La traza para ELMAH dice por dónde se fue")
    End Sub

    ' ---- Lo que el usuario sí puede hacer ----

    <TestMethod()>
    Public Sub Imprimir_SiElUsuarioQuitaElRetornoEnElCombo_SeGrabaSinRetornoYSeLeDiceALaApi()
        AbrirVentana()
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        viewModel.Peso = 9D
        ContestaElComparador(CTT)
        Dim retornoQuitado As Byte? = Nothing
        A.CallTo(Sub() servicio.ModificarQuitandoRetorno(A(Of EnviosAgencia).Ignored)).Invokes(Sub(e As EnviosAgencia) retornoQuitado = e.Retorno)

        viewModel.retornoActual = viewModel.listaTiposRetorno.Single(Function(r) r.id = 0)
        viewModel.InsertarRegistro(False, 0D)

        Assert.AreEqual(CType(0, Byte?), retornoQuitado, "PUT con ?quitarRetorno=true")
        A.CallTo(Sub() servicio.Modificar(A(Of EnviosAgencia).That.Matches(Function(e) e.Numero = 250390 AndAlso e.Estado = 0))).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub ElegirElRetornoDeOtroPedido_NoValeParaElSiguiente()
        AbrirVentana()
        AbrirPedido(PEDIDO_SIN_PENDIENTE)
        Dim actual As Byte = viewModel.retornoActual.id
        viewModel.retornoActual = viewModel.listaTiposRetorno.First(Function(r) r.id <> actual)
        Assert.IsTrue(viewModel.RetornoElegidoPorElUsuario(PEDIDO_SIN_PENDIENTE), "Precondición: elegido a mano")
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        Assert.IsFalse(viewModel.RetornoElegidoPorElUsuario(PEDIDO_CON_PENDIENTE))
        Assert.IsFalse(viewModel.RetornoElegidoPorElUsuario(PEDIDO_SIN_PENDIENTE), "Al abrir otro pedido se olvida")
    End Sub

    <TestMethod()>
    Public Sub RutaModificarQuitandoRetorno_LlevaElParametroQueEsperaLaApi()
        Assert.AreEqual("EnviosAgencias/250390?quitarRetorno=true", AgenciaService.RutaModificarQuitandoRetorno(250390))
    End Sub
End Class
