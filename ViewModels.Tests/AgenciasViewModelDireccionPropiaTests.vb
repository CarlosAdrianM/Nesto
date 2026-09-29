Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports Prism.Regions
Imports Prism.Services.Dialogs

' Carlos 29/09/26 (pedido 927075): un envío de la tienda online salió por CTT a nuestra dirección
' (la de la ficha genérica 31517). Registrar un envío a Río Tiétar, 11 exige confirmarlo.
<TestClass()>
Public Class AgenciasViewModelDireccionPropiaTests
    Private dialogService As IDialogService
    Private viewModel As AgenciasViewModel
    Private vecesPreguntado As Integer

    <TestInitialize()>
    Public Sub Initialize()
        dialogService = A.Fake(Of IDialogService)
        viewModel = New AgenciasViewModel(A.Fake(Of RegionManager), A.Fake(Of IAgenciaService), A.Fake(Of IConfiguracion), dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        vecesPreguntado = 0
    End Sub

    Private Sub ElUsuarioContesta(ok As Boolean)
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      vecesPreguntado += 1
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(If(ok, ButtonResult.OK, ButtonResult.Cancel))
                      callback?.Invoke(resultado)
                  End Sub)
    End Sub

    Private Shared Function Envio927075() As EnviosAgencia
        Return New EnviosAgencia With {.Numero = 249216, .Pedido = 927075, .Direccion = "C/ RÍO TIÉTAR, 11",
            .CodPostal = "28110", .Poblacion = "ALGETE"}
    End Function

    <DataTestMethod()>
    <DataRow("C/ RÍO TIÉTAR, 11", "28110")>
    <DataRow("Calle Rio Tietar 11", "28110 ")>
    <DataRow("c/ río tiétar, 11 nave", "28110")>
    Public Sub EsDireccionDeNuevaVision_NuestraDireccion(direccion As String, codPostal As String)
        Assert.IsTrue(AgenciasViewModel.EsDireccionDeNuevaVision(direccion, codPostal))
    End Sub

    <DataTestMethod()>
    <DataRow("VÁZQUEZ DE MELLA 50, 5C", "33012")>
    <DataRow("C/ MAYOR, 1", "28110")>
    <DataRow("C/ RÍO TIÉTAR, 11", "28100")>
    <DataRow(Nothing, "28110")>
    <DataRow("C/ RÍO TIÉTAR, 11", Nothing)>
    Public Sub EsDireccionDeNuevaVision_OtraDireccion(direccion As String, codPostal As String)
        Assert.IsFalse(AgenciasViewModel.EsDireccionDeNuevaVision(direccion, codPostal))
    End Sub

    <TestMethod()>
    Public Sub ConfirmarDireccionPropia_DireccionDelCliente_NoPregunta()
        ElUsuarioContesta(False)
        Dim envio = Envio927075()
        envio.Direccion = "VÁZQUEZ DE MELLA 50, 5C"
        envio.CodPostal = "33012"

        Assert.IsTrue(viewModel.ConfirmarDireccionPropia(envio))
        Assert.AreEqual(0, vecesPreguntado)
    End Sub

    <TestMethod()>
    Public Sub ConfirmarDireccionPropia_NuestraDireccionYCancela_NoSeRegistra()
        ElUsuarioContesta(False)

        Assert.IsFalse(viewModel.ConfirmarDireccionPropia(Envio927075()))
        Assert.AreEqual(1, vecesPreguntado)
    End Sub

    <TestMethod()>
    Public Sub ConfirmarDireccionPropia_Confirmado_NoVuelveAPreguntarPorElMismoEnvio()
        ElUsuarioContesta(True)
        Dim envio = Envio927075()

        Assert.IsTrue(viewModel.ConfirmarDireccionPropia(envio))
        Assert.IsTrue(viewModel.ConfirmarDireccionPropia(envio), "Imprimir y luego tramitar: una sola pregunta")
        Assert.AreEqual(1, vecesPreguntado)
    End Sub
End Class
