Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' 20/11/26 (Reina, víspera del inventario): una referencia leída en «Cantidad» (45929 unidades) desbordaba el contado (Short) en
''' OnInsertarProducto, sin aviso. El contado es Integer, como en la base de datos, y una cantidad así no se manda: se dice.
''' </summary>
<TestClass()>
Public Class InventarioCantidadTests

    <TestMethod()>
    Public Sub Cantidad_QueParececUnaReferencia_NoVale()
        StringAssert.Contains(InventarioViewModel.MotivoCantidadNoValida(45929), "parece una referencia")
        Assert.IsNotNull(InventarioViewModel.MotivoCantidadNoValida(-45929))
        Assert.IsNotNull(InventarioViewModel.MotivoCantidadNoValida(Integer.MaxValue))
        Assert.IsNotNull(InventarioViewModel.MotivoCantidadNoValida(Integer.MinValue), "sin desbordar al calcular el valor absoluto")
    End Sub

    <TestMethod()>
    Public Sub Cantidad_Normal_Vale()
        Assert.IsNull(InventarioViewModel.MotivoCantidadNoValida(1))
        Assert.IsNull(InventarioViewModel.MotivoCantidadNoValida(0))
        Assert.IsNull(InventarioViewModel.MotivoCantidadNoValida(-3), "negativa, para corregir lo contado de más")
        Assert.IsNull(InventarioViewModel.MotivoCantidadNoValida(InventarioViewModel.CANTIDAD_MAXIMA_LECTURA))
    End Sub

    <TestMethod()>
    Public Sub Contado_SumarUnaCantidadGrande_NoDesborda()
        Dim linea As New InventarioDTO With {.StockReal = 30000}
        linea.StockReal += 9999
        Assert.AreEqual(39999, linea.StockReal)
    End Sub
End Class
