Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs

' Nesto#340 (1C.8, slice 4): primeros tests reales del VM. Los antiguos (2014) eran de
' integración contra la BD de producción y estaban comentados; el constructor de tests nuevo
' permite inyectar IClienteComercialService y no toca EF ni el contenedor de Prism.
<TestClass()>
Public Class ClientesViewModelTests

    Private _configuracion As IConfiguracion
    Private _dialogService As IDialogService
    Private _servicio As IClienteComercialService
    Private _servicioRapports As IRapportService
    Private _servicioAutenticacion As IServicioAutenticacion

    <TestInitialize()>
    Public Sub Initialize()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogService = A.Fake(Of IDialogService)()
        _servicio = A.Fake(Of IClienteComercialService)()
        _servicioRapports = A.Fake(Of IRapportService)()
        _servicioAutenticacion = A.Fake(Of IServicioAutenticacion)()
    End Sub

    Private Function CrearViewModel() As ClientesViewModel
        Return New ClientesViewModel(_configuracion, _dialogService, _servicio, _servicioRapports, _servicioAutenticacion)
    End Function

    Private Function CrearFicha() As ClienteJson
        Return New ClienteJson With {
            .empresa = "1",
            .cliente = "15191",
            .contacto = "0",
            .nombre = "CENTRO DE ESTÉTICA EL EDÉN, S.L.U.",
            .estado = 0,
            .ccc = "3",
            .telefono = "915555555",
            .PersonasContacto = New ObservableCollection(Of PersonaContactoJson) From {
                New PersonaContactoJson With {.Numero = 1, .Nombre = "Eva", .Cargo = 26, .CargoDescripcion = "Agencia", .CorreoElectronico = "eva@eleden.com"}
            }
        }
    End Function

    <TestMethod()>
    Public Async Function ActualizarCliente_CargaLaFichaDesdeElServicio() As Task
        Dim ficha = CrearFicha()
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(ficha))
        Dim vm = CrearViewModel()

        Await vm.ActualizarClienteAsync("1", "15191", "0")

        Assert.AreSame(ficha, vm.clienteActivo)
        Assert.AreEqual("CENTRO DE ESTÉTICA EL EDÉN, S.L.U.", vm.nombre)
        Assert.AreEqual("Cliente 15191", vm.Titulo)
        Assert.AreEqual("Agencia", vm.clienteActivo.PersonasContacto.First().CargoDescripcion)
    End Function

    <TestMethod()>
    Public Async Function ActualizarCliente_LaCuentaActivaSeLocalizaPorElCccEscalar() As Task
        ' Antes se usaba la nav property CCC2 de la entidad EF; ahora el campo ccc del DTO y
        ' (slice 5) las cuentas vienen del servicio como POCOs CCCModel.
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(CrearFicha()))
        A.CallTo(Function() _servicio.LeerCCCs("1", "15191", "0")).Returns(Task.FromResult(New List(Of CCCModel) From {
            New CCCModel With {.Empresa = "1", .Cliente = "15191", .Número = "1  ", .Estado = 0},
            New CCCModel With {.Empresa = "1", .Cliente = "15191", .Número = "3  ", .Estado = 0}
        }))
        Dim vm = CrearViewModel()

        Await vm.ActualizarClienteAsync("1", "15191", "0")

        Assert.AreEqual(2, vm.cuentasBanco.Count)
        Assert.IsNotNull(vm.cuentaActiva)
        Assert.AreEqual("3  ", vm.cuentaActiva.Número)
    End Function

    <TestMethod()>
    Public Async Function ActualizarCliente_SiElServicioNoDevuelveFichaNoTocaElClienteActivo() As Task
        A.CallTo(Function() _servicio.LeerCliente(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(Of ClienteJson)(Nothing))
        Dim vm = CrearViewModel()

        Await vm.ActualizarClienteAsync("1", "99999", "0")

        Assert.IsNull(vm.clienteActivo)
        Assert.IsNull(vm.nombre)
    End Function

    <TestMethod()>
    Public Async Function ActualizarCliente_ConParametrosNothingNoLlamaAlServicio() As Task
        Dim vm = CrearViewModel()

        Await vm.ActualizarClienteAsync("1", Nothing, "0")

        A.CallTo(Function() _servicio.LeerCliente(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ActualizarCliente_SiElServicioFallaInformaElError() As Task
        A.CallTo(Function() _servicio.LeerCliente(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .Throws(New Exception("Se ha caído el servidor"))
        Dim vm = CrearViewModel()

        Await vm.ActualizarClienteAsync("1", "15191", "0")

        Assert.AreEqual("Se ha caído el servidor", vm.mensajeError)
        Assert.IsNull(vm.clienteActivo)
    End Function

    ' Nesto#340 (1C.8, slice 5): CRUD de CCC sin EF (POCOs con dirty flag + PUT Clientes/CCCs).

    <TestMethod()>
    Public Sub NuevoMandato_CreaUnCCCNuevoMarcadoComoModificado()
        Dim vm = CrearViewModel()
        vm.cuentasBanco = New ObservableCollection(Of CCCModel)

        vm.cmdNuevoMandato.Execute(Nothing)

        Assert.AreEqual(1, vm.cuentasBanco.Count)
        Assert.IsNotNull(vm.cuentaActiva)
        Assert.IsTrue(vm.cuentaActiva.EsModificado)
        Assert.AreEqual("1", vm.cuentaActiva.Número)
        Assert.AreEqual(CShort(0), vm.cuentaActiva.Estado)
        Assert.AreEqual("FRST", vm.cuentaActiva.Secuencia)
        Assert.AreEqual(String.Empty, vm.mensajeError)
    End Sub

    <TestMethod()>
    Public Sub EditarUnCampoDelCCC_MarcaElDirtyQueAntesLlevabaElChangeTracker()
        Dim ccc As New CCCModel With {.Número = "1"}
        ccc.EsModificado = False

        ccc.Entidad = "2100"

        Assert.IsTrue(ccc.EsModificado)
    End Sub

    <TestMethod()>
    Public Sub Guardar_EnviaSoloLosModificadosYLimpiaElFlag()
        Dim peticionEnviada As GuardarCCCsRequest = Nothing
        A.CallTo(Function() _servicio.GuardarCCCs(A(Of GuardarCCCsRequest).Ignored)) _
            .Invokes(Sub(p As GuardarCCCsRequest) peticionEnviada = p) _
            .Returns(Task.FromResult(New GuardarCCCsRespuesta With {
                .extractoOtroCCC = New List(Of ExtractoCCCModel) From {
                    New ExtractoCCCModel With {.Concepto = "Efecto viejo", .ImportePdte = 100, .CCC = "2"}
                },
                .pedidosOtroCCC = New List(Of cabeceraPedidoAgrupada)
            }))
        Dim vm = CrearViewModel()
        Dim sinCambios As New CCCModel With {.Número = "1"}
        sinCambios.EsModificado = False
        Dim modificado As New CCCModel With {.Número = "2"}
        modificado.Entidad = "2100" ' marca EsModificado
        vm.cuentasBanco = New ObservableCollection(Of CCCModel) From {sinCambios, modificado}
        vm.cuentaActiva = modificado

        vm.cmdGuardar.Execute(Nothing)

        Assert.IsNotNull(peticionEnviada)
        Assert.AreEqual(1, peticionEnviada.cccs.Count)
        Assert.AreEqual("2", peticionEnviada.cccs.Single().Número)
        Assert.AreEqual("2", peticionEnviada.cccActivo)
        Assert.IsFalse(modificado.EsModificado)
        Assert.AreEqual(1, vm.extractoCCC.Count)
        Assert.AreEqual("Efecto viejo", vm.extractoCCC.Single().Concepto)
        Assert.AreEqual(String.Empty, vm.mensajeError)
    End Sub

    <TestMethod()>
    Public Sub Guardar_SiElServicioFallaInformaElErrorYNoLimpiaElFlag()
        A.CallTo(Function() _servicio.GuardarCCCs(A(Of GuardarCCCsRequest).Ignored)) _
            .Throws(New Exception("No se pudo grabar el CCC"))
        Dim vm = CrearViewModel()
        Dim modificado As New CCCModel With {.Número = "1"}
        modificado.Entidad = "2100"
        vm.cuentasBanco = New ObservableCollection(Of CCCModel) From {modificado}
        vm.cuentaActiva = modificado

        vm.cmdGuardar.Execute(Nothing)

        Assert.AreEqual("No se pudo grabar el CCC", vm.mensajeError)
        Assert.IsTrue(modificado.EsModificado)
    End Sub

    ' Nesto#423: el CommandParameter de CargarPedidoCommand es el SelectedItem del DataGrid de
    ' ventas y puede llegar Nothing (doble clic en cabecera o hueco del grid) o ser el
    ' NewItemPlaceholder (que no es un ResumenPedido). El código antiguo hacía param.numero por
    ' late binding y reventaba con "Object variable or With block variable not set" (NRE de
    ' Lidia, ELMAH 21/07/26). Ahora se ignora en silencio: no hay pedido que cargar.
    <TestMethod()>
    Public Sub CargarPedidoCommand_ConParametroNothing_NoLanza()
        Dim vm = CrearViewModel()

        vm.CargarPedidoCommand.Execute(Nothing)
    End Sub

    <TestMethod()>
    Public Sub CargarPedidoCommand_ConParametroQueNoEsResumenPedido_NoLanza()
        Dim vm = CrearViewModel()

        vm.CargarPedidoCommand.Execute("NewItemPlaceholder")
    End Sub
End Class
