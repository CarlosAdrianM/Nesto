Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Models.Nesto.Models
Imports Nesto.ViewModels

' NestoAPI#258 slice (b): calcularCodigoBarras ya no depende del AgenciasViewModel (recibe el
' envío y SU agencia efectiva), así que por primera vez se puede testear en aislamiento.
' Además elimina el último resto del bug de Nesto#412: Sending/ASM usaban la agencia
' seleccionada en la VENTANA, que podía no ser la del envío.
<TestClass()>
Public Class AgenciaCodigoBarrasTests

    Private Function CrearEnvio(numero As Integer, Optional servicio As Integer = 0) As EnviosAgencia
        Return New EnviosAgencia With {.Numero = numero, .Servicio = servicio}
    End Function

    Private Function CrearAgencia(prefijo As String) As AgenciasTransporte
        Return New AgenciasTransporte With {.PrefijoCodigoBarras = prefijo}
    End Function

    ' Nesto#443: el test de Sending se eliminó con la agencia (sin uso desde febrero/26).

    <TestMethod()>
    Public Sub Asm_ServicioNormal_PrefijoDeLaAgenciaDelEnvio_MasNumeroEnSieteDigitos()
        Dim agencia = New AgenciaASM()

        Dim codigo = agencia.calcularCodigoBarras(CrearEnvio(246998, servicio:=1), CrearAgencia("9"))

        Assert.AreEqual("90246998", codigo)
    End Sub

    <TestMethod()>
    Public Sub Asm_Servicio96BusinessParcel_NoUsaElPrefijoDeLaAgencia()
        Dim agencia = New AgenciaASM()

        Dim codigo = agencia.calcularCodigoBarras(CrearEnvio(246998, servicio:=96), CrearAgencia("9"))

        Assert.IsTrue(codigo.EndsWith("0246998"), "El código debe terminar con el número del envío en 7 dígitos")
        Assert.IsFalse(codigo.StartsWith("9"), "Con servicio 96 (BusinessParcel) no se usa el prefijo de la agencia")
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_NoCalculaCodigoEnLocal()
        ' El albarán lo asigna la plataforma al tramitar
        Dim agencia = New AgenciaInnovatrans()

        Assert.AreEqual(String.Empty, agencia.calcularCodigoBarras(CrearEnvio(1), CrearAgencia("5")))
    End Sub
End Class
