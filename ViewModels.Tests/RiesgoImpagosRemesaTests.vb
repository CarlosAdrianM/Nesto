Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.ViewModels

' NestoAPI#896 (Carlos, 27/11/26, «Enseñarlo ya a Dirección»): riesgo de devolución de los
' candidatos a remesa en la pestaña Cartera viva.
<TestClass()>
Public Class RiesgoImpagosRemesaTests

    Private Shared Function Candidato(id As Integer, Optional cliente As String = "1001", Optional documento As String = "NV26/1",
                                      Optional efecto As String = "1", Optional preseleccionado As Boolean = True) As EfectoCandidatoModel
        Return New EfectoCandidatoModel With {
            .Id = id, .Cliente = cliente, .Documento = documento, .Efecto = efecto,
            .Preseleccionado = preseleccionado, .ImportePendiente = 100D
        }
    End Function

    Private Shared Function Respuesta(ParamArray efectos As EfectoRiesgoImpagoModel()) As CandidatosRiesgoImpagosModel
        Return New CandidatosRiesgoImpagosModel With {
            .ConModelo = True,
            .Total = New TotalRiesgoImpagosModel With {.Recibos = efectos.Length, .DevolucionesEsperadas = 4.2, .ImporteEnRiesgo = 1830.5D},
            .Tramos = New List(Of TramoRiesgoImpagosModel) From {
                New TramoRiesgoImpagosModel With {.Tramo = "< 5 %", .Recibos = 10},
                New TramoRiesgoImpagosModel With {.Tramo = "5-15 %", .Recibos = 7},
                New TramoRiesgoImpagosModel With {.Tramo = "15-30 %", .Recibos = 5},
                New TramoRiesgoImpagosModel With {.Tramo = "≥ 30 %", .Recibos = 3}
            },
            .Efectos = efectos.ToList()
        }
    End Function

