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
''' NestoAPI#847 (sugerencia 1103, Alfredo, 06/11/26): la etiqueta del envío 250662 (pedido 929042, cliente 1179) no salió, se
''' volvió a pulsar «Insertar e Imprimir» y quedaron tres envíos del reparto propio. La API ya no crea otro (409
''' ENVIO_REPARTO_PROPIO_REPETIDO con el envío que tiene el pedido): Agencias no borra ni enseña el error genérico y ofrece
''' reimprimir la etiqueta de ese envío, sin volver a facturar.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelEnvioRepetidoTests
    Private Const CTT As Integer = 13
    Private Const PEDIDO As Integer = 929042
    Private Const ENVIO As Integer = 250662
    Private Const MENSAJE_409 As String = "El pedido 929042 ya tiene el envío 250662 del reparto propio (1 bulto): no se crea otro. Para volver a sacar la etiqueta, reimprima la del envío 250662; si hacen falta más bultos, modifique ese envío."

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
            .Empresa = "1  ", .Número = PEDIDO, .Nº_Cliente = "1179", .Contacto = "0", .Vendedor = "MAN", .Comentarios = "",
            .Clientes = New ClienteAgenciaModel With {.Nombre = "CLIENTE", .Dirección = "C/ MAYOR, 1", .Población = "GETAFE", .Provincia = "MADRID", .CodPostal = "28905", .Teléfono = "910000000"}
        }
        A.CallTo(Function() servicio.LeerPedidoParaAgencia(A(Of String).Ignored, PEDIDO)).Returns(pedidoAgencia)
        A.CallTo(Function() servicio.LeerPedidoParaAgenciaPorNumero(PEDIDO, A(Of Boolean).Ignored)).Returns(pedidoAgencia)
        A.CallTo(Function() servicio.CargarEnvio(A(Of String).Ignored, PEDIDO)).Returns(Nothing)
        A.CallTo(Function() servicio.CargarEnvioPorClienteYDireccion(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns(Nothing)
        A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, PEDIDO)).ReturnsLazily(
            Function() New ObservableCollection(Of EnviosAgencia) From {Existente()})

        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.ComparadorAgencias = comparador
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute(Nothing)
        viewModel.pedidoSeleccionado = servicio.LeerPedidoParaAgencia("1  ", PEDIDO)
        viewModel.Peso = 3D
        Dim tcs = respuestaComparador
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        tcs.TrySetResult(New OpcionEnvioAgencia With {.AgenciaId = CTT, .Coste = 4.5D})
        Fake.ClearRecordedCalls(dialogService)
    End Sub

    Private Shared Function Agencia() As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = CTT, .Nombre = "CTT"}
    End Function

    Private Shared Function Existente() As EnviosAgencia
        Return New EnviosAgencia With {.Numero = ENVIO, .Empresa = "1  ", .Agencia = CTT, .Pedido = PEDIDO, .Estado = 1,
                .Bultos = 1, .CodigoBarras = "RP250662", .AgenciasTransporte = Agencia()}
    End Function

    Private Sub ConfirmarCon(resultado As ResultadoBoton)
        A.CallTo(Sub() dialogService.ShowConfirmation(A(Of String).Ignored, A(Of String).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .Invokes(Sub(titulo As String, mensaje As String, callback As Action(Of ResultadoDialogo))
                         callback?.Invoke(New ResultadoDialogo(resultado))
                     End Sub)
    End Sub

    ' ---- La respuesta de la API ----

    <TestMethod()>
    Public Sub DesdeRespuesta_409EnvioRepetido_ExcepcionConElEnvioYElMensaje()
        Dim cuerpo = "{""Codigo"":""ENVIO_REPARTO_PROPIO_REPETIDO"",""Message"":""" + MENSAJE_409 + """,""Envio"":250662,""CodigoBarras"":""RP250662"",""Bultos"":1,""Estado"":1}"

        Dim ex = EnvioRepartoPropioRepetidoException.DesdeRespuesta(409, cuerpo)

        Assert.IsNotNull(ex)
        Assert.AreEqual(ENVIO, ex.NumeroEnvio)
        Assert.AreEqual(MENSAJE_409, ex.Message)
    End Sub

    <TestMethod()>
    Public Sub DesdeRespuesta_OtroConflictoOtroEstadoONoJson_Nothing()
        Assert.IsNull(EnvioRepartoPropioRepetidoException.DesdeRespuesta(409, """El pedido está retenido""")) ' 409 de retenido (texto)
        Assert.IsNull(EnvioRepartoPropioRepetidoException.DesdeRespuesta(409, "{""Codigo"":""ReembolsoCeroContraReembolso"",""Message"":""x""}"))
        Assert.IsNull(EnvioRepartoPropioRepetidoException.DesdeRespuesta(400, "{""Codigo"":""ENVIO_REPARTO_PROPIO_REPETIDO"",""Envio"":1}"))
        Assert.IsNull(EnvioRepartoPropioRepetidoException.DesdeRespuesta(409, "no es json"))
    End Sub

    ' ---- Insertar e Imprimir: se ofrece reimprimir el que ya tiene ----

    <TestMethod()>
    Public Async Function Reimprimir_ConfirmaElUsuario_ReimprimeElEnvioQueYaTenia() As Task
        ConfirmarCon(ResultadoBoton.OK)
        Dim reimpreso As EnviosAgencia = Nothing
        viewModel.ReimprimirEtiquetaEnvio = Function(envio)
                                                reimpreso = envio
                                                Return Task.FromResult(New Nesto.Models.RespuestaAgencia With {.Exito = True})
                                            End Function

        Await viewModel.ReimprimirEnvioRepetidoAsync(New EnvioRepartoPropioRepetidoException(ENVIO, MENSAJE_409))

        Assert.IsNotNull(reimpreso, "se reimprime")
        Assert.AreEqual(ENVIO, reimpreso.Numero)
        Assert.AreEqual(ENVIO, viewModel.envioActual.Numero)
        A.CallTo(Sub() dialogService.ShowConfirmation(A(Of String).Ignored, A(Of String).That.Contains(MENSAJE_409), A(Of Action(Of ResultadoDialogo)).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification(A(Of String).Ignored, A(Of String).That.Contains("250662"))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.Insertar(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Reimprimir_CancelaElUsuario_NoHaceNada() As Task
        ConfirmarCon(ResultadoBoton.Cancel)
        Dim llamado As Boolean = False
        viewModel.ReimprimirEtiquetaEnvio = Function(envio)
                                                llamado = True
                                                Return Task.FromResult(New Nesto.Models.RespuestaAgencia With {.Exito = True})
                                            End Function

        Await viewModel.ReimprimirEnvioRepetidoAsync(New EnvioRepartoPropioRepetidoException(ENVIO, MENSAJE_409))

        Assert.IsFalse(llamado)
    End Function

    <TestMethod()>
    Public Async Function Reimprimir_FallaLaImpresion_SeDiceSinCrearNada() As Task
        ConfirmarCon(ResultadoBoton.OK)
        viewModel.ReimprimirEtiquetaEnvio = Function(envio) Task.FromResult(New Nesto.Models.RespuestaAgencia With {.Exito = False, .TextoRespuestaError = "sin conexión"})

        Await viewModel.ReimprimirEnvioRepetidoAsync(New EnvioRepartoPropioRepetidoException(ENVIO, MENSAJE_409))

        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("sin conexión"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.Insertar(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
    End Function
End Class
