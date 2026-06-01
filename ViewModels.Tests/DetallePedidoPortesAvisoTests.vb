Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Modulos.PedidoVenta

''' <summary>
''' NestoAPI#211 / Nesto#365: aviso al desmarcar "servir junto" si aparecen portes por las líneas
''' sobre pedido. Se testea la lógica pura (compara base con/sin servir junto contra el umbral).
''' </summary>
<TestClass()>
Public Class DetallePedidoPortesAvisoTests

    <TestMethod()>
    Public Sub Aviso_ConServirJuntoLlegaPeroSinNo_DevuelveAviso()
        ' Con servir junto 40€ >= umbral 35€ (portes pagados), pero sin servir junto 30€ < 35€
        ' (aparecen portes) → hay que avisar.
        Dim aviso = DetallePedidoViewModel.ConstruirAvisoPortesAlDesmarcar(30D, 40D, 35D)
        Assert.IsFalse(String.IsNullOrEmpty(aviso))
    End Sub

    <TestMethod()>
    Public Sub Aviso_AmbasLleganAlUmbral_NoAvisa()
        ' Desmarcar no cambia nada: sigue habiendo portes pagados.
        Dim aviso = DetallePedidoViewModel.ConstruirAvisoPortesAlDesmarcar(40D, 40D, 35D)
        Assert.AreEqual(String.Empty, aviso)
    End Sub

    <TestMethod()>
    Public Sub Aviso_YaLlevabaPortesConServirJunto_NoAvisa()
        ' Si ni con servir junto se llega al umbral, ya llevaba portes: desmarcar no introduce nada nuevo.
        Dim aviso = DetallePedidoViewModel.ConstruirAvisoPortesAlDesmarcar(20D, 30D, 35D)
        Assert.AreEqual(String.Empty, aviso)
    End Sub

    <TestMethod()>
    Public Sub Aviso_BaseSinServirJuntoNula_NoAvisa()
        ' El backend no devolvió base (request sin líneas) → no se puede avisar.
        Dim aviso = DetallePedidoViewModel.ConstruirAvisoPortesAlDesmarcar(Nothing, 40D, 35D)
        Assert.AreEqual(String.Empty, aviso)
    End Sub

End Class
