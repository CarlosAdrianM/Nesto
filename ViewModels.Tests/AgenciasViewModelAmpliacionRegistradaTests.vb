Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Collections.ObjectModel

''' <summary>
''' Nesto#539: en el camino de siempre (InsertarRegistro), ampliar un envío de CTT o Innovatrans que YA está
''' registrado en la agencia borraba su albarán (calcularCodigoBarras devuelve "" en las agencias de plataforma) y,
''' al imprimir, se registraba otra expedición. Mismo criterio que la API (AmpliacionRegistrada): no se permite,
''' se avisa y no se graba nada. Sin registrar, y en el resto de agencias, como siempre.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelAmpliacionRegistradaTests
    Private Const ASM As Integer = 1
    Private Const CTT As Integer = 13
    Private Const PEDIDO_AMPLIACION As Integer = 928600
    Private Const PEDIDO_ORIGINAL As Integer = 928590
    Private Const NUMERO_ENVIO As Integer = 249800

    Private servicio As IAgenciaService
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private envioExistente As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        Dim configuracion = A.Fake(Of IConfiguracion)
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, "UltNumPedidoVta")).Returns("1")

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(New ObservableCollection(Of Empresas) From {empresa})
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).
            ReturnsLazily(Function() New ObservableCollection(Of AgenciasTransporte) From {Agencia(CTT, "CTT")})

        Dim pedido = New PedidoAgenciaModel With {
            .Empresa = "1  ",
            .Número = PEDIDO_AMPLIACION,
            .Nº_Cliente = "40198",
            .Contacto = "0",
            .Vendedor = "MAN",
            .Clientes = New ClienteAgenciaModel With {.Nombre = "CLIENTE", .Dirección = "CALLE", .Población = "MADRID", .Provincia = "MADRID", .CodPostal = "28001", .Teléfono = "911234567"}
        }
        A.CallTo(Function() servicio.LeerPedidoParaAgencia(A(Of String).Ignored, PEDIDO_AMPLIACION)).Returns(pedido)
        A.CallTo(Function() servicio.LeerPedidoParaAgenciaPorNumero(PEDIDO_AMPLIACION, A(Of Boolean).Ignored)).Returns(pedido)
        A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, PEDIDO_AMPLIACION)).Returns(New ObservableCollection(Of EnviosAgencia))
        ' No hay etiqueta pendiente del pedido: lo que hay es un envío EN CURSO del mismo cliente y dirección (ampliación).
        A.CallTo(Function() servicio.CargarEnvio(A(Of String).Ignored, PEDIDO_AMPLIACION)).Returns(Nothing)
        A.CallTo(Function() servicio.CargarEnvioPorClienteYDireccion(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).
            ReturnsLazily(Function() envioExistente)
        A.CallTo(Function() servicio.HayAlgunaLineaConPicking(A(Of String).Ignored, A(Of Integer).Ignored)).Returns(True)
        ' El usuario acepta siempre (la ampliación): así lo único que puede pararla es el arreglo.
        A.CallTo(Sub() dialogService.ShowConfirmation(A(Of String).Ignored, A(Of String).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
         .Invokes(Sub(titulo As String, mensaje As String, callback As Action(Of ResultadoDialogo))
                      callback?.Invoke(New ResultadoDialogo(ResultadoBoton.OK))
                  End Sub)

        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.ComparadorAgencias = A.Fake(Of IServicioComparadorAgencias)
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute(Nothing)
        viewModel.pedidoSeleccionado = pedido
        If viewModel.paisActual Is Nothing Then
            viewModel.paisActual = New Pais(34, "ESPAÑA")
        End If
    End Sub

    Private Shared Function Agencia(numero As Integer, nombre As String) As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = numero, .Nombre = nombre}
    End Function

    Private Shared Function EnvioEnCurso(agenciaNumero As Integer, codigoBarras As String) As EnviosAgencia
        Return New EnviosAgencia With {.Numero = NUMERO_ENVIO, .Empresa = "1  ", .Agencia = agenciaNumero, .Pedido = PEDIDO_ORIGINAL,
            .Cliente = "40198", .Contacto = "0", .Estado = 0, .Bultos = 1, .Reembolso = 0D, .CodPostal = "28001",
            .Nemonico = "CTT", .CodigoBarras = codigoBarras}
    End Function

    <TestMethod()>
    Public Sub InsertarRegistro_AmpliacionDeCTTYaRegistrado_NoGrabaYAvisa()
        envioExistente = EnvioEnCurso(CTT, "00342701000123456789")
        viewModel.bultos = 3
        Fake.ClearRecordedCalls(dialogService) ' Solo cuenta lo que pase al insertar

        Dim ex = Assert.ThrowsException(Of Exception)(Sub() viewModel.InsertarRegistro())

        StringAssert.Contains(ex.Message, "El envío 249800 ya está registrado en CTT (albarán 00342701000123456789)")
        StringAssert.Contains(ex.Message, "anúlelo y vuelva a insertarlo")
        Assert.AreEqual("00342701000123456789", envioExistente.CodigoBarras, "No se puede borrar el albarán de la agencia")
        Assert.AreEqual(CShort(1), CShort(envioExistente.Bultos), "Ni cambiar los bultos")
        A.CallTo(Sub() servicio.Modificar(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.Insertar(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub InsertarRegistro_AmpliacionDeCTTSinRegistrar_SeAmpliaComoHoy()
        envioExistente = EnvioEnCurso(CTT, Nothing)
        viewModel.bultos = 3
        Fake.ClearRecordedCalls(dialogService) ' Solo cuenta lo que pase al insertar

        viewModel.InsertarRegistro()

        A.CallTo(Sub() servicio.Modificar(envioExistente)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.Insertar(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
        Assert.AreEqual(CShort(3), CShort(envioExistente.Bultos))
        Assert.AreEqual(PEDIDO_AMPLIACION, CInt(envioExistente.Pedido))
        Assert.IsTrue(viewModel.HayQueImprimirEtiqueta, "Han cambiado los bultos: al imprimir se registra en CTT")
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("Se ha producido un error"))).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub MensajeAmpliacionRegistrada_CTTEInnovatransConAlbaran_NoSePermite()
        Assert.IsNotNull(AgenciasViewModel.MensajeAmpliacionRegistrada(EnvioEnCurso(CTT, "123"), New AgenciaCTT()))
        StringAssert.Contains(AgenciasViewModel.MensajeAmpliacionRegistrada(EnvioEnCurso(7, " 2470188878 "), New AgenciaInnovatrans()),
                              "ya está registrado en Innovatrans (albarán 2470188878)")
    End Sub

    <TestMethod()>
    Public Sub MensajeAmpliacionRegistrada_CTTSinAlbaran_SePermite()
        Assert.IsNull(AgenciasViewModel.MensajeAmpliacionRegistrada(EnvioEnCurso(CTT, Nothing), New AgenciaCTT()))
        Assert.IsNull(AgenciasViewModel.MensajeAmpliacionRegistrada(EnvioEnCurso(CTT, "   "), New AgenciaCTT()))
    End Sub

    <TestMethod()>
    Public Sub MensajeAmpliacionRegistrada_GLSConAlbaran_SePermiteComoSiempre()
        ' GLS (AgenciaASM) genera el código en local: ampliar no toca nada en la agencia hasta el cierre.
        Assert.IsNull(AgenciasViewModel.MensajeAmpliacionRegistrada(EnvioEnCurso(ASM, "61771000249800"), New AgenciaASM()))
    End Sub

    <TestMethod()>
    Public Sub MensajeAmpliacionRegistrada_EnvioNuevo_SePermite()
        ' Un envío nuevo (sin pedido) no es ampliación.
        Assert.IsNull(AgenciasViewModel.MensajeAmpliacionRegistrada(New EnviosAgencia With {.CodigoBarras = "123"}, New AgenciaCTT()))
    End Sub

End Class