#Region "Casar"

    <TestMethod()>
    Public Sub Casar_PorNumeroDeOrden_PonePorcentajeColorYMotivos()
        Dim c1 = Candidato(500)
        Dim c2 = Candidato(501)
        Dim r = Respuesta(
            New EfectoRiesgoImpagoModel With {.NumOrden = 501, .Probabilidad = 0.237, .Tramo = "15-30 %",
                .Motivos = New List(Of String) From {"Le devolvieron 2 recibos el último año", " Paga por CORE "}},
            New EfectoRiesgoImpagoModel With {.NumOrden = 500, .Probabilidad = 0.012, .Tramo = "< 5 %"})

        RiesgoImpagosRemesa.Casar({c1, c2}, r)

        Assert.AreEqual("23,7 %", c2.RiesgoTexto)
        Assert.AreEqual(0.237, c2.ProbabilidadDevolucion.Value, 0.0000001)
        Assert.AreEqual(2, c2.NivelRiesgo)
        Assert.AreEqual("Le devolvieron 2 recibos el último año" & Environment.NewLine & "Paga por CORE", c2.MotivosRiesgo)
        Assert.AreEqual("1,2 %", c1.RiesgoTexto)
        Assert.AreEqual(0, c1.NivelRiesgo)
        Assert.IsNull(c1.MotivosRiesgo)
    End Sub

    <TestMethod()>
    Public Sub Casar_NoTocaLaSeleccionNiLaPreseleccion()
        Dim retenido = Candidato(600, preseleccionado:=False)
        retenido.Seleccionado = False
        Dim normal = Candidato(601)
        normal.Seleccionado = True
        Dim r = Respuesta(
            New EfectoRiesgoImpagoModel With {.NumOrden = 600, .Probabilidad = 0.9, .Tramo = "≥ 30 %"},
            New EfectoRiesgoImpagoModel With {.NumOrden = 601, .Probabilidad = 0.95, .Tramo = "≥ 30 %"})

        RiesgoImpagosRemesa.Casar({retenido, normal}, r)

        Assert.IsFalse(retenido.Seleccionado)
        Assert.IsFalse(retenido.Preseleccionado)
        Assert.IsTrue(normal.Seleccionado)
        Assert.IsTrue(normal.Preseleccionado)
        Assert.AreEqual(3, normal.NivelRiesgo)
    End Sub

    <TestMethod()>
    Public Sub Casar_CandidatoQueNoVieneEnLaRespuesta_SeQuedaSinRiesgo()
        Dim c = Candidato(700)
        c.RiesgoTexto = "50,0 %"
        c.NivelRiesgo = 3
        c.ProbabilidadDevolucion = 0.5

        RiesgoImpagosRemesa.Casar({c}, Respuesta(New EfectoRiesgoImpagoModel With {.NumOrden = 999, .Probabilidad = 0.4}))

        Assert.IsNull(c.RiesgoTexto)
        Assert.IsNull(c.ProbabilidadDevolucion)
        Assert.AreEqual(0, c.NivelRiesgo)
    End Sub

    <TestMethod()>
    Public Sub Casar_SinNumeroDeOrden_CasaPorEmpresaClienteDocumentoYEfecto()
        Dim c = Candidato(800, cliente:="2002  ", documento:="NV26/77 ", efecto:="2")
        Dim r = Respuesta(New EfectoRiesgoImpagoModel With {
            .NumOrden = 0, .Empresa = "1", .Cliente = "2002", .Documento = "NV26/77", .Efecto = "2", .Probabilidad = 0.08})

        RiesgoImpagosRemesa.Casar({c}, r, "1  ")

        Assert.AreEqual("8,0 %", c.RiesgoTexto)
        Assert.AreEqual(1, c.NivelRiesgo)
    End Sub

    <TestMethod()>
    Public Sub Casar_SinProbabilidad_SinTextoNiColor()
        Dim c = Candidato(900)
        RiesgoImpagosRemesa.Casar({c}, Respuesta(New EfectoRiesgoImpagoModel With {.NumOrden = 900, .Probabilidad = Nothing, .Tramo = Nothing}))
        Assert.IsNull(c.RiesgoTexto)
        Assert.AreEqual(0, c.NivelRiesgo)
    End Sub

    <TestMethod()>
    Public Sub Nivel_SinTramo_UsaLosMismosLimitesQueLaApi()
        Assert.AreEqual(0, RiesgoImpagosRemesa.Nivel(Nothing, 0.0499))
        Assert.AreEqual(1, RiesgoImpagosRemesa.Nivel(Nothing, 0.05))
        Assert.AreEqual(1, RiesgoImpagosRemesa.Nivel(Nothing, 0.1499))
        Assert.AreEqual(2, RiesgoImpagosRemesa.Nivel(Nothing, 0.15))
        Assert.AreEqual(3, RiesgoImpagosRemesa.Nivel(Nothing, 0.3))
        Assert.AreEqual(1, RiesgoImpagosRemesa.Nivel("5-15 %", 0.9))
    End Sub

#End Region

