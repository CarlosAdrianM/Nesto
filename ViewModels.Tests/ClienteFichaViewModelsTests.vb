Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels

' Nesto#646 (paso 2): ClientesViewModel servía a dos vistas y se ha partido en ClienteCuentasViewModel (Ventanas/Clientes.xaml)
' y ClienteComercialViewModel (Ventanas/ClienteComercial.xaml) sobre ClienteFichaViewModelBase, moviendo el código tal cual.
' Tests de caracterización de lo que podía romper el movimiento: que cada vista encuentra en su ViewModel todo lo que
' enlaza (un binding que no existe no da error, solo deja el control vacío), que la de cuentas se puede crear desde el
' XAML y que lo que cada ficha hacía dentro del código común (los ganchos) sigue pasando.
<TestClass()>
Public Class ClienteFichaViewModelsTests

    ' Lo que enlaza Ventanas/Clientes.xaml contra su DataContext (más Titulo, la cabecera de la pestaña).
    Private Shared ReadOnly BindingsFichaCuentas As String() = {
        "listaEmpresas", "empresaActual", "clienteActual", "listaContactos", "contactoActual", "cmdGuardar", "mensajeError",
        "nombre", "cuentasBanco", "cuentaActiva", "textoCuentaDelCliente", "cmdUsarCuentaParaRecibos", "cmdNuevoMandato",
        "listaEstadosCCC", "listaTipos", "listaSecuencias", "cmdVerMandato", "cmdAsignarMandato", "ImprimirMandatoCommand",
        "EnviarMandatoPorCorreoCommand", "estaVisibleExtractoCCC", "extractoCCC", "estaVisiblePedidosCCC", "pedidosCCC", "Titulo"
    }

    ' Lo que enlaza Ventanas/ClienteComercial.xaml (y su code-behind: EnlaceReclamarDeuda) contra su DataContext.
    Private Shared ReadOnly BindingsFichaComercial As String() = {
        "estaOcupado", "ListaClientesFiltrable", "clienteActivoDTO", "clienteActivo", "PuedeEditarComercial",
        "MotivoComercialBloqueado", "configuracion", "clienteServidor", "FiltroEquipoVendedores", "EsJefeDeVentas",
        "vendedorPorGrupo", "estadoPeluqueria", "descripcionEstadoCCC", "seguimientosOrdenados", "rangoFechasVenta",
        "listaVentas", "deudaVencida", "ExigeSuPedido", "PuedeCambiarSuPedidoObligatorio", "TooltipSuPedidoObligatorio",
        "IndiceSeleccionado", "FacturasSeleccionadas", "CorreoEnvioFacturas", "MostrarImagenesFacturas", "ListaPedidos",
        "PedidoSeleccionadoCobro", "TextoCobroPedido", "ListaDeudas", "CorreoReclamarDeuda", "MovilReclamarDeuda",
        "ImporteReclamarDeuda", "AsuntoReclamarDeuda", "NombreReclamarDeuda", "PuedeElegirMotorPagos",
        "MotorPagosSeleccionado", "EnlaceReclamarDeuda", "HistorialPagos", "PagoSeleccionado", "DeudaSeleccionada",
        "MotivoCambioEstado", "AbrirPedidoAction", "cmdGuardarVendedores", "CargarPedidoCommand", "GuardarEfectoDeudaCommand",
        "EnviarFacturasPorCorreoCommand", "EnviarEnlacePagoPedidoCommand", "DescargarFacturasCommand",
        "CopiarDatosTransferenciaPedidoCommand", "ConfirmarReclamarDeudaCommand", "AbrirEnlaceReclamacionCommand", "Titulo",
        "DescargarFacturaeCommand", "PuedeDescargarFacturae"
    }

    Private _servicio As IClienteComercialService

    <TestInitialize()>
    Public Sub Initialize()
        _servicio = A.Fake(Of IClienteComercialService)()
    End Sub

    Private Function Cuentas() As ClienteCuentasViewModel
        Return New ClienteCuentasViewModel(A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), _servicio,
                                           A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)())
    End Function

    Private Function Comercial() As ClienteComercialViewModel
        Return New ClienteComercialViewModel(A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), _servicio,
                                             A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)())
    End Function

    Private Shared Function QueFaltan(tipo As Type, nombres As IEnumerable(Of String)) As List(Of String)
        Return nombres.Where(Function(n) tipo.GetProperty(n, Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance) Is Nothing).ToList()
    End Function

    <TestMethod()>
    Public Sub FichaCuentas_TieneTodoLoQueEnlazaSuVista()
        Dim faltan = QueFaltan(GetType(ClienteCuentasViewModel), BindingsFichaCuentas)

        Assert.AreEqual(0, faltan.Count, "Faltan en ClienteCuentasViewModel: " & String.Join(", ", faltan))
    End Sub

    <TestMethod()>
    Public Sub FichaComercial_TieneTodoLoQueEnlazaSuVista()
        Dim faltan = QueFaltan(GetType(ClienteComercialViewModel), BindingsFichaComercial)

        Assert.AreEqual(0, faltan.Count, "Faltan en ClienteComercialViewModel: " & String.Join(", ", faltan))
    End Sub

    <TestMethod()>
    Public Sub FichaCuentas_SeCreaDesdeElXaml_ConElConstructorSinParametros()
        ' Clientes.xaml hace <vm:ClienteCuentasViewModel/>: sin constructor público sin parámetros la ficha no abre.
        Assert.IsNotNull(GetType(ClienteCuentasViewModel).GetConstructor(Type.EmptyTypes))
    End Sub

    <TestMethod()>
    Public Sub LasDosFichas_ComparteLaPiezaComun()
        Assert.IsTrue(GetType(ClienteFichaViewModelBase).IsAssignableFrom(GetType(ClienteCuentasViewModel)))
        Assert.IsTrue(GetType(ClienteFichaViewModelBase).IsAssignableFrom(GetType(ClienteComercialViewModel)))
    End Sub

    <TestMethod()>
    Public Sub FichaCuentas_SusComandosExisten()
        Dim vm = Cuentas()

        Assert.IsNotNull(vm.cmdGuardar)
        Assert.IsNotNull(vm.cmdUsarCuentaParaRecibos)
        Assert.IsNotNull(vm.cmdNuevoMandato)
        Assert.IsNotNull(vm.cmdVerMandato)
        Assert.IsNotNull(vm.cmdAsignarMandato)
        Assert.IsNotNull(vm.ImprimirMandatoCommand)
        Assert.IsNotNull(vm.EnviarMandatoPorCorreoCommand)
    End Sub

    <TestMethod()>
    Public Sub FichaComercial_SusComandosExisten()
        Dim vm = Comercial()

        Assert.IsNotNull(vm.ReclamarDeudaCommand)
        Assert.IsNotNull(vm.AbrirEnlaceReclamacionCommand)
        Assert.IsNotNull(vm.ConfirmarReclamarDeudaCommand)
        Assert.IsNotNull(vm.GuardarEfectoDeudaCommand)
        Assert.IsNotNull(vm.cmdGuardarVendedores)
        Assert.IsNotNull(vm.DescargarFacturasCommand)
        Assert.IsNotNull(vm.EnviarFacturasPorCorreoCommand)
        Assert.IsNotNull(vm.CargarPedidoCommand)
        Assert.IsNotNull(vm.EnviarEnlacePagoPedidoCommand)
        Assert.IsNotNull(vm.CopiarDatosTransferenciaPedidoCommand)
    End Sub

    ' Gancho AlCargarFicha: la ficha de cuentas vacía los avisos de otro CCC al cargar un cliente (antes, en ActualizarClienteAsync).
    <TestMethod()>
    Public Async Function FichaCuentas_AlCargarUnCliente_VaciaLosAvisosDeOtroCcc() As Task
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(New ClienteJson With {.empresa = "1", .cliente = "15191", .contacto = "0"}))
        Dim vm = Cuentas()
        vm.extractoCCC = New ObservableCollection(Of ExtractoCCCModel) From {New ExtractoCCCModel With {.Concepto = "Efecto viejo"}}
        vm.pedidosCCC = New ObservableCollection(Of cabeceraPedidoAgrupada) From {New cabeceraPedidoAgrupada With {.Numero = 1}}

        Await vm.ActualizarClienteAsync("1", "15191", "0")

        Assert.IsNull(vm.extractoCCC)
        Assert.IsNull(vm.pedidosCCC)
        Assert.AreEqual("Cliente 15191", vm.Titulo)
    End Function

    ' Gancho AlCambiarClienteActivo: la ficha de cuentas avisa de su texto de la cuenta del cliente.
    <TestMethod()>
    Public Sub FichaCuentas_AlCambiarElClienteActivo_AvisaDelTextoDeLaCuenta()
        Dim vm = Cuentas()
        Dim avisos As New List(Of String)
        AddHandler vm.PropertyChanged, Sub(s, e) avisos.Add(e.PropertyName)

        vm.clienteActivo = New ClienteJson With {.cliente = "15191", .ccc = "2"}

        CollectionAssert.Contains(avisos, NameOf(ClienteCuentasViewModel.textoCuentaDelCliente))
        CollectionAssert.Contains(avisos, NameOf(ClienteCuentasViewModel.clienteActivo))
        Assert.AreEqual("Cuenta del cliente: 2", vm.textoCuentaDelCliente)
    End Sub

    ' Gancho AlCambiarClienteActivo: la ficha comercial apunta el estado con que llega la ficha (Nesto#558).
    <TestMethod()>
    Public Sub FichaComercial_AlCambiarElClienteActivo_ApuntaElEstadoCargado()
        Dim vm = Comercial()

        vm.clienteActivo = New ClienteJson With {.cliente = "15191", .estado = 9}

        Assert.AreEqual(9, vm.EstadoCargado)
    End Sub

    ' Gancho AlCambiarClienteActivo: sin cliente (y en la pestaña General) la ficha comercial vacía facturas, pedidos y deudas.
    <TestMethod()>
    Public Sub FichaComercial_SinClienteActivo_VaciaFacturasPedidosYDeudas()
        Dim vm = Comercial()
        vm.ListaFacturas = New ObservableCollection(Of ExtractoClienteDTO)
        vm.ListaDeudas = New ObservableCollection(Of ExtractoClienteDTO)
        vm.ListaPedidos = New ObservableCollection(Of Nesto.Modulos.PedidoVenta.PedidoVentaModel.ResumenPedido)

        vm.clienteActivo = Nothing

        Assert.IsNull(vm.ListaFacturas)
        Assert.IsNull(vm.ListaDeudas)
        Assert.IsNull(vm.ListaPedidos)
        Assert.IsNull(vm.EstadoCargado)
    End Sub
End Class
