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
    Public Sub AltoMaximo_ConElMarcoMedido_DescuentaEseMarco()
        ' Con la ventana abierta se sabe lo que ocupan de verdad el título y el borde
        Assert.AreEqual(1001.0, CopiarFacturaView.AltoMaximo(1040, 400, 39))
    End Sub

    ' Carlos, 30/09/26: en una pantalla grande el pie quedaba tapado por la barra de tareas,
    ' porque la ventana crecía hacia abajo. Tiene que quedar centrada en el área útil.
    <TestMethod()>
    Public Sub ArribaCentrado_LaVentanaQuedaCentradaEnElAreaUtil()
        ' Área útil de 1040 (sin la barra de tareas) y ventana de 800: 120 por arriba y 120 por abajo
        Dim arriba = CopiarFacturaView.ArribaCentrado(0, 1040, 800)

        Assert.AreEqual(120.0, arriba)
        Assert.IsTrue(arriba + 800 <= 1040, "El pie no puede pasar del área útil")
    End Sub

    <TestMethod()>
    Public Sub ArribaCentrado_ConLaBarraDeTareasArriba_EmpiezaDebajoDeElla()
        Assert.AreEqual(160.0, CopiarFacturaView.ArribaCentrado(40, 1040, 800))
    End Sub

    <TestMethod()>
    Public Sub ArribaCentrado_SiNoCabe_ArribaDelTodo()
        Assert.AreEqual(0.0, CopiarFacturaView.ArribaCentrado(0, 574, 700))
    End Sub

    <TestMethod()>
    Public Sub AltoMaximo_EnUnaPantallaMinuscula_NoBajaDelMinimo()
        Assert.AreEqual(400.0, CopiarFacturaView.AltoMaximo(300, 400))
    End Sub

End Class
