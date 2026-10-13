Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels

' Nesto#490 / Nesto#340 (RDLC → QuestPDF, 13/10/26): el mandato SEPA se pide a NestoAPI con &motor=QuestPDF
' solo si el usuario tiene MotorPdfMandatoSepa = QuestPDF. Va al cliente: sin fila, con otro valor o si la
' lectura falla, sigue el RDLC de siempre (la URL de antes, sin motor).
<TestClass()>
Public Class ClientesViewModelMandatoPdfTests

    Private _configuracion As IConfiguracion
    Private _vm As ClientesViewModel

    <TestInitialize()>
    Public Sub Initialize()
        _configuracion = A.Fake(Of IConfiguracion)()
        _vm = New ClientesViewModel(_configuracion, A.Fake(Of IServicioDialogos)(), A.Fake(Of IClienteComercialService)(),
                                    A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)())
    End Sub

    <TestMethod()>
    Public Sub ConstruirUrlMandatoPdf_SinMotor_EsLaUrlDeSiempre()
        Dim url = ClientesViewModel.ConstruirUrlMandatoPdf("https://api/", "1  ", "15191 ", "0  ", "1  ", Nothing)

        Assert.AreEqual("https://api/Clientes/MandatoPDF?empresa=1&cliente=15191&contacto=0&ccc=1", url)
    End Sub

    <TestMethod()>
    Public Sub ConstruirUrlMandatoPdf_ConRdlc_NoAnadeMotor()
        Dim url = ClientesViewModel.ConstruirUrlMandatoPdf("https://api/", "1", "15191", "0", "1", "RDLC")

        Assert.IsFalse(url.Contains("motor="))
    End Sub

    <TestMethod()>
    Public Sub ConstruirUrlMandatoPdf_ConQuestPdf_AnadeMotor()
        Dim url = ClientesViewModel.ConstruirUrlMandatoPdf("https://api/", "1", "15191", "0", "1", " questpdf ")

        Assert.AreEqual("https://api/Clientes/MandatoPDF?empresa=1&cliente=15191&contacto=0&ccc=1&motor=QuestPDF", url)
    End Sub

    <TestMethod()>
    Public Async Function LeerMotorPdfMandato_LeeElParametroDelUsuario() As Task
        A.CallTo(Function() _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.MotorPdfMandatoSepa)) _
            .Returns(Task.FromResult("QuestPDF"))

        Assert.AreEqual("QuestPDF", Await _vm.LeerMotorPdfMandato())
    End Function

    <TestMethod()>
    Public Async Function LeerMotorPdfMandato_SiFallaLaLectura_Rdlc() As Task
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.MotorPdfMandatoSepa)) _
            .ThrowsAsync(New Exception("sin conexión"))

        Assert.AreEqual("RDLC", Await _vm.LeerMotorPdfMandato())
    End Function

End Class
