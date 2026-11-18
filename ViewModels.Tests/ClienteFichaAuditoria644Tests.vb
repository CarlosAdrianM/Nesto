Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels

' Nesto#644 (auditoría de la ficha de cliente, 17/11/26), tanda 1: hallazgos 1 a 4. Un test de regresión por hallazgo.
<TestClass()>
Public Class ClienteFichaAuditoria644Tests

    Private _servicio As IClienteComercialService

    <TestInitialize()>
    Public Sub Initialize()
        _servicio = A.Fake(Of IClienteComercialService)()
    End Sub

    Private Function Comercial() As ClienteComercialViewModel
        Return New ClienteComercialViewModel(A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), _servicio,
                                             A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)())
    End Function

    Private Shared Function UnCliente(numero As String, correo As String, telefono As String) As ClienteJson
        Return New ClienteJson With {
            .empresa = "1", .cliente = numero, .contacto = "0", .nombre = "Cliente " & numero, .telefono = telefono,
            .PersonasContacto = New ObservableCollection(Of PersonaContactoJson) From {
                New PersonaContactoJson With {.CorreoElectronico = correo, .Cargo = 0}
            }
        }
    End Function

    ' Hallazgo 1: el correo y el móvil del cobro de un pedido eran los del cliente anterior (solo se rellenaban al cargar las deudas).
    <TestMethod()>
    Public Async Function H1_AlCambiarDeCliente_ElEnlaceDePagoDeUnPedidoNuncaVaAlCorreoNiAlMovilDelAnterior() As Task
        Dim servicioPedidos = A.Fake(Of IPedidoVentaService)()
        Dim vm = Comercial()
        vm.ServicioPedidos = servicioPedidos
        Dim pedidoDeA As New PedidoVentaDTO With {.empresa = "1", .numero = 927295, .cliente = "30676", .formaPago = "RCB", .plazosPago = "CONTADO"}
        pedidoDeA.Lineas.Add(New LineaPedidoVentaDTO With {.Cantidad = 1, .PrecioUnitario = 100D, .PorcentajeIva = 0.21D, .estado = -1})
        A.CallTo(Function() servicioPedidos.cargarPedido("1", 927295)).Returns(Task.FromResult(pedidoDeA))
        Dim resumenDeA As New ResumenPedido With {.empresa = "1", .numero = 927295, .cliente = "30676"}

        vm.clienteActivo = UnCliente("30676", "a@example.com", "600111222")
        vm.PedidoSeleccionadoCobro = resumenDeA
        Await vm.CargarPedidoParaCobroAsync(resumenDeA)
        Assert.AreEqual("a@example.com", vm.CorreoReclamarDeuda, "Sin pasar por Deudas, ya son los del cliente en pantalla")
        Assert.IsTrue(vm.CanEnviarEnlacePagoPedido())

        vm.clienteActivo = UnCliente("15191", "b@example.com", "916000000")

        Assert.AreEqual("b@example.com", vm.CorreoReclamarDeuda)
        Assert.AreNotEqual("600111222", vm.MovilReclamarDeuda)
        Assert.IsNull(vm.PedidoSeleccionadoCobro, "El pedido del anterior se suelta")
        Assert.IsFalse(vm.CanEnviarEnlacePagoPedido())

        ' Y aunque se vuelva a elegir el pedido de A con la ficha de B, no se puede mandar al correo de B
        vm.PedidoSeleccionadoCobro = resumenDeA
        Await vm.CargarPedidoParaCobroAsync(resumenDeA)
        Assert.IsFalse(vm.CanEnviarEnlacePagoPedido())
    End Function

    ' Hallazgo 2: una carga que contesta tarde (cliente A) pisaba la ficha del cliente elegido después (B).
    <TestMethod()>
    Public Async Function H2_UnaCargaQueLlegaTarde_NoPisaLaFichaDelClienteElegidoDespues() As Task
        Dim lecturaDeA As New TaskCompletionSource(Of ClienteJson)()
        A.CallTo(Function() _servicio.LeerCliente("1", "30676", "0")).Returns(lecturaDeA.Task)
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(UnCliente("15191", "b@example.com", Nothing)))
        Dim vm = Comercial()

        Dim cargaDeA As Task = vm.ActualizarClienteAsync("1", "30676", "0")
        Await vm.ActualizarClienteAsync("1", "15191", "0")
        lecturaDeA.SetResult(UnCliente("30676", "a@example.com", "600111222"))
        Await cargaDeA

        Assert.AreEqual("15191", vm.clienteActivo.cliente)
        Assert.AreEqual("Cliente 15191", vm.nombre)
        Assert.AreEqual("Cliente 15191", vm.Titulo)
        Assert.AreEqual("b@example.com", vm.CorreoReclamarDeuda)
    End Function

    ' Hallazgo 3: si el cliente no se podía leer, se quedaba entera la ficha del anterior.
    <TestMethod()>
    Public Async Function H3_SiElClienteNoSePuedeLeer_SeVaciaLaFichaYSeDice() As Task
        A.CallTo(Function() _servicio.LeerCliente("1", "30676", "0")).Returns(Task.FromResult(UnCliente("30676", "a@example.com", "600111222")))
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(Of ClienteJson)(Nothing))
        A.CallTo(Function() _servicio.LeerCliente("1", "99999", "0")).Throws(New Exception("Error 500"))
        Dim vm = Comercial()
        Await vm.ActualizarClienteAsync("1", "30676", "0")
        vm.cuentaActiva = New CCCModel With {.Número = "1"}

        Await vm.ActualizarClienteAsync("1", "15191", "0")

        Assert.IsNull(vm.clienteActivo)
        Assert.IsNull(vm.nombre)
        Assert.IsNull(vm.cuentaActiva)
        Assert.AreEqual(0, vm.cuentasBanco.Count)
        Assert.IsTrue(String.IsNullOrEmpty(vm.CorreoReclamarDeuda))
        Assert.AreEqual("No se ha podido leer el cliente 15191 (contacto 0). Vuelva a elegirlo.", vm.mensajeError)
        Assert.AreEqual("Cliente 15191: no se ha podido leer", vm.Titulo)

        Await vm.ActualizarClienteAsync("1", "30676", "0")
        Assert.AreEqual("30676", vm.clienteActivo.cliente)
        Assert.IsTrue(String.IsNullOrEmpty(vm.mensajeError), "Al leer otro bien, el aviso se va")

        Await vm.ActualizarClienteAsync("1", "99999", "0")
        Assert.IsNull(vm.clienteActivo, "Si la lectura falla, tampoco se queda la del anterior")
        StringAssert.Contains(vm.mensajeError, "Error 500")
    End Function

    ' Hallazgo 4: el enlace, el asunto, el nombre, el motivo y el historial de la reclamación no se reiniciaban al cambiar de cliente.
    <TestMethod()>
    Public Sub H4_AlCambiarDeCliente_SeReiniciaLaReclamacionDeDeuda_YConElMismoClienteNo()
        Dim vm = Comercial()
        vm.clienteActivo = UnCliente("30676", "a@example.com", "600111222")
        vm.EnlaceReclamarDeuda = "https://pago.example/a"
        vm.AsuntoReclamarDeuda = "Pago del curso de A"
        vm.NombreReclamarDeuda = "Ana"
        vm.MotivoCambioEstado = "Lo ha pagado por transferencia"
        vm.HistorialPagos = New ObservableCollection(Of PagoTPVDTO) From {New PagoTPVDTO()}
        vm.ImporteReclamarDeuda = 25D
        vm.ListaDeudas = New ObservableCollection(Of ExtractoClienteDTO) From {New ExtractoClienteDTO With {.Cliente = "30676"}}

        vm.clienteActivo = UnCliente("15191", "b@example.com", Nothing)

        Assert.IsTrue(String.IsNullOrEmpty(vm.EnlaceReclamarDeuda))
        Assert.AreEqual(String.Empty, vm.AsuntoReclamarDeuda)
        Assert.IsTrue(String.IsNullOrEmpty(vm.NombreReclamarDeuda))
        Assert.IsTrue(String.IsNullOrEmpty(vm.MotivoCambioEstado))
        Assert.IsNull(vm.HistorialPagos)
        Assert.AreEqual(0D, vm.ImporteReclamarDeuda)
        Assert.IsNull(vm.ListaDeudas, "Los efectos del anterior no se quedan para reclamarlos con el nuevo")

        ' Volver a leer el MISMO cliente (al guardar, al recargar) no borra lo que el usuario está escribiendo
        vm.AsuntoReclamarDeuda = "Pago del curso de B"
        vm.clienteActivo = UnCliente("15191", "b@example.com", Nothing)
        Assert.AreEqual("Pago del curso de B", vm.AsuntoReclamarDeuda)
    End Sub
End Class
