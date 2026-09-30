Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Modulos.PedidoVenta

''' <summary>
''' Incidencia 435 (30/09/26): en un portátil la ventana de «Copiar Factura» era más alta que la
''' pantalla y el cliente destino y «Ejecutar» quedaban fuera, sin forma de llegar a ellos.
''' </summary>
<TestClass()>
Public Class CopiarFacturaViewAltoTests

    <TestMethod()>
    Public Sub AltoMaximo_EnUnPortatil_LaVentanaNoPasaDeLaPantalla()
        ' 1366x768 al 125 %: unas 574 unidades de alto de área de trabajo
        Dim alto = CopiarFacturaView.AltoMaximo(574, 400)

        Assert.IsTrue(alto < 574, "La ventana con su marco tiene que caber en la pantalla")
        Assert.AreEqual(514.0, alto)
    End Sub

    <TestMethod()>
    Public Sub AltoMaximo_EnUnaPantallaGrande_DejaSitioAlMarcoYAlTitulo()
        Assert.AreEqual(980.0, CopiarFacturaView.AltoMaximo(1040, 400))
    End Sub

    <TestMethod()>
    Public Sub AltoMaximo_EnUnaPantallaMinuscula_NoBajaDelMinimo()
        Assert.AreEqual(400.0, CopiarFacturaView.AltoMaximo(300, 400))
    End Sub

End Class
