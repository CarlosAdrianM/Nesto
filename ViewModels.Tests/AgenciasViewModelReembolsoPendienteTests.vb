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
''' Nesto#603 / NestoAPI#785 (pedido 929609, envío 250432, 30/10/26): la etiqueta pendiente se creó con reembolso 0 antes del
''' picking y al imprimirla la agencia fue sin reembolso. Desde NestoAPI#785 el PUT que la registra contesta:
''' 200 { RowVersion, Reembolso, Avisos } (reembolso recalculado: se refresca y se avisa sin parar), 409 ReembolsoCeroContraReembolso
''' (no se registra: se enseña solo el Message, no se imprime y sigue pendiente) o, con una API anterior, 204 sin cuerpo (como siempre).
''' Aquí, la impresión entera (InsertarRegistro) con el servicio falso; el parseo de la respuesta, en AgenciaServiceModificarRespuestaTests.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelReembolsoPendienteTests
    Private Const CTT As Integer = 13
    Private Const PEDIDO As Integer = 929609
    Private Const ENVIO As Integer = 250432
    Private Const AVISO_RECALCULADO As String = "La etiqueta pendiente 250432 se creó con reembolso 0,00 € antes de que el pedido tuviera picking: se ha recalculado a 134,16 €."
    Private Const MENSAJE_409 As String = "El pedido es contra reembolso y la etiqueta lleva 0 € (envío 250432): la factura tiene 134,16 € pendientes de cobro. Ponga el reembolso en la etiqueta pendiente antes de imprimirla."

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
            .Clientes = New ClienteAgenciaModel With {.Nombre = "GUIBETH, CB", .Dirección = "C/ REAL OVIEDO, 6", .Población = "OVIEDO", .Provincia = "ASTURIAS", .CodPostal = "33013", .Teléfono = "985230561"}
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
        ' Solo cuenta lo que se le enseña al usuario al imprimir.
        Fake.ClearRecordedCalls(dialogService)
    End Sub

    Private Shared Function Agencia() As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = CTT, .Nombre = "CTT"}
    End Function

    Private Shared Function Pendiente() As EnviosAgencia
        Return New EnviosAgencia With {.Numero = ENVIO, .Empresa = "1  ", .Agencia = CTT, .Pedido = PEDIDO,
                .Estado = -1, .Servicio = 48, .Horario = 0, .Retorno = 0, .Peso = 0D, .Bultos = 0, .CodPostal = "33013",
                .Reembolso = 0D, .AgenciasTransporte = Agencia()}
    End Function

    ''' <summary>El PUT que registra la etiqueta (el Estado ya no es pendiente).</summary>
    Private Shared Function EsElRegistro() As EnviosAgencia
        Return A(Of EnviosAgencia).That.Matches(Function(e) e.Numero = ENVIO AndAlso e.Estado >= 0)
    End Function

    ' ---- 200 con cuerpo: reembolso recalculado ----

    <TestMethod()>
    Public Sub Registrar_ApiRecalculaElReembolso_SeEnseñaElAvisoYSeImprimeConElReembolsoNuevo()
        Dim registrado As EnviosAgencia = Nothing
        ' El servicio deja en el envío el Reembolso del cuerpo (AgenciaService.LeerRespuestaModificar) y devuelve los Avisos.
        A.CallTo(Function() servicio.Modificar(EsElRegistro())).Invokes(Sub(e As EnviosAgencia)
                                                                           e.Reembolso = 134.16D
                                                                           registrado = e
                                                                       End Sub) _
            .Returns(New List(Of String) From {AVISO_RECALCULADO})

        viewModel.InsertarRegistro(False, 0D)

        Assert.IsNotNull(registrado, "Se tiene que haber registrado")
        Assert.AreEqual(134.16D, viewModel.envioActual.Reembolso, "La etiqueta (GLS local) sale con el reembolso recalculado")
        A.CallTo(Sub() dialogService.ShowNotification(A(Of String).Ignored, AVISO_RECALCULADO)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        Assert.IsTrue(viewModel.HayQueImprimirEtiqueta, "El aviso no para la impresión")
    End Sub

    ' ---- 204 sin cuerpo (API anterior): como siempre ----

    <TestMethod()>
    Public Sub Registrar_ApiAnteriorSinCuerpo_ComoSiempre()
        A.CallTo(Function() servicio.Modificar(EsElRegistro())).Returns(New List(Of String)())

        viewModel.InsertarRegistro(False, 0D)

        A.CallTo(Function() servicio.Modificar(EsElRegistro())).MustHaveHappenedOnceExactly()
        Assert.AreEqual(0D, viewModel.envioActual.Reembolso)
        A.CallTo(Sub() dialogService.ShowNotification(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        Assert.IsTrue(viewModel.HayQueImprimirEtiqueta)
    End Sub

    ' ---- 409 ReembolsoCeroContraReembolso: no se registra ----

    <TestMethod()>
    Public Sub Registrar_ApiBloqueaPorReembolsoCero_SoloElMensajeNoSeImprimeYSigueEnPendientes()
        A.CallTo(Function() servicio.Modificar(EsElRegistro())).Throws(New ReembolsoCeroContraReembolsoException(ENVIO, MENSAJE_409))
        Dim restaurado As EnviosAgencia = Nothing
        A.CallTo(Function() servicio.Modificar(A(Of EnviosAgencia).That.Matches(Function(e) e.Estado < 0))).Invokes(Sub(e As EnviosAgencia) restaurado = e)

        Assert.ThrowsException(Of EnvioNoTramitadoException)(Sub() viewModel.InsertarRegistro(False, 0D),
            "Quien imprime (Insertar e imprimir) no tiene que imprimir ni enseñar más mensajes")

        A.CallTo(Sub() dialogService.ShowError(MENSAJE_409)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Not.IsEqualTo(MENSAJE_409))).MustNotHaveHappened()
        Assert.IsFalse(viewModel.HayQueImprimirEtiqueta, "No se imprime")
        Assert.IsNotNull(restaurado, "El envío vuelve a quedar como estaba: pendiente")
        Assert.AreEqual(ENVIO, restaurado.Numero)
    End Sub

    <TestMethod()>
    Public Sub Registrar_TrasElBloqueo_ElReintentoSigueSiendoElPendiente()
        ' El usuario le pone el reembolso y vuelve a imprimir: tiene que ser el mismo pendiente, no un envío nuevo.
        Dim primeraVez As Boolean = True
        A.CallTo(Function() servicio.Modificar(EsElRegistro())).Invokes(Sub(e As EnviosAgencia)
                                                                           If primeraVez Then
                                                                               primeraVez = False
                                                                               Throw New ReembolsoCeroContraReembolsoException(ENVIO, MENSAJE_409)
                                                                           End If
                                                                       End Sub)
        Assert.ThrowsException(Of EnvioNoTramitadoException)(Sub() viewModel.InsertarRegistro(False, 0D))

        viewModel.InsertarRegistro(False, 0D)

        A.CallTo(Function() servicio.Insertar(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.Modificar(EsElRegistro())).MustHaveHappenedTwiceExactly()
        Assert.IsTrue(viewModel.HayQueImprimirEtiqueta)
    End Sub
End Class