#Region "Resumen"

    <TestMethod()>
    Public Sub ComponerResumen_ConModelo_LaFraseConTotalYTramos()
        Assert.AreEqual(
            "Riesgo de devolución: se esperan 4,2 devoluciones (1.830,50 € en riesgo). 3 recibos con 30 % o más, 5 entre 15 y 30 %.",
            RiesgoImpagosRemesa.ComponerResumen(Respuesta()))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_SinModeloYConMensaje_ElMensaje()
        Dim r = Respuesta()
        r.ConModelo = False
        r.Mensaje = "No hay modelo guardado (o no ha podido predecir): la probabilidad es la de la regla."
        Assert.AreEqual(r.Mensaje, RiesgoImpagosRemesa.ComponerResumen(r))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_SinRespuesta_Nada()
        Assert.IsNull(RiesgoImpagosRemesa.ComponerResumen(Nothing))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_SinTramosAltos_LoDice()
        Dim r = Respuesta()
        r.Tramos = New List(Of TramoRiesgoImpagosModel) From {New TramoRiesgoImpagosModel With {.Tramo = "< 5 %", .Recibos = 4}}
        r.Total.DevolucionesEsperadas = 0.12
        r.Total.ImporteEnRiesgo = 9.5D
        Assert.AreEqual("Riesgo de devolución: se esperan 0,1 devoluciones (9,50 € en riesgo). Ningún recibo con 15 % o más.",
                        RiesgoImpagosRemesa.ComponerResumen(r))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_SoloTramoMedio_UnRecibo()
        Dim r = Respuesta()
        r.Tramos = New List(Of TramoRiesgoImpagosModel) From {New TramoRiesgoImpagosModel With {.Tramo = "15-30 %", .Recibos = 1}}
        r.Total.DevolucionesEsperadas = 1
        r.Total.ImporteEnRiesgo = 20D
        Assert.AreEqual("Riesgo de devolución: se espera 1,0 devolución (20,00 € en riesgo). 1 recibo entre 15 y 30 %.",
                        RiesgoImpagosRemesa.ComponerResumen(r))
    End Sub

#End Region

