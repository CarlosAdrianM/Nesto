Imports FakeItEasy
Imports FakeItEasy.Core
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Services.ServirJunto
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Events
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports Unity

''' <summary>
''' Nesto#452: cuando el servidor deniega desmarcar "servir junto" porque hay líneas (muestras MMP,
''' bonificados sin stock) que se quedarían pendientes, DetallePedidoViewModel ofrece borrarlas y
''' desmarcar en un solo paso. Nunca borra sin confirmación, y revalida sin esas líneas ANTES de
''' borrar: si sigue denegado por otro motivo, el pedido no se toca.
''' </summary>
<TestClass()>
Public Class DetallePedidoBorrarLineasServirJuntoTests

    Private dialogService As IDialogService
    Private servicioServirJunto As IServirJuntoService
    Private mensajesDialogo As List(Of String)
    Private dialogosMostrados As List(Of String)
    Private respuestasConfirmacion As Queue(Of Boolean)
    Private lineasEnviadas As List(Of List(Of ProductoBonificadoConCantidadRequest))

    <TestInitialize()>
    Public Sub Initialize()
        dialogService = A.Fake(Of IDialogService)
        servicioServirJunto = A.Fake(Of IServirJuntoService)
        mensajesDialogo = New List(Of String)
        dialogosMostrados = New List(Of String)
        respuestasConfirmacion = New Queue(Of Boolean)
        lineasEnviadas = New List(Of List(Of ProductoBonificadoConCantidadRequest))

        A.CallTo(Sub() dialogService.ShowDialog(
                    A(Of String).Ignored,
                    A(Of IDialogParameters).Ignored,
                    A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      dialogosMostrados.Add(nombre)
                      If parametros IsNot Nothing AndAlso parametros.ContainsKey("message") Then
                          mensajesDialogo.Add(parametros.GetValue(Of String)("message"))
                      End If
                      ' ShowError llama a ShowDialog SIN callback: no hay nada que responder.
                      If callback Is Nothing Then
                          Return
                      End If
                      Dim ok = If(respuestasConfirmacion.Count > 0, respuestasConfirmacion.Dequeue(), True)
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(If(ok, ButtonResult.OK, ButtonResult.Cancel))
                      callback(resultado)
                  End Sub)
    End Sub

    Private Sub ElServidorResponde(ParamArray respuestas() As ValidarServirJuntoResponse)
        Dim llamada = A.CallTo(Function() servicioServirJunto.Validar(
            A(Of String).Ignored,
            A(Of List(Of ProductoBonificadoConCantidadRequest)).Ignored,
            A(Of List(Of ProductoBonificadoConCantidadRequest)).Ignored,
            A(Of String).Ignored,
            A(Of String).Ignored,
            A(Of String).Ignored,
            A(Of String).Ignored,
            A(Of Nullable(Of Boolean)).Ignored,
            A(Of List(Of LineaPortesServirJuntoDTO)).Ignored,
            A(Of Nullable(Of Integer)).Ignored))
        llamada.Invokes(Sub(c As IFakeObjectCall) lineasEnviadas.Add(c.GetArgument(Of List(Of ProductoBonificadoConCantidadRequest))(2))) _
               .ReturnsNextFromSequence(respuestas.Select(Function(r) Task.FromResult(r)).ToArray())
    End Sub

    Private Function CrearViewModelConPedido(ParamArray lineas() As LineaPedidoVentaWrapper) As DetallePedidoViewModel
        Dim vm = New DetallePedidoViewModel(A.Fake(Of IRegionManager), A.Fake(Of IConfiguracion), A.Fake(Of IPedidoVentaService),
                                            A.Fake(Of IEventAggregator), dialogService, A.Fake(Of IUnityContainer), A.Fake(Of IServicioAutenticacion))
        vm.ServicioServirJunto = servicioServirJunto
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .numero = 922687, .servirJunto = False})
        For Each l In lineas
            vm.pedido.Lineas.Add(l)
        Next
        Return vm
    End Function

    Private Shared Function Linea(producto As String, texto As String, Optional estado As Short = -1, Optional picking As Integer = 0) As LineaPedidoVentaWrapper
        Return New LineaPedidoVentaWrapper() With {
            .tipoLinea = 1,
            .Producto = producto,
            .texto = texto,
            .Cantidad = 1,
            .estado = estado,
            .picking = picking,
            .Almacen = "ALG"
        }
    End Function

    Private Shared Function Denegada(ParamArray productos() As String) As ValidarServirJuntoResponse
        Return New ValidarServirJuntoResponse With {
            .PuedeDesmarcar = False,
            .Mensaje = "No se puede desmarcar 'Servir junto': estas muestras (material promocional) se quedarían pendientes y no está permitido. Bórralas primero del pedido: " & String.Join(", ", productos) & ".",
            .ProductosProblematicos = productos.Select(Function(p) New ProductoSinStockDTO With {.ProductoId = p, .ProductoNombre = "MUESTRA " & p}).ToList()
        }
    End Function

    Private Shared Function Permitida(Optional aviso As String = Nothing) As ValidarServirJuntoResponse
        Return New ValidarServirJuntoResponse With {
            .PuedeDesmarcar = True,
            .Aviso = aviso,
            .ProductosProblematicos = New List(Of ProductoSinStockDTO)
        }
    End Function

    Private Shared Function Productos(lineas As IEnumerable(Of LineaPedidoVentaWrapper)) As String()
        Return lineas.Select(Function(l) l.Producto).ToArray()
    End Function

    <TestMethod()>
    Public Sub Denegado_AceptandoLaOferta_BorraLasLineasYQuedaDesmarcado()
        ' Caso real del triage de ELMAH: dos muestras MMP sin stock y "servir junto" marcado.
        ElServidorResponde(Denegada("45461", "45444"), Permitida())
        respuestasConfirmacion.Enqueue(True)
        Dim vm = CrearViewModelConPedido(
            Linea("12345", "CHAMPU 1000 ML"),
            Linea("45461", "SENSITIVE MUESTRA CREMA FACIAL"),
            Linea("45444", "MUESTRA CREMA REPARADORA SILK SENSITIVE"))

        vm.cmdValidarServirJunto.Execute(Nothing)

        CollectionAssert.AreEqual({"12345"}, Productos(vm.pedido.Lineas), "Solo tienen que quedar las líneas que no eran muestras")
        Assert.AreEqual(1, vm.pedido.Model.Lineas.Count, "El DTO del pedido también pierde las líneas")
        Assert.IsFalse(vm.pedido.servirJunto, "El desmarcado se mantiene: era lo que quería la usuaria")
        Assert.AreEqual(2, lineasEnviadas.Count, "Se valida con todas las líneas y se revalida sin las muestras")
        CollectionAssert.AreEqual({"12345"}, lineasEnviadas(1).Select(Function(l) l.ProductoId).ToArray(), "La revalidación va SIN las muestras")
        Dim oferta = mensajesDialogo.Single()
        Assert.IsTrue(oferta.Contains("45461") AndAlso oferta.Contains("SENSITIVE MUESTRA CREMA FACIAL"), "La oferta lista código y nombre: " & oferta)
        Assert.IsTrue(oferta.Contains("45444"), oferta)
        Assert.IsTrue(oferta.Contains("borrarlas"), oferta)
    End Sub

    <TestMethod()>
    Public Sub Denegado_SiCancelaLaOferta_NoBorraNadaYVuelveAMarcar()
        ElServidorResponde(Denegada("45461"))
        respuestasConfirmacion.Enqueue(False)
        Dim vm = CrearViewModelConPedido(Linea("12345", "CHAMPU"), Linea("45461", "MUESTRA"))

        vm.cmdValidarServirJunto.Execute(Nothing)

        CollectionAssert.AreEqual({"12345", "45461"}, Productos(vm.pedido.Lineas), "Sin confirmación no se borra ninguna línea")
        Assert.IsTrue(vm.pedido.servirJunto, "Se revierte el desmarcado, como antes de Nesto#452")
        Assert.AreEqual(1, lineasEnviadas.Count, "Si cancela no hace falta revalidar")
    End Sub

    <TestMethod()>
    Public Sub Denegado_SiSigueDenegadoSinEsasLineas_NoBorraNadaYLoExplica()
        ' Tras quitar las muestras seguiría denegado (p. ej. bonificado Ganavisiones sin stock):
        ' no se deja el pedido a medias.
        ElServidorResponde(Denegada("45461"), Denegada("77777"))
        respuestasConfirmacion.Enqueue(True)
        Dim vm = CrearViewModelConPedido(Linea("12345", "CHAMPU"), Linea("45461", "MUESTRA"), Linea("77777", "REGALO"))

        vm.cmdValidarServirJunto.Execute(Nothing)

        CollectionAssert.AreEqual({"12345", "45461", "77777"}, Productos(vm.pedido.Lineas), "No se borra nada si el desmarcado seguiría denegado")
        Assert.IsTrue(vm.pedido.servirJunto)
        Assert.AreEqual(2, lineasEnviadas.Count)
        Dim aviso = mensajesDialogo.Last()
        Assert.IsTrue(aviso.Contains("No se ha borrado ninguna línea"), aviso)
        Assert.IsTrue(aviso.Contains("77777"), "Se enseña el motivo nuevo del servidor: " & aviso)
    End Sub

    <TestMethod()>
    Public Sub Denegado_SiCancelaElAvisoPosterior_NoBorraLasLineas()
        ' La revalidación pasa pero trae un aviso (comisión contra reembolso). Si la usuaria cancela
        ' ahí, las líneas siguen en el pedido y servir junto vuelve a marcarse.
        ElServidorResponde(Denegada("45461"), Permitida(aviso:="Al desmarcar se cobra comisión contra reembolso en cada envío."))
        respuestasConfirmacion.Enqueue(True)   ' acepta borrar y desmarcar
        respuestasConfirmacion.Enqueue(False)  ' pero cancela ante el aviso
        Dim vm = CrearViewModelConPedido(Linea("12345", "CHAMPU"), Linea("45461", "MUESTRA"))

        vm.cmdValidarServirJunto.Execute(Nothing)

        CollectionAssert.AreEqual({"12345", "45461"}, Productos(vm.pedido.Lineas), "Las líneas solo se borran cuando todo el desmarcado queda aceptado")
        Assert.IsTrue(vm.pedido.servirJunto)
    End Sub

    <TestMethod()>
    Public Sub Denegado_ConLineaEnPicking_NoOfreceBorrarYEnsenaElMensajeDelServidor()
        ElServidorResponde(Denegada("45461"))
        Dim vm = CrearViewModelConPedido(Linea("12345", "CHAMPU"), Linea("45461", "MUESTRA", picking:=5))

        vm.cmdValidarServirJunto.Execute(Nothing)

        CollectionAssert.AreEqual({"12345", "45461"}, Productos(vm.pedido.Lineas))
        Assert.IsTrue(vm.pedido.servirJunto)
        Assert.AreEqual(1, lineasEnviadas.Count)
        Assert.IsFalse(dialogosMostrados.Contains("ConfirmationDialog"), "Con picking no se ofrece borrar")
        Assert.IsTrue(mensajesDialogo.Single().Contains("Bórralas primero"), "Se enseña el mensaje del servidor, como antes")
    End Sub

    <TestMethod()>
    Public Sub Denegado_SiElProductoProblematicoNoEstaEnElPedido_NoOfreceBorrar()
        ' Borrar no resolvería nada: se comporta como antes de Nesto#452.
        ElServidorResponde(Denegada("45461", "99999"))
        Dim vm = CrearViewModelConPedido(Linea("12345", "CHAMPU"), Linea("45461", "MUESTRA"))

        vm.cmdValidarServirJunto.Execute(Nothing)

        CollectionAssert.AreEqual({"12345", "45461"}, Productos(vm.pedido.Lineas))
        Assert.IsTrue(vm.pedido.servirJunto)
        Assert.IsFalse(dialogosMostrados.Contains("ConfirmationDialog"))
    End Sub

    <TestMethod()>
    Public Sub Permitido_NoTocaLasLineasNiPreguntaNada()
        ElServidorResponde(Permitida())
        Dim vm = CrearViewModelConPedido(Linea("12345", "CHAMPU"), Linea("45461", "MUESTRA"))

        vm.cmdValidarServirJunto.Execute(Nothing)

        CollectionAssert.AreEqual({"12345", "45461"}, Productos(vm.pedido.Lineas))
        Assert.IsFalse(vm.pedido.servirJunto)
        Assert.AreEqual(0, dialogosMostrados.Count)
    End Sub

    <TestMethod()>
    Public Sub LineasQueImpidenDesmarcar_ComparaSinRellenoNiMayusculasYSoloLineasDeProducto()
        Dim lineaTexto = New LineaPedidoVentaWrapper() With {.tipoLinea = 0, .Producto = "45461", .texto = "comentario"}
        Dim lineas = {Linea("45461 ", "MUESTRA"), Linea("12345", "CHAMPU"), lineaTexto}
        Dim problematicos = {New ProductoSinStockDTO With {.ProductoId = "45461"}}

        Dim resultado = DetallePedidoViewModel.LineasQueImpidenDesmarcar(lineas, problematicos)

        Assert.AreEqual(1, resultado.Count)
        Assert.AreSame(lineas(0), resultado(0))
    End Sub

    <TestMethod()>
    Public Sub SePuedeOfrecerBorrarlas_SoloConLineasPendientesOEnCursoSinPicking()
        Dim problematicos = {New ProductoSinStockDTO With {.ProductoId = "45461"}}
        Assert.IsTrue(DetallePedidoViewModel.SePuedeOfrecerBorrarlas({Linea("45461", "M", estado:=-1)}, problematicos), "pendiente")
        Assert.IsTrue(DetallePedidoViewModel.SePuedeOfrecerBorrarlas({Linea("45461", "M", estado:=1)}, problematicos), "en curso")
        Assert.IsFalse(DetallePedidoViewModel.SePuedeOfrecerBorrarlas({Linea("45461", "M", estado:=2)}, problematicos), "albarán")
        Assert.IsFalse(DetallePedidoViewModel.SePuedeOfrecerBorrarlas({Linea("45461", "M", picking:=7)}, problematicos), "picking")
        Assert.IsFalse(DetallePedidoViewModel.SePuedeOfrecerBorrarlas(New LineaPedidoVentaWrapper() {}, problematicos), "sin líneas")
        Assert.IsFalse(DetallePedidoViewModel.SePuedeOfrecerBorrarlas({Linea("45461", "M")}, New ProductoSinStockDTO() {}), "sin problemáticos")
    End Sub

    <TestMethod()>
    Public Sub ConstruirOfertaBorrarYDesmarcar_ListaCodigoYNombreDeCadaLinea()
        Dim texto = DetallePedidoViewModel.ConstruirOfertaBorrarYDesmarcar({Linea("45461 ", "SENSITIVE MUESTRA CREMA FACIAL "), Linea("45444", "MUESTRA CREMA REPARADORA")})

        Assert.IsTrue(texto.Contains("· 45461 SENSITIVE MUESTRA CREMA FACIAL"), texto)
        Assert.IsTrue(texto.Contains("· 45444 MUESTRA CREMA REPARADORA"), texto)
        Assert.IsTrue(texto.Contains("desmarcar 'Servir junto'?"), texto)
    End Sub

End Class
