Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta

''' <summary>
''' ELMAH 02/10/26 (Javier, pedido 927535): dos líneas nuevas llegaron a la API sin usuario y el PUT falló
''' («El campo Usuario es obligatorio»). Las líneas nuevas son de quien edita el pedido; las existentes no se tocan.
''' </summary>
<TestClass()>
Public Class DetallePedidoAutorLineasTests

    <TestMethod()>
    Public Sub AsignarAutorLineasNuevas_LineaNuevaSinUsuario_EsDeQuienEdita()
        Dim nueva As New LineaPedidoVentaDTO With {.id = 0, .Usuario = Nothing}

        DetallePedidoViewModel.AsignarAutorLineasNuevas({nueva}, "NUEVAVISION\Javier")

        Assert.AreEqual("NUEVAVISION\Javier", nueva.Usuario)
    End Sub

    <TestMethod()>
    Public Sub AsignarAutorLineasNuevas_LineaExistente_ConservaSuAutor()
        Dim existente As New LineaPedidoVentaDTO With {.id = 328631000, .Usuario = "NUEVAVISION\Alfredo"}
        Dim existenteSinUsuario As New LineaPedidoVentaDTO With {.id = 328631100, .Usuario = Nothing}

        DetallePedidoViewModel.AsignarAutorLineasNuevas({existente, existenteSinUsuario}, "NUEVAVISION\Javier")

        Assert.AreEqual("NUEVAVISION\Alfredo", existente.Usuario)
        Assert.IsNull(existenteSinUsuario.Usuario, "Una línea que ya existía no se atribuye a quien edita ahora")
    End Sub

    <TestMethod()>
    Public Sub AsignarAutorLineasNuevas_LineaNuevaConUsuario_NoSeCambia()
        Dim nueva As New LineaPedidoVentaDTO With {.id = 0, .Usuario = "NUEVAVISION\Laura"}

        DetallePedidoViewModel.AsignarAutorLineasNuevas({nueva}, "NUEVAVISION\Javier")

        Assert.AreEqual("NUEVAVISION\Laura", nueva.Usuario)
    End Sub

End Class
