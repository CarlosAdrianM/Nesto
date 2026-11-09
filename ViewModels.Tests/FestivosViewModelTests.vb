Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.ViewModels

''' <summary>
''' Nesto#616 (NestoAPI#779): el mantenimiento de festivos contra api/Festivos. Antes iban escritos en el código de la API
''' y cada año hacía falta publicarla (el olvido del 02/11/2026 descuadró remesas y fechas de entrega).
''' </summary>
<TestClass()>
Public Class FestivosViewModelTests

    Private _servicio As IServicioFestivos
    Private _dialogService As IServicioDialogos
    Private _configuracion As IConfiguracion

    <TestInitialize()>
    Public Sub Inicializar()
        _servicio = A.Fake(Of IServicioFestivos)()
        _dialogService = A.Fake(Of IServicioDialogos)()
        ' Por defecto, Administración: todos los tipos
        _configuracion = A.Fake(Of IConfiguracion)()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(True)
        A.CallTo(Function() _servicio.Leer(A(Of Integer).Ignored)).Returns(Task.FromResult(New List(Of Festivo)))
    End Sub

    Private Shared Function UnFestivo(fecha As Date, tipo As String, descripcion As String, Optional origen As String = TiposFestivo.ORIGEN_TABLA) As Festivo
        Return New Festivo With {.Fecha = fecha, .Tipo = tipo, .Descripcion = descripcion, .Origen = origen}
    End Function

    Private Sub DevolverAlLeer(anio As Integer, ParamArray festivos As Festivo())
        A.CallTo(Function() _servicio.Leer(anio)).Returns(Task.FromResult(New List(Of Festivo)(festivos)))
    End Sub

    Private Function CrearViewModel() As FestivosViewModel
        Return New FestivosViewModel(_servicio, _dialogService, _configuracion)
    End Function

    ''' <summary>NestoAPI#841: un usuario de Almacén (sin ser Administración, Dirección ni Informática).</summary>
    Private Function CrearViewModelDe(grupo As String) As FestivosViewModel
        Dim configuracion = A.Fake(Of IConfiguracion)()
        A.CallTo(Function() configuracion.UsuarioEnGrupo(grupo)).Returns(True)
        Return New FestivosViewModel(_servicio, _dialogService, configuracion)
    End Function

#Region "Carga por año"

    <TestMethod()>
    Public Sub AlAbrir_CargaLosDelAnioActual()
        Dim hoy = Date.Today.Year
        DevolverAlLeer(hoy, UnFestivo(New Date(hoy, 1, 1), TiposFestivo.NACIONAL, "Año Nuevo"))

        Dim vm = CrearViewModel()

        Assert.AreEqual(hoy, vm.Anio)
        A.CallTo(Function() _servicio.Leer(hoy)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(1, vm.Festivos.Count)
        Assert.AreEqual("Año Nuevo", vm.Festivos(0).Descripcion)
    End Sub

    <TestMethod()>
    Public Sub AnioSiguiente_CargaLosDeEseAnio()
        Dim siguiente = Date.Today.Year + 1
        DevolverAlLeer(siguiente, UnFestivo(New Date(siguiente, 3, 19), TiposFestivo.AUTONOMICA, "San José"),
                       UnFestivo(New Date(siguiente, 5, 2), TiposFestivo.AUTONOMICA, "Fiesta de la Comunidad"))
        Dim vm = CrearViewModel()

        vm.AnioSiguienteCommand.Execute(Nothing)

        Assert.AreEqual(siguiente, vm.Anio)
        A.CallTo(Function() _servicio.Leer(siguiente)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(2, vm.Festivos.Count)
        Assert.IsFalse(vm.VienenDelCodigo)
        Assert.IsNull(vm.AvisoOrigen)
    End Sub

    <TestMethod()>
    Public Sub SiVienenDelCodigo_AvisaDeQueHayQueDarlosDeAlta()
        DevolverAlLeer(2027, UnFestivo(New Date(2027, 1, 1), TiposFestivo.NACIONAL, "Año Nuevo", TiposFestivo.ORIGEN_CODIGO))
        Dim vm = CrearViewModel()

        vm.Anio = 2027

        Assert.IsTrue(vm.VienenDelCodigo)
        Assert.AreEqual("Estos festivos vienen del programa, no de la tabla: dé de alta los del año para que cuenten.", vm.AvisoOrigen)
    End Sub

    <TestMethod()>
    Public Sub SiFallaLaCarga_SeEnsenaElError()
        A.CallTo(Function() _servicio.Leer(2027)).ThrowsAsync(New FestivosException("Año fuera de rango.", 400))
        Dim vm = CrearViewModel()

        vm.Anio = 2027

        A.CallTo(Sub() _dialogService.ShowError(A(Of String).That.Contains("Año fuera de rango."))).MustHaveHappenedOnceExactly()
        Assert.AreEqual(0, vm.Festivos.Count)
    End Sub

    <TestMethod()>
    Public Sub ElDiaDeLaSemana_EnEspanol()
        Assert.AreEqual("Viernes", UnFestivo(New Date(2027, 3, 19), TiposFestivo.AUTONOMICA, "San José").DiaSemana)
    End Sub

#End Region

#Region "Alta"

    <TestMethod()>
    Public Async Function Alta_LlamaAlPostYRecarga() As Task
        Dim anio = Date.Today.Year
        Dim vm = CrearViewModel()
        vm.NuevaFecha = New Date(anio, 12, 8)
        vm.NuevoTipo = TiposFestivo.NACIONAL
        vm.NuevaDescripcion = "  Inmaculada Concepción  "

        Await vm.AnadirAsync()

        A.CallTo(Function() _servicio.Guardar(A(Of PeticionFestivo).That.Matches(
            Function(p) p.Fecha = New Date(anio, 12, 8) AndAlso p.Tipo = TiposFestivo.NACIONAL AndAlso p.Descripcion = "Inmaculada Concepción"))).
            MustHaveHappenedOnceExactly()
        ' Una al abrir y otra después del alta
        A.CallTo(Function() _servicio.Leer(anio)).MustHaveHappenedTwiceExactly()
        Assert.IsNull(vm.NuevaDescripcion)
        A.CallTo(Sub() _dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Alta_DeOtroAnio_PasaAEseAnio() As Task
        Dim siguiente = Date.Today.Year + 1
        Dim vm = CrearViewModel()
        vm.NuevaFecha = New Date(siguiente, 1, 6)
        vm.NuevoTipo = TiposFestivo.NACIONAL
        vm.NuevaDescripcion = "Epifanía del Señor"

        Await vm.AnadirAsync()

        Assert.AreEqual(siguiente, vm.Anio)
        A.CallTo(Function() _servicio.Leer(siguiente)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub Alta_SinDescripcion_NoSePuede()
        Dim vm = CrearViewModel()
        vm.NuevaFecha = New Date(2027, 1, 6)
        vm.NuevoTipo = TiposFestivo.NACIONAL

        Assert.IsFalse(vm.AnadirCommand.CanExecute(Nothing))
        vm.NuevaDescripcion = "Epifanía del Señor"
        Assert.IsTrue(vm.AnadirCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Async Function Alta_Con403_EnsenaElMensajeDeLaApi() As Task
        Const SIN_PERMISO = "Solo Administración, Dirección o Informática pueden cambiar los festivos."
        A.CallTo(Function() _servicio.Guardar(A(Of PeticionFestivo).Ignored)).ThrowsAsync(New FestivosException(SIN_PERMISO, 403))
        Dim vm = CrearViewModel()
        vm.NuevaFecha = New Date(Date.Today.Year, 12, 8)
        vm.NuevoTipo = TiposFestivo.NACIONAL
        vm.NuevaDescripcion = "Inmaculada Concepción"

        Await vm.AnadirAsync()

        A.CallTo(Sub() _dialogService.ShowError(SIN_PERMISO)).MustHaveHappenedOnceExactly()
        ' Lo tecleado no se pierde
        Assert.AreEqual("Inmaculada Concepción", vm.NuevaDescripcion)
    End Function

    <TestMethod()>
    Public Async Function Alta_Con409_EnsenaElMensajeTalCual() As Task
        Const SIN_TABLA = "Todavía no existe la tabla Festivos: hay que lanzar Scripts/Issue779_Festivos.sql."
        A.CallTo(Function() _servicio.Guardar(A(Of PeticionFestivo).Ignored)).ThrowsAsync(New FestivosException(SIN_TABLA, 409))
        Dim vm = CrearViewModel()
        vm.NuevaFecha = New Date(Date.Today.Year, 12, 8)
        vm.NuevoTipo = TiposFestivo.NACIONAL
        vm.NuevaDescripcion = "Inmaculada Concepción"

        Await vm.AnadirAsync()

        A.CallTo(Sub() _dialogService.ShowError(SIN_TABLA)).MustHaveHappenedOnceExactly()
    End Function

#End Region

#Region "Baja"

    <TestMethod()>
    Public Async Function Baja_Confirmada_LlamaAlDeleteYRecarga() As Task
        Dim anio = Date.Today.Year
        Dim sanJose = UnFestivo(New Date(anio, 3, 19), TiposFestivo.AUTONOMICA, "San José")
        DevolverAlLeer(anio, sanJose)
        A.CallTo(Function() _dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
        Dim vm = CrearViewModel()
        vm.FestivoSeleccionado = vm.Festivos(0)

        Await vm.BorrarAsync()

        A.CallTo(Function() _servicio.Borrar(New Date(anio, 3, 19), TiposFestivo.AUTONOMICA)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Leer(anio)).MustHaveHappenedTwiceExactly()
        Assert.IsNull(vm.FestivoSeleccionado)
    End Function

    <TestMethod()>
    Public Async Function Baja_SinConfirmar_NoLlamaAlDelete() As Task
        Dim anio = Date.Today.Year
        DevolverAlLeer(anio, UnFestivo(New Date(anio, 3, 19), TiposFestivo.AUTONOMICA, "San José"))
        A.CallTo(Function() _dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(False)
        Dim vm = CrearViewModel()
        vm.FestivoSeleccionado = vm.Festivos(0)

        Await vm.BorrarAsync()

        A.CallTo(Function() _servicio.Borrar(A(Of Date).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub Baja_DeUnoDelCodigo_NoSePuede()
        Dim anio = Date.Today.Year
        DevolverAlLeer(anio, UnFestivo(New Date(anio, 1, 1), TiposFestivo.NACIONAL, "Año Nuevo", TiposFestivo.ORIGEN_CODIGO))
        Dim vm = CrearViewModel()

        vm.FestivoSeleccionado = vm.Festivos(0)

        Assert.IsFalse(vm.BorrarCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Async Function Baja_Con403_EnsenaElMensajeDeLaApi() As Task
        Const SIN_PERMISO = "Solo Administración, Dirección o Informática pueden cambiar los festivos."
        Dim anio = Date.Today.Year
        DevolverAlLeer(anio, UnFestivo(New Date(anio, 3, 19), TiposFestivo.AUTONOMICA, "San José"))
        A.CallTo(Function() _dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
        A.CallTo(Function() _servicio.Borrar(A(Of Date).Ignored, A(Of String).Ignored)).ThrowsAsync(New FestivosException(SIN_PERMISO, 403))
        Dim vm = CrearViewModel()
        vm.FestivoSeleccionado = vm.Festivos(0)

        Await vm.BorrarAsync()

        A.CallTo(Sub() _dialogService.ShowError(SIN_PERMISO)).MustHaveHappenedOnceExactly()
    End Function

#End Region

#Region "NestoAPI#841: día sin reparto propio"

    <TestMethod()>
    Public Sub Tipos_IncluyeSinRepartoPropioConSuEtiqueta()
        Dim vm = CrearViewModel()

        Dim opcion = vm.OpcionesTipo.Single(Function(o) o.Valor = TiposFestivo.REPARTO_PROPIO)
        Assert.AreEqual("Sin reparto propio (furgoneta)", opcion.Etiqueta)
        CollectionAssert.Contains(vm.Tipos.ToList(), "RepartoPropio")
        Assert.AreEqual(TiposFestivo.Todos.Count, vm.OpcionesTipo.Count, "Administración ve todos los tipos")
        Assert.IsFalse(vm.SoloRepartoPropio)
        Assert.IsNull(vm.AvisoPermisos)
    End Sub

    <TestMethod()>
    Public Sub Almacen_SoloElTipoSinRepartoPropio()
        Dim vm = CrearViewModelDe(Constantes.GruposSeguridad.ALMACEN)

        Assert.IsTrue(vm.SoloRepartoPropio)
        Assert.AreEqual(1, vm.OpcionesTipo.Count)
        Assert.AreEqual("Sin reparto propio (furgoneta)", vm.OpcionesTipo(0).Etiqueta)
        Assert.AreEqual(TiposFestivo.REPARTO_PROPIO, vm.NuevoTipo)
        Assert.AreEqual(FestivosViewModel.AVISO_SOLO_REPARTO_PROPIO, vm.AvisoPermisos)
    End Sub

    <TestMethod()>
    Public Sub Almacen_QueTambienEsDireccion_VeTodosLosTipos()
        Dim configuracion = A.Fake(Of IConfiguracion)()
        A.CallTo(Function() configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(True)
        A.CallTo(Function() configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(True)

        Dim vm = New FestivosViewModel(_servicio, _dialogService, configuracion)

        Assert.IsFalse(vm.SoloRepartoPropio)
        Assert.AreEqual(TiposFestivo.Todos.Count, vm.OpcionesTipo.Count)
    End Sub

    <TestMethod()>
    Public Async Function Almacen_DaDeAltaUnDiaSinRepartoPropio() As Task
        Dim anio = Date.Today.Year
        Dim vm = CrearViewModelDe(Constantes.GruposSeguridad.ALMACEN)
        vm.NuevaFecha = New Date(anio, 12, 24)
        vm.NuevaDescripcion = "Andre y Miguel libres"

        Assert.IsTrue(vm.AnadirCommand.CanExecute(Nothing))
        Await vm.AnadirAsync()

        A.CallTo(Function() _servicio.Guardar(A(Of PeticionFestivo).That.Matches(Function(p) p.Tipo = TiposFestivo.REPARTO_PROPIO))).
            MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Almacen_NoPuedeDarDeAltaOtroTipo() As Task
        Dim vm = CrearViewModelDe(Constantes.GruposSeguridad.ALMACEN)
        vm.NuevaFecha = New Date(Date.Today.Year, 12, 8)
        vm.NuevoTipo = TiposFestivo.NACIONAL
        vm.NuevaDescripcion = "Inmaculada Concepción"

        Assert.IsFalse(vm.AnadirCommand.CanExecute(Nothing))
        Await vm.AnadirAsync()

        A.CallTo(Function() _servicio.Guardar(A(Of PeticionFestivo).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() _dialogService.ShowError(FestivosViewModel.ERROR_SOLO_REPARTO_PROPIO)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Almacen_SoloBorraLosDiasSinRepartoPropio() As Task
        Dim anio = Date.Today.Year
        Dim sanJose = UnFestivo(New Date(anio, 3, 19), TiposFestivo.AUTONOMICA, "San José")
        Dim sinFurgoneta = UnFestivo(New Date(anio, 11, 9), TiposFestivo.REPARTO_PROPIO, "Andre y Miguel libres")
        DevolverAlLeer(anio, sanJose, sinFurgoneta)
        A.CallTo(Function() _dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
        Dim vm = CrearViewModelDe(Constantes.GruposSeguridad.ALMACEN)
        Await vm.CargarAsync()

        Assert.AreEqual(2, vm.Festivos.Count, "La lista enseña todos")
        vm.FestivoSeleccionado = sanJose
        Assert.IsFalse(vm.BorrarCommand.CanExecute(Nothing), "Un festivo de otro tipo no")
        Await vm.BorrarAsync()
        A.CallTo(Function() _servicio.Borrar(A(Of Date).Ignored, TiposFestivo.AUTONOMICA)).MustNotHaveHappened()

        vm.FestivoSeleccionado = sinFurgoneta
        Assert.IsTrue(vm.BorrarCommand.CanExecute(Nothing))
        Await vm.BorrarAsync()
        A.CallTo(Function() _servicio.Borrar(New Date(anio, 11, 9), TiposFestivo.REPARTO_PROPIO)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Alta_SinRepartoPropio_MandaElTipoDeLaApiYLaDescripcion() As Task
        Dim anio = Date.Today.Year
        Dim vm = CrearViewModel()
        vm.NuevaFecha = New Date(anio, 11, 9)
        vm.NuevoTipo = vm.OpcionesTipo.Single(Function(o) o.Etiqueta = "Sin reparto propio (furgoneta)").Valor
        vm.NuevaDescripcion = "Andre y Miguel libres"

        Await vm.AnadirAsync()

        A.CallTo(Function() _servicio.Guardar(A(Of PeticionFestivo).That.Matches(
            Function(p) p.Tipo = "RepartoPropio" AndAlso p.Descripcion = "Andre y Miguel libres"))).
            MustHaveHappenedOnceExactly()
    End Function

#End Region

End Class
