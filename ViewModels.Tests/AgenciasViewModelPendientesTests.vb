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
Imports Prism.Services.Dialogs
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' Pestaña Pendientes de Agencias (28/09/26, envío 249165 del cliente 29606): al pinchar un
''' pendiente de GLS con la pantalla en CTT, el retorno salía «NO» aunque en la base de datos era 1,
''' y al guardar volvía a salir «NO». Además había envíos de GLS guardados con el servicio 48 y el
''' horario 0 de CTT.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelPendientesTests
    Private Const ASM As Integer = 1
    Private Const CTT As Integer = 13

    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IDialogService
    Private comparador As IServicioComparadorAgencias
    Private respuestaComparador As TaskCompletionSource(Of OpcionEnvioAgencia)
    Private viewModel As AgenciasViewModel

    Private pendienteCTT As EnvioAgenciaWrapper
    Private pendienteGLS As EnvioAgenciaWrapper

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IDialogService)
        comparador = A.Fake(Of IServicioComparadorAgencias)
        ' El comparador del servidor contesta TARDE, como en la vida real: la respuesta llega
        ' cuando el setter que lo lanzó ya ha terminado.
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        A.CallTo(Function() comparador.MasEconomica(A(Of String).Ignored, A(Of String).Ignored, A(Of Decimal).Ignored, A(Of Decimal).Ignored, A(Of String).Ignored)) _
            .ReturnsLazily(Function() respuestaComparador.Task)

        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, "UltNumPedidoVta")).Returns("12345")

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(New ObservableCollection(Of Empresas) From {empresa})
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).
            ReturnsLazily(Function() New ObservableCollection(Of AgenciasTransporte) From {Agencia(ASM, "ASM"), Agencia(CTT, "CTT")})

        ' El pedido que se queda abierto en la pestaña Pedidos (la ventana arranca con el último).
        Dim pedido = New PedidoAgenciaModel With {
            .Empresa = "1  ",
            .Número = 12345,
            .Nº_Cliente = "29606",
            .Contacto = "0",
            .Clientes = New ClienteAgenciaModel With {.Nombre = "CLIENTE", .Dirección = "CALLE", .Población = "MADRID", .Provincia = "MADRID", .CodPostal = "28001", .Teléfono = "911234567"}
        }
        A.CallTo(Function() servicio.LeerPedidoParaAgenciaPorNumero(12345, A(Of Boolean).Ignored)).Returns(pedido)
        A.CallTo(Function() servicio.LeerPedidoParaAgencia(A(Of String).Ignored, A(Of Nullable(Of Integer)).Ignored)).Returns(pedido)

        ' Los datos reales de la base de datos: Empresa char(3) con relleno.
        pendienteCTT = New EnvioAgenciaWrapper With {.Numero = 249100, .Empresa = "1  ", .Agencia = CTT, .Estado = -1, .Servicio = 48, .Horario = 0, .Retorno = 0, .Pais = 34}
        pendienteGLS = New EnvioAgenciaWrapper With {.Numero = 249165, .Empresa = "1  ", .Agencia = ASM, .Estado = -1, .Servicio = 96, .Horario = 18, .Retorno = 1, .Pais = 34}
        pendienteCTT.TieneCambios = False
        pendienteGLS.TieneCambios = False
        A.CallTo(Function() servicio.CargarListaPendientes()).
            ReturnsLazily(Function() New List(Of EnvioAgenciaWrapper) From {pendienteCTT, pendienteGLS})
    End Sub

    Private Shared Function Agencia(numero As Integer, nombre As String) As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = numero, .Nombre = nombre}
    End Function

    ''' <summary>La ventana abierta en Pendientes con el pendiente de CTT seleccionado (la pantalla en CTT).</summary>
    Private Sub AbrirPendientesConCTTSeleccionada()
        viewModel = New AgenciasViewModel(A.Fake(Of RegionManager), servicio, configuracion, dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.ComparadorAgencias = comparador
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute(Nothing)
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = pendienteCTT.Numero)
        ContestaElComparador(CTT)
        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero, "Precondición: la pantalla está en CTT")
        For Each p In viewModel.listaPendientes
            p.TieneCambios = False
        Next
    End Sub

    Private Sub ContestaElComparador(agenciaId As Integer)
        Dim tcs = respuestaComparador
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        tcs.TrySetResult(New OpcionEnvioAgencia With {.AgenciaId = agenciaId, .Coste = 4.5D})
    End Sub

    <TestMethod()>
    Public Sub Pendientes_AlPincharUnPendienteDeGLS_ConLaPantallaEnCTT_NoSePisanSusDatos()
        ' El 249165: la pantalla cambia a GLS, pero el comparador del SERVIDOR (lanzado por el pedido
        ' que se quedó abierto en la pestaña Pedidos) contesta después «CTT» y la devolvía a CTT,
        ' ya sin la protección de _estaCambiandoDePedido: el pendiente se quedaba con los defectos de
        ' CTT (retorno 0 «NO», servicio 48, horario 0) y marcado como cambiado.
        AbrirPendientesConCTTSeleccionada()

        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)
        ContestaElComparador(CTT)

        Assert.AreEqual(ASM, viewModel.agenciaSeleccionada.Numero, "La pantalla se queda en la agencia del pendiente")
        Assert.AreEqual(CByte(1), viewModel.EnvioPendienteSeleccionado.Retorno, "El retorno guardado")
        Assert.AreEqual(CByte(96), viewModel.EnvioPendienteSeleccionado.Servicio)
        Assert.AreEqual(CByte(18), viewModel.EnvioPendienteSeleccionado.Horario)
        Assert.AreEqual(ASM, viewModel.EnvioPendienteSeleccionado.Agencia)
        Assert.IsFalse(viewModel.EnvioPendienteSeleccionado.TieneCambios, "Pinchar un pendiente no es cambiarlo")
        Assert.AreEqual("Con Retorno", viewModel.listaTiposRetorno.Single(Function(r) r.id = 1).descripcion, "La lista de retornos es la de GLS")
    End Sub

    <TestMethod()>
    Public Sub Pendientes_LaRespuestaTardiaDelComparadorDelPedido_NoCambiaLaAgenciaDelPendiente()
        ' La respuesta del comparador que se pidió en la pestaña Pedidos llega cuando ya se está en
        ' Pendientes con un pendiente de GLS: la agencia es la del pendiente, no la del pedido.
        viewModel = New AgenciasViewModel(A.Fake(Of RegionManager), servicio, configuracion, dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.ComparadorAgencias = comparador
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute(Nothing)
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)
        viewModel.EnvioPendienteSeleccionado.TieneCambios = False

        ContestaElComparador(CTT)

        Assert.AreEqual(ASM, viewModel.agenciaSeleccionada.Numero)
        Assert.AreEqual(CByte(1), viewModel.EnvioPendienteSeleccionado.Retorno)
        Assert.AreEqual(CByte(96), viewModel.EnvioPendienteSeleccionado.Servicio)
        Assert.IsFalse(viewModel.EnvioPendienteSeleccionado.TieneCambios)
    End Sub

    <TestMethod()>
    Public Sub Pendientes_DespuesDeGuardar_NoSeVuelvenAPisarLosDatos()
        ' «Pongo el retorno, doy a Guardar y no se guarda»: sí se guardaba, pero al recargar el
        ' pendiente tras guardar se repetía lo de arriba y volvía a salir «NO».
        AbrirPendientesConCTTSeleccionada()
        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)
        ContestaElComparador(CTT)
        Dim guardado As EnviosAgencia = Nothing
        A.CallTo(Sub() servicio.Modificar(A(Of EnviosAgencia).Ignored)).Invokes(Sub(e As EnviosAgencia) guardado = e)
        viewModel.EnvioPendienteSeleccionado.Retorno = 2

        viewModel.GuardarEnvioPendienteCommand.Execute(Nothing)
        ContestaElComparador(CTT)

        Assert.IsNotNull(guardado)
        Assert.AreEqual(CByte(2), guardado.Retorno)
        Assert.AreEqual(ASM, guardado.Agencia)
        Assert.AreEqual(CByte(96), guardado.Servicio)
        Assert.AreEqual(CByte(2), viewModel.EnvioPendienteSeleccionado.Retorno)
        Assert.IsFalse(viewModel.EnvioPendienteSeleccionado.TieneCambios)
    End Sub

    <TestMethod()>
    Public Sub Pendientes_DespuesDeGuardar_UnNuevoCambioSeVuelveAPoderGuardar()
        ' Tras guardar, el pendiente se sustituye por uno nuevo que no avisaba de sus cambios: si se
        ' tocaba otra vez sin cambiar de fila, el botón Guardar se quedaba apagado.
        AbrirPendientesConCTTSeleccionada()
        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)
        viewModel.EnvioPendienteSeleccionado.Retorno = 2
        viewModel.GuardarEnvioPendienteCommand.Execute(Nothing)
        Assert.IsFalse(viewModel.GuardarEnvioPendienteCommand.CanExecute(Nothing), "Recién guardado")

        viewModel.EnvioPendienteSeleccionado.Retorno = 1

        Assert.IsTrue(viewModel.EnvioPendienteSeleccionado.TieneCambios)
        Assert.IsTrue(viewModel.GuardarEnvioPendienteCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub Pendientes_UnPendienteConLaEmpresaSinRelleno_SeSeleccionaIgual()
        ' Empresa es char(3): "1" y "1  " son la misma. Con el "=" pelado el Single lanzaba dentro del
        ' setter y dejaba _estaCambiandoDePedido a True para siempre.
        AbrirPendientesConCTTSeleccionada()
        pendienteGLS = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)
        pendienteGLS.Empresa = "1"
        pendienteGLS.TieneCambios = False

        viewModel.EnvioPendienteSeleccionado = pendienteGLS

        Assert.AreEqual(ASM, viewModel.agenciaSeleccionada.Numero)
        Assert.AreEqual(CByte(1), viewModel.EnvioPendienteSeleccionado.Retorno)
    End Sub

    <TestMethod()>
    Public Sub Pendientes_AlCambiarElUsuarioLaAgencia_SeAdaptanServicioYHorario_YSeConservaElRetorno()
        ' Por no adaptarse hay envíos de GLS con el servicio 48 y el horario 0 de CTT.
        AbrirPendientesConCTTSeleccionada()
        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)
        ContestaElComparador(CTT)

        viewModel.agenciaSeleccionada = viewModel.listaAgencias.Single(Function(a) a.Numero = CTT)

        Dim envio = viewModel.EnvioPendienteSeleccionado
        Assert.AreEqual(CTT, envio.Agencia, "El pendiente pasa a la agencia elegida")
        Assert.AreEqual(New AgenciaCTT().ServicioDefecto, envio.Servicio, "El 96 de GLS no existe en CTT: el defecto")
        Assert.AreEqual(New AgenciaCTT().HorarioDefecto, envio.Horario, "El 18 de GLS no existe en CTT: el defecto")
        Assert.AreEqual(CByte(1), envio.Retorno, "«Con retorno» existe en las dos: se conserva")
        Assert.IsTrue(envio.TieneCambios)
        Assert.IsTrue(viewModel.GuardarEnvioPendienteCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub Pendientes_AlCambiarElUsuarioLaAgencia_LoQueExisteEnLaNuevaSeConserva()
        AbrirPendientesConCTTSeleccionada()
        Dim envio = viewModel.EnvioPendienteSeleccionado
        envio.Servicio = 24 ' CTT 24 h, forzado a mano
        envio.Retorno = 2

        viewModel.agenciaSeleccionada = viewModel.listaAgencias.Single(Function(a) a.Numero = ASM)

        Assert.AreEqual(ASM, envio.Agencia)
        Assert.AreEqual(New AgenciaASM().ServicioDefecto, envio.Servicio, "El 24 de CTT no existe en GLS")
        Assert.AreEqual(New AgenciaASM().HorarioDefecto, envio.Horario, "El 0 de CTT no existe en GLS")
        Assert.AreEqual(CByte(2), envio.Retorno, "El 2 existe en GLS: se conserva")
        Assert.AreEqual(34, envio.Pais)
    End Sub

    <TestMethod()>
    Public Sub Pendientes_AlPincharUnPendienteConUnServicioQueNoEsDeSuAgencia_SeCorrigeYQuedaSinGuardar()
        ' El 249165 tal y como está en la base de datos: agencia 1 (GLS) con servicio 48 y horario 0
        ' (los de CTT). El desplegable saldría en blanco: se pone el defecto de GLS y se deja marcado
        ' para guardar, con aviso. El retorno (que sí es válido) no se toca.
        pendienteGLS.Servicio = 48
        pendienteGLS.Horario = 0
        pendienteGLS.TieneCambios = False
        AbrirPendientesConCTTSeleccionada()

        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)
        ContestaElComparador(CTT)

        Dim envio = viewModel.EnvioPendienteSeleccionado
        Assert.AreEqual(ASM, viewModel.agenciaSeleccionada.Numero)
        Assert.AreEqual(ASM, envio.Agencia)
        Assert.AreEqual(CByte(96), envio.Servicio)
        Assert.AreEqual(CByte(18), envio.Horario)
        Assert.AreEqual(CByte(1), envio.Retorno)
        Assert.IsTrue(envio.TieneCambios, "Queda pendiente de guardar la corrección")
        StringAssert.Contains(viewModel.mensajeError, "249165")
    End Sub

    <TestMethod()>
    Public Sub Pendientes_AlPincharUnPendienteConElServicioSinElegir_NoSeTocaNiSeAvisa()
        ' La tienda online y NestoApp dejan los de CTT e Innovatrans con servicio 0 («sin elegir»):
        ' al imprimir se resuelve al de la pantalla. No es un error y no se toca.
        pendienteCTT.Servicio = 0
        pendienteCTT.TieneCambios = False
        AbrirPendientesConCTTSeleccionada()
        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)

        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = pendienteCTT.Numero)

        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero)
        Assert.AreEqual(CByte(0), viewModel.EnvioPendienteSeleccionado.Servicio)
        Assert.IsFalse(viewModel.EnvioPendienteSeleccionado.TieneCambios)
        Assert.IsTrue(String.IsNullOrEmpty(viewModel.mensajeError), viewModel.mensajeError)
    End Sub

    <TestMethod()>
    Public Sub Pendientes_AlPincharUnPendienteCorrecto_NoHayAviso()
        AbrirPendientesConCTTSeleccionada()

        viewModel.EnvioPendienteSeleccionado = viewModel.listaPendientes.Single(Function(p) p.Numero = 249165)

        Assert.IsTrue(String.IsNullOrEmpty(viewModel.mensajeError), viewModel.mensajeError)
    End Sub

End Class