#Region "ViewModel"

    Private _servicio As IRemesasService

    <TestInitialize()>
    Public Sub Initialize()
        _servicio = A.Fake(Of IRemesasService)()
        A.CallTo(Function() _servicio.LeerFechaCargoPropuesta()).Returns(Task.FromResult(#2026-11-30#))
    End Sub

    Private Function CrearViewModel() As RemesasViewModel
        Return New RemesasViewModel(A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), _servicio)
    End Function

    <TestMethod()>
    Public Async Function VM_Con200_PideElRiesgoConLosMismosParametrosYLoEnsena() As Task
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(10), Candidato(11, preseleccionado:=False)}))
        A.CallTo(Function() _servicio.LeerRiesgoCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(Respuesta(New EfectoRiesgoImpagoModel With {.NumOrden = 11, .Probabilidad = 0.31, .Tramo = "≥ 30 %"})))

        Dim vm = CrearViewModel()
        vm.empresaActual = "1  "
        Await vm.CargarCandidatosAsync()
        Await vm.UltimaCargaRiesgo

        A.CallTo(Function() _servicio.LeerRiesgoCandidatos("1  ", #2026-11-30#)).MustHaveHappenedOnceExactly()
        Assert.IsTrue(vm.HayRiesgo)
        StringAssert.StartsWith(vm.ResumenRiesgo, "Riesgo de devolución: se esperan 4,2 devoluciones")
        Assert.AreEqual("31,0 %", vm.ListaCandidatos.Single(Function(c) c.Id = 11).RiesgoTexto)
        ' Solo informa: la preselección y la selección siguen igual.
        Assert.IsTrue(vm.ListaCandidatos.Single(Function(c) c.Id = 10).Seleccionado)
        Assert.IsFalse(vm.ListaCandidatos.Single(Function(c) c.Id = 11).Seleccionado)
        Assert.AreEqual(1, vm.NumeroEfectosSeleccionados)
    End Function

    <TestMethod()>
    Public Async Function VM_SinPermisoOSinTabla_NoEnsenaNadaNiAvisa() As Task
        ' El servicio devuelve Nothing con 403 o 503.
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(10)}))
        A.CallTo(Function() _servicio.LeerRiesgoCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(Of CandidatosRiesgoImpagosModel)(Nothing))

        Dim vm = CrearViewModel()
        vm.empresaActual = "1  "
        Dim mensajeAntes = vm.mensajeError
        Await vm.CargarCandidatosAsync()
        Await vm.UltimaCargaRiesgo

        Assert.IsFalse(vm.HayRiesgo)
        Assert.IsNull(vm.ResumenRiesgo)
        Assert.IsNull(vm.ListaCandidatos.Single().RiesgoTexto)
        Assert.AreEqual(mensajeAntes, vm.mensajeError)
        Assert.AreEqual(1, vm.ListaCandidatos.Count)
    End Function

    <TestMethod()>
    Public Async Function VM_SiElRiesgoFalla_LaVentanaSigueIgualYSinAviso() As Task
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(10)}))
        A.CallTo(Function() _servicio.LeerRiesgoCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        vm.empresaActual = "1  "
        Await vm.CargarCandidatosAsync()
        Await vm.UltimaCargaRiesgo

        Assert.IsFalse(vm.HayRiesgo)
        Assert.IsNull(vm.ResumenRiesgo)
        Assert.AreEqual(1, vm.ListaCandidatos.Count)
        Assert.IsTrue(vm.ListaCandidatos.Single().Seleccionado)
        Assert.IsTrue(String.IsNullOrEmpty(vm.mensajeError) OrElse Not vm.mensajeError.Contains("API caída"))
    End Function

    <TestMethod()>
    Public Async Function VM_LaCargaNoEsperaAlRiesgo() As Task
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(10)}))
        Dim pendiente As New TaskCompletionSource(Of CandidatosRiesgoImpagosModel)
        A.CallTo(Function() _servicio.LeerRiesgoCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)).Returns(pendiente.Task)

        Dim vm = CrearViewModel()
        vm.empresaActual = "1  "
        Await vm.CargarCandidatosAsync()

        ' Los candidatos ya están y la ventana no está ocupada aunque el riesgo no haya llegado.
        Assert.AreEqual(1, vm.ListaCandidatos.Count)
        Assert.IsFalse(vm.estaOcupado)
        Assert.IsFalse(vm.UltimaCargaRiesgo.IsCompleted)

        pendiente.SetResult(Respuesta(New EfectoRiesgoImpagoModel With {.NumOrden = 10, .Probabilidad = 0.2, .Tramo = "15-30 %"}))
        Await vm.UltimaCargaRiesgo
        Assert.AreEqual("20,0 %", vm.ListaCandidatos.Single().RiesgoTexto)
    End Function

    <TestMethod()>
    Public Async Function VM_UnaRespuestaViejaNoPisaALaNueva() As Task
        Dim primeraLista = New List(Of EfectoCandidatoModel) From {Candidato(10)}
        Dim segundaLista = New List(Of EfectoCandidatoModel) From {Candidato(10)}
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .ReturnsNextFromSequence(Task.FromResult(primeraLista), Task.FromResult(segundaLista))
        Dim vieja As New TaskCompletionSource(Of CandidatosRiesgoImpagosModel)
        Dim nueva As New TaskCompletionSource(Of CandidatosRiesgoImpagosModel)
        A.CallTo(Function() _servicio.LeerRiesgoCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .ReturnsNextFromSequence(vieja.Task, nueva.Task)

        Dim vm = CrearViewModel()
        vm.empresaActual = "1  "
        Await vm.CargarCandidatosAsync()
        Dim cargaVieja = vm.UltimaCargaRiesgo
        Await vm.CargarCandidatosAsync()
        Dim cargaNueva = vm.UltimaCargaRiesgo

        Dim respuestaNueva = Respuesta(New EfectoRiesgoImpagoModel With {.NumOrden = 10, .Probabilidad = 0.02, .Tramo = "< 5 %"})
        nueva.SetResult(respuestaNueva)
        Await cargaNueva
        Dim respuestaVieja = Respuesta(New EfectoRiesgoImpagoModel With {.NumOrden = 10, .Probabilidad = 0.6, .Tramo = "≥ 30 %"})
        respuestaVieja.Total.DevolucionesEsperadas = 9.9
        vieja.SetResult(respuestaVieja)
        Await cargaVieja

        Assert.AreEqual("2,0 %", vm.ListaCandidatos.Single().RiesgoTexto)
        StringAssert.Contains(vm.ResumenRiesgo, "4,2 devoluciones")
        ' Y la lista vieja tampoco se ha tocado.
        Assert.IsNull(primeraLista.Single().RiesgoTexto)
    End Function

#End Region

End Class
