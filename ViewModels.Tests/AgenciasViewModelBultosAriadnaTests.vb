Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports Prism.Regions
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' Nesto#507: al elegir en Agencias un pedido preparado con Ariadna, el envío nace con los bultos que el mozo
''' hizo en el packing (antes nacía con 1 y se tecleaba a mano), y se pueden ver sus fotos.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelBultosAriadnaTests
    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private bultosAriadna As IServicioBultosAriadna
    Private viewModel As AgenciasViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IServicioDialogos)
        bultosAriadna = A.Fake(Of IServicioBultosAriadna)
        A.CallTo(Function() bultosAriadna.LeerBultosDelPedido(A(Of String).Ignored, A(Of Integer).Ignored)).
            Returns(Task.FromResult(New List(Of BultoAriadna)))

        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, "UltNumPedidoVta")).Returns("12345")
        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(New ObservableCollection(Of Empresas) From {empresa})
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).
            ReturnsLazily(Function() New ObservableCollection(Of AgenciasTransporte) From {New AgenciasTransporte With {.Empresa = "1  ", .Numero = 1, .Nombre = "ASM"}, New AgenciasTransporte With {.Empresa = "1  ", .Numero = 13, .Nombre = "CTT"}})

        A.CallTo(Function() servicio.CargarAgencia(A(Of Integer).Ignored)).Returns(New AgenciasTransporte With {.Empresa = "1  ", .Numero = 1, .Nombre = "ASM"})

        For Each numero In {12345, 927646}
            Dim pedido = New PedidoAgenciaModel With {
                .Empresa = "1  ",
                .Número = numero,
                .Nº_Cliente = "29606",
                .Contacto = "0",
                .Clientes = New ClienteAgenciaModel With {.Nombre = "CLIENTE", .Dirección = "CALLE", .Población = "MADRID", .Provincia = "MADRID", .CodPostal = "28001", .Teléfono = "911234567"}
            }
            A.CallTo(Function() servicio.LeerPedidoParaAgenciaPorNumero(numero, A(Of Boolean).Ignored)).Returns(pedido)
            A.CallTo(Function() servicio.LeerPedidoParaAgencia(A(Of String).Ignored, numero)).Returns(pedido)
        Next

        viewModel = New AgenciasViewModel(A.Fake(Of RegionManager), servicio, configuracion, dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        ' El comparador del servidor no contesta en estas pruebas (no importa qué agencia se elija).
        Dim comparador = A.Fake(Of IServicioComparadorAgencias)
        A.CallTo(Function() comparador.MasEconomica(A(Of String).Ignored, A(Of String).Ignored, A(Of Decimal).Ignored, A(Of Decimal).Ignored, A(Of String).Ignored)) _
            .Returns(New TaskCompletionSource(Of OpcionEnvioAgencia)().Task)
        viewModel.ComparadorAgencias = comparador
        viewModel.ServicioBultosAriadna = bultosAriadna
        viewModel.cmdCargarDatos.Execute(Nothing)
    End Sub

    Private Sub ElPedidoTieneBultosEnAriadna(ParamArray bultos As BultoAriadna())
        A.CallTo(Function() bultosAriadna.LeerBultosDelPedido(A(Of String).Ignored, 927646)).
            Returns(Task.FromResult(bultos.ToList()))
    End Sub

    <TestMethod()>
    Public Sub ElegirPedido_ConBultosDeAriadna_ProponeEseNumero()
        ElPedidoTieneBultosEnAriadna(New BultoAriadna With {.Id = 17, .Bulto = 1, .TieneFoto = True},
                                     New BultoAriadna With {.Id = 18, .Bulto = 2, .TieneFoto = True})

        viewModel.numeroPedido = "927646"

        Assert.AreEqual(2, viewModel.bultos)
        Assert.AreEqual("Ariadna: 2 bultos (2 con foto)", viewModel.TextoBultosAriadna)
        Assert.AreEqual(System.Windows.Visibility.Visible, viewModel.VisibilidadBultosAriadna)
        A.CallTo(Function() bultosAriadna.LeerBultosDelPedido("1  ", 927646)).MustHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub ElegirPedido_SinBultosDeAriadna_TodoComoHoy()
        viewModel.numeroPedido = "927646"

        Assert.AreEqual(1, viewModel.bultos)
        Assert.AreEqual(System.Windows.Visibility.Collapsed, viewModel.VisibilidadBultosAriadna)
    End Sub

    <TestMethod()>
    Public Sub ElegirPedido_FallaLaApi_NoMolestaYSigueConUnBulto()
        A.CallTo(Function() bultosAriadna.LeerBultosDelPedido(A(Of String).Ignored, 927646)).
            ThrowsAsync(New Net.Http.HttpRequestException("sin red"))

        viewModel.numeroPedido = "927646"

        Assert.AreEqual(1, viewModel.bultos)
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("sin red"))).MustNotHaveHappened()
    End Sub
End Class
