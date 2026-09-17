Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Services
Imports Nesto.ViewModels

''' <summary>
''' Nesto#340 (Agencias 1D): el saldo de la cuenta de reembolsos (sumaContabilidad de la ventana de
''' Agencias) ya no es un Aggregate de Entity Framework, sino GET api/Contabilidades/Saldo. Aquí se
''' verifica el contrato del servicio con el lector inyectado, como CargarEmpresa con LectorEmpresas.
''' </summary>
<TestClass()>
Public Class AgenciaServiceSaldoTests

    Private Shared Function CrearServicio(lector As Func(Of String, String, Double?)) As AgenciaService
        Dim servicio As New AgenciaService(A.Fake(Of IServicioAgenciasMantenimiento)())
        servicio.LectorSaldo = lector
        Return servicio
    End Function

    <TestMethod()>
    Public Sub CalcularSumaContabilidad_PideElSaldoALaApiConEmpresaYCuentaRecortadas()
        Dim empresaPedida As String = Nothing, cuentaPedida As String = Nothing
        Dim servicio = CrearServicio(Function(empresa, cuenta)
                                         empresaPedida = empresa
                                         cuentaPedida = cuenta
                                         Return 123.45
                                     End Function)

        Dim saldo = servicio.CalcularSumaContabilidad("1  ", "55500043  ")

        Assert.AreEqual(123.45, saldo)
        Assert.AreEqual("1", empresaPedida, "Empresa es char(3): viaja recortada")
        Assert.AreEqual("55500043", cuentaPedida)
    End Sub

    <TestMethod()>
    Public Sub CalcularSumaContabilidad_SinCuentaDeReembolsos_NoLlamaALaApi()
        ' Agencias sin CuentaReembolsos (Canteras, CTT): la ventana enseña 0 sin ir al servidor.
        Dim llamadas As Integer = 0
        Dim servicio = CrearServicio(Function(empresa, cuenta)
                                         llamadas += 1
                                         Return 1.0
                                     End Function)

        Assert.IsNull(servicio.CalcularSumaContabilidad("1", Nothing))
        Assert.IsNull(servicio.CalcularSumaContabilidad("1", "   "))
        Assert.AreEqual(0, llamadas)
    End Sub

End Class
