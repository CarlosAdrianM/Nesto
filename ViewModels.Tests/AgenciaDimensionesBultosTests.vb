Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Nesto.Models.Nesto.Models

' Nesto#367: dimensiones de bultos obligatorias en Canteras. Las agencias exponen el flag
' DimensionesBultosObligatorias; al tramitar se piden las dimensiones (popup) y se guardan en
' Observaciones, y el correo a Canteras las incluye junto a bultos y peso (que vienen del envío).
<TestClass()>
Public Class AgenciaDimensionesBultosTests

    <TestMethod()>
    Public Sub Canteras_ExigeDimensionesBultos()
        Assert.IsTrue(New AgenciaCanteras().DimensionesBultosObligatorias)
    End Sub

    <TestMethod()>
    Public Sub CorreosExpress_NoExigeDimensionesBultos()
        Assert.IsFalse(New AgenciaCorreosExpress().DimensionesBultosObligatorias)
    End Sub

    <TestMethod()>
    Public Sub Sending_NoExigeDimensionesBultos()
        Assert.IsFalse(New AgenciaSending().DimensionesBultosObligatorias)
    End Sub

    <TestMethod()>
    Public Sub ComponerCuerpoCorreo_ConDimensiones_IncluyeMedidasBultosYPeso()
        Dim envio As New EnviosAgencia With {
            .Cliente = "15191",
            .Contacto = "0",
            .Nombre = "Peluquería Test",
            .Bultos = 2,
            .Peso = 5.5D,
            .Observaciones = "2 cajas 30x20x15"
        }

        Dim cuerpo As String = AgenciaCanteras.ComponerCuerpoCorreo(envio)

        ' Las dimensiones vienen del popup (Observaciones); bultos y peso, del propio EnviosAgencia.
        StringAssert.Contains(cuerpo, "Medidas de los bultos: 2 cajas 30x20x15")
        StringAssert.Contains(cuerpo, "2 bultos")
        StringAssert.Contains(cuerpo, "5.5")
    End Sub

    <TestMethod()>
    Public Sub ComponerCuerpoCorreo_SinObservaciones_NoIncluyeLineaMedidas()
        Dim envio As New EnviosAgencia With {
            .Cliente = "15191",
            .Nombre = "Peluquería Test",
            .Bultos = 1,
            .Peso = 3D,
            .Observaciones = Nothing
        }

        Dim cuerpo As String = AgenciaCanteras.ComponerCuerpoCorreo(envio)

        Assert.IsFalse(cuerpo.Contains("Medidas de los bultos:"), "Sin Observaciones no debe salir la línea de medidas")
    End Sub

    <TestMethod()>
    Public Sub CombinarObservaciones_VaciasDevuelveSoloDimensiones()
        Assert.AreEqual("30x20x15", AgenciasViewModel.CombinarObservaciones(Nothing, "30x20x15"))
        Assert.AreEqual("30x20x15", AgenciasViewModel.CombinarObservaciones("   ", "30x20x15"))
    End Sub

    <TestMethod()>
    Public Sub CombinarObservaciones_ConContenidoPreviojAnadeSinPisar()
        Assert.AreEqual("Frágil - 30x20x15", AgenciasViewModel.CombinarObservaciones("Frágil", "30x20x15"))
    End Sub

    <DataTestMethod()>
    <DataRow("30x20x15")>
    <DataRow("30 x 20 x 15")>
    <DataRow("30x20x15 cm")>
    <DataRow("30,5x20x15")>
    <DataRow("2 cajas de 30x20x15")>
    Public Sub EsFormatoDimensionesValido_ConNumeroPorNumeroPorNumero_EsVerdadero(texto As String)
        Assert.IsTrue(AgenciasViewModel.EsFormatoDimensionesValido(texto))
    End Sub

    <DataTestMethod()>
    <DataRow("3")>
    <DataRow("30x20")>
    <DataRow(".")>
    <DataRow("a")>
    <DataRow("")>
    Public Sub EsFormatoDimensionesValido_SinTresNumeros_EsFalso(texto As String)
        Assert.IsFalse(AgenciasViewModel.EsFormatoDimensionesValido(texto))
    End Sub

    <TestMethod()>
    Public Sub EsFormatoDimensionesValido_Nothing_EsFalso()
        Assert.IsFalse(AgenciasViewModel.EsFormatoDimensionesValido(Nothing))
    End Sub

End Class
