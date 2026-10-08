Imports System.Threading.Tasks
Imports ControlesUsuario.Services
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels

' NestoAPI#609: al reclamar la deuda (crear el enlace de pago) se propone antes la corrección del concepto; el
' elegido queda en el cuadro y es el que se manda.
<TestClass()>
Public Class ClientesViewModelRevisionConceptoTests

    Private _revisor As IRevisorConceptoPago
    Private _vm As ClientesViewModel

    <TestInitialize()>
    Public Sub Initialize()
        _revisor = A.Fake(Of IRevisorConceptoPago)()
        _vm = New ClientesViewModel(A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), A.Fake(Of IClienteComercialService)(),
                                    A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)()) With {
            .RevisorConceptoPago = _revisor
        }
    End Sub

    <TestMethod()>
    Public Sub ReclamarDeuda_ConceptoEscrito_SeRevisaYSeQuedaElElegido()
        _vm.AsuntoReclamarDeuda = "Master classn 29/09"
        A.CallTo(Function() _revisor.ElegirConcepto("Master classn 29/09", A(Of String).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult("Masterclass 29/09"))

        _vm.ReclamarDeudaCommand.Execute(Nothing)

        Assert.AreEqual("Masterclass 29/09", _vm.AsuntoReclamarDeuda)
    End Sub

    <TestMethod()>
    Public Sub ReclamarDeuda_ConceptoPorDefecto_NoSeRevisa()
        _vm.AsuntoReclamarDeuda = ClientesViewModel.ASUNTO_PAGO_POR_DEFECTO

        _vm.ReclamarDeudaCommand.Execute(Nothing)

        A.CallTo(Function() _revisor.ElegirConcepto(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Sub
End Class
