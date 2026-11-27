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

    ' Un candidato con su riesgo ya casado (lo que deja Casar) y marcado o no en la rejilla.
    Private Shared Function ConRiesgo(id As Integer, probabilidad As Double?, seleccionado As Boolean,
                                      Optional importe As Decimal = 100D) As EfectoCandidatoModel
        Dim c = Candidato(id, preseleccionado:=seleccionado)
        c.ImportePendiente = importe
        c.Seleccionado = seleccionado
        c.ProbabilidadDevolucion = probabilidad
        c.NivelRiesgo = RiesgoImpagosRemesa.Nivel(Nothing, probabilidad)
        Return c
    End Function

    <TestMethod()>
    Public Sub ComponerResumen_SeCalculaConLoSeleccionado_NoConElTotalDeLaApi()
        ' La API trae 4,2 esperadas y 3+5 altos contando los retenidos: no se usan.
        Dim candidatos = {
            ConRiesgo(1, 0.31, True, 1000D), ConRiesgo(2, 0.35, True, 500D), ConRiesgo(3, 0.2, True, 200D),
            ConRiesgo(4, 0.01, True, 300D), ConRiesgo(5, Nothing, True, 50D)}
        Assert.AreEqual(
            "Riesgo de devolución: se esperan 0,9 devoluciones (528,00 € en riesgo). 2 recibos con 30 % o más, 1 entre 15 y 30 %.",
            RiesgoImpagosRemesa.ComponerResumen(Respuesta(), candidatos))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_CasoReal27Nov_DieciochoSeleccionadosYDosAltosRetenidos()
        ' 54 candidatos: 18 seleccionados (máximo 6,9 %, 0,37 esperadas) y 36 retenidos, dos de ellos con 15 % o más.
        Dim candidatos As New List(Of EfectoCandidatoModel) From {ConRiesgo(1, 0.069, True)}
        For i = 2 To 18
            candidatos.Add(ConRiesgo(i, 0.0177, True))
        Next
        candidatos.Add(ConRiesgo(19, 0.2, False))
        candidatos.Add(ConRiesgo(20, 0.35, False))
        For i = 21 To 54
            candidatos.Add(ConRiesgo(i, 0.01, False))
        Next
        Assert.AreEqual(
            "Riesgo de devolución: se esperan 0,4 devoluciones (36,99 € en riesgo). Ningún recibo con 15 % o más. Fuera de la selección hay 2 recibos con 15 % o más.",
            RiesgoImpagosRemesa.ComponerResumen(Respuesta(), candidatos))

        ' Al marcar uno de los altos, entra en la frase y sale de «fuera».
        candidatos.Single(Function(c) c.Id = 20).Seleccionado = True
        Assert.AreEqual(
            "Riesgo de devolución: se esperan 0,7 devoluciones (71,99 € en riesgo). 1 recibo con 30 % o más. Fuera de la selección hay 1 recibo con 15 % o más.",
            RiesgoImpagosRemesa.ComponerResumen(Respuesta(), candidatos))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_NadaSeleccionado()
        Dim candidatos = {ConRiesgo(1, 0.4, False), ConRiesgo(2, 0.02, False)}
        Assert.AreEqual("No hay recibos seleccionados. Fuera de la selección hay 1 recibo con 15 % o más.",
                        RiesgoImpagosRemesa.ComponerResumen(Respuesta(), candidatos))
        Assert.AreEqual("No hay recibos seleccionados.",
                        RiesgoImpagosRemesa.ComponerResumen(Respuesta(), {ConRiesgo(3, 0.02, False)}))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_SinModeloYConMensaje_ElMensaje()
        Dim r = Respuesta()
        r.ConModelo = False
        r.Mensaje = "No hay modelo guardado (o no ha podido predecir): la probabilidad es la de la regla."
        Assert.AreEqual(r.Mensaje, RiesgoImpagosRemesa.ComponerResumen(r, {ConRiesgo(1, 0.4, True)}))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_SinRespuesta_Nada()
        Assert.IsNull(RiesgoImpagosRemesa.ComponerResumen(Nothing, {ConRiesgo(1, 0.4, True)}))
    End Sub

    <TestMethod()>
    Public Sub ComponerResumen_UnaDevolucionYUnSoloMedio_EnSingular()
        Dim candidatos = {ConRiesgo(1, 0.2, True, 20D), ConRiesgo(2, 0.8, True, 0D)}
        Assert.AreEqual("Riesgo de devolución: se espera 1,0 devolución (4,00 € en riesgo). 1 recibo con 30 % o más, 1 entre 15 y 30 %.",
                        RiesgoImpagosRemesa.ComponerResumen(Respuesta(), candidatos))
        Dim soloMedio = {ConRiesgo(3, 0.2, True, 20D)}
        Assert.AreEqual("Riesgo de devolución: se esperan 0,2 devoluciones (4,00 € en riesgo). 1 recibo entre 15 y 30 %.",
                        RiesgoImpagosRemesa.ComponerResumen(Respuesta(), soloMedio))
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
        ' El resumen es de lo seleccionado (el 10, sin riesgo), no del Total de la API (4,2).
        Assert.AreEqual("Riesgo de devolución: se esperan 0,0 devoluciones (0,00 € en riesgo). Ningún recibo con 15 % o más. Fuera de la selección hay 1 recibo con 15 % o más.",
                        vm.ResumenRiesgo)
        Assert.AreEqual("31,0 %", vm.ListaCandidatos.Single(Function(c) c.Id = 11).RiesgoTexto)
        ' Solo informa: la preselección y la selección siguen igual.
        Assert.IsTrue(vm.ListaCandidatos.Single(Function(c) c.Id = 10).Seleccionado)
        Assert.IsFalse(vm.ListaCandidatos.Single(Function(c) c.Id = 11).Seleccionado)
        Assert.AreEqual(1, vm.NumeroEfectosSeleccionados)
    End Function

    <TestMethod()>
    Public Async Function VM_AlMarcarODesmarcar_SeRecalculaElResumen() As Task
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(10), Candidato(11, preseleccionado:=False)}))
        A.CallTo(Function() _servicio.LeerRiesgoCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(Respuesta(New EfectoRiesgoImpagoModel With {.NumOrden = 11, .Probabilidad = 0.31, .Tramo = "≥ 30 %"})))

        Dim vm = CrearViewModel()
        vm.empresaActual = "1  "
        Await vm.CargarCandidatosAsync()
        Await vm.UltimaCargaRiesgo

        vm.ListaCandidatos.Single(Function(c) c.Id = 11).Seleccionado = True
        Assert.AreEqual("Riesgo de devolución: se esperan 0,3 devoluciones (31,00 € en riesgo). 1 recibo con 30 % o más.", vm.ResumenRiesgo)

        vm.DesmarcarTodosCommand.Execute(Nothing)
        Assert.AreEqual("No hay recibos seleccionados. Fuera de la selección hay 1 recibo con 15 % o más.", vm.ResumenRiesgo)
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
        ' 2 % de 100 € (la vieja, 60 %, daría 60,00 €).
        StringAssert.Contains(vm.ResumenRiesgo, "(2,00 € en riesgo)")
        ' Y la lista vieja tampoco se ha tocado.
        Assert.IsNull(primeraLista.Single().RiesgoTexto)
    End Function

#End Region

End Class
