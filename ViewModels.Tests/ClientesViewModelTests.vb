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
        ' Antes se usaba la nav property CCC2 de la entidad EF; ahora el campo ccc del DTO.
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(CrearFicha()))
        Dim vm = CrearViewModel()
        vm.cuentasBanco = New ObservableCollection(Of CCC) From {
            New CCC With {.Empresa = "1", .Cliente = "15191", .Número = "1  ", .Estado = 0},
            New CCC With {.Empresa = "1", .Cliente = "15191", .Número = "3  ", .Estado = 0}
        }

        Await vm.ActualizarClienteAsync("1", "15191", "0")

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
End Class
