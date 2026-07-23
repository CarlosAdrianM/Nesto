Imports System.Windows.Input
Imports System.ComponentModel
Imports System.Collections.ObjectModel
Imports System.Windows
Imports Microsoft.Win32
Imports System.Windows.Controls
Imports Nesto.Contratos
Imports Prism.Mvvm
Imports Prism.Commands
Imports Microsoft.Graph
Imports Prism.Services.Dialogs
Imports Azure.Identity
Imports Nesto.Infrastructure.Shared
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports System.Collections.Specialized
Imports Unity
Imports ControlesUsuario.Dialogs

Public Class RemesasViewModel
    Inherits BindableBase

    Const numRemesas = 100

    ' Nesto#340 Fase 1C.14 slices 6-8: este VM ya NO usa EF. Todos los accesos a datos
    ' (lecturas, los 2 SP de remesas/impagados y los datos de las tareas de Planner) van
    ' por IRemesasService contra NestoAPI.
    Dim empresaDefecto As String = "1" 'mainModel.leerParametro("1", "EmpresaPorDefecto")
    Dim blnPuedeVerTodasLasRemesas As Boolean = True

    Private ReadOnly dialogService As IDialogService
    ' Nesto#340 Fase 1C.14: servicio API que va sustituyendo los accesos EF de este VM.
    Private ReadOnly _remesasService As IRemesasService

    Public Structure tipoRemesa
        Public Sub New(
       ByVal _id As String,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As String
        Property descripcion As String
    End Structure

    Public Sub New(interactiveBrowserCredential As InteractiveBrowserCredential, configuracion As IConfiguracion, dialogService As IDialogService, container As IUnityContainer)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Titulo = "Remesas"
        Me.InteractiveBrowserCredential = interactiveBrowserCredential
        Me.configuracion = configuracion
        Me.dialogService = dialogService
        Dim servicioAutenticacion = container.Resolve(Of IServicioAutenticacion)()
        _remesasService = New RemesasService(configuracion, servicioAutenticacion)
        listaEmpresas = New ObservableCollection(Of EmpresaModel)
        CargarEmpresasAsync()
        ' Nesto#340 Fase 1C.14 slices 2 y 4: el setter de empresaActual ya carga remesas e
        ' impagados por API (async, con selección inicial dentro), así que aquí no hace falta
        ' repetir las consultas ni seleccionar el primer impagado.
        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        listaTiposRemesa = New ObservableCollection(Of tipoRemesa)
        tipoRemesaActual = New tipoRemesa("B2B", "Profesionales (B2B)")
        listaTiposRemesa.Add(tipoRemesaActual)
        listaTiposRemesa.Add(New tipoRemesa("CORE", "Particulares (CORE)"))
        tipoRemesaActual = listaTiposRemesa.LastOrDefault ' Por defecto es CORE -> FirstOrDefault para B2B
        fechaCobro = Today

        CrearTareasPlannerCommand = New DelegateCommand(AddressOf OnCrearTareasPlanner, AddressOf CanCrearTareasPlanner)
        ' NestoAPI#332: pestaña Crear Remesa
        ' NestoAPI#345: pedir ya la fecha "hasta" propuesta, que el DatePicker no muestre hoy
        Dim unusedFecha = InicializarFechaSeleccionAsync()
        CargarCandidatosCommand = New DelegateCommand(AddressOf OnCargarCandidatos)
        CrearRemesaCommand = New DelegateCommand(AddressOf OnCrearRemesa, AddressOf CanCrearRemesa)
        MarcarTodosCommand = New DelegateCommand(AddressOf OnMarcarTodos)
        DesmarcarTodosCommand = New DelegateCommand(AddressOf OnDesmarcarTodos)
        ImprimirRemesaCommand = New DelegateCommand(AddressOf OnImprimirRemesa, AddressOf CanImprimirRemesa)
    End Sub

    ' Constructor para tests: inyecta el servicio API y NO toca EF (Nesto#340 Fase 1C.14).
    Public Sub New(configuracion As IConfiguracion, dialogService As IDialogService, remesasService As IRemesasService)
        Titulo = "Remesas"
        Me.configuracion = configuracion
        Me.dialogService = dialogService
        _remesasService = remesasService
        listaEmpresas = New ObservableCollection(Of EmpresaModel)
        CargarCandidatosCommand = New DelegateCommand(AddressOf OnCargarCandidatos)
        CrearRemesaCommand = New DelegateCommand(AddressOf OnCrearRemesa, AddressOf CanCrearRemesa)
        MarcarTodosCommand = New DelegateCommand(AddressOf OnMarcarTodos)
        DesmarcarTodosCommand = New DelegateCommand(AddressOf OnDesmarcarTodos)
        ImprimirRemesaCommand = New DelegateCommand(AddressOf OnImprimirRemesa, AddressOf CanImprimirRemesa)
    End Sub

    ' Nesto#340 Fase 1C.14 slice 1: sustituye la lectura EF de DbContext.Empresas.
    ' Es Function As Task (no Sub) para que los tests puedan await; los call sites de
    ' fire-and-forget la invocan como sentencia y la excepción queda capturada aquí dentro.
    Public Async Function CargarEmpresasAsync() As Task
        Try
            Dim empresas = Await _remesasService.LeerEmpresas()
            listaEmpresas = New ObservableCollection(Of EmpresaModel)(empresas)
        Catch ex As Exception
            mensajeError = $"No se han podido cargar las empresas: {ex.Message}"
        End Try
    End Function

    ' Nesto#340 Fase 1C.14 slice 3: sustituye la lectura EF de DbContext.ExtractoCliente
    ' (efectos de la remesa, TipoApunte = 3).
    Public Async Function CargarMovimientosAsync(remesa As Integer) As Task
        Try
            Dim movimientos = Await _remesasService.LeerMovimientos(empresaActual, remesa)
            listaMovimientos = New ObservableCollection(Of MovimientoRemesaModel)(movimientos)
        Catch ex As Exception
            listaMovimientos = New ObservableCollection(Of MovimientoRemesaModel)
            mensajeError = $"No se han podido cargar los movimientos de la remesa: {ex.Message}"
        End Try
    End Function

    ' Nesto#340 Fase 1C.14 slice 4: sustituye el GROUP BY EF de impagados (TipoApunte = 4).
    ' Selecciona el primer asiento al terminar (antes lo hacía el constructor en síncrono).
    Public Async Function CargarImpagadosAsync(top As Integer?) As Task
        Try
            Dim impagados = Await _remesasService.LeerImpagados(empresaActual, top)
            listaImpagados = New ObservableCollection(Of impagado)(impagados)
            impagadoActual = listaImpagados.FirstOrDefault
        Catch ex As Exception
            listaImpagados = New ObservableCollection(Of impagado)
            impagadoActual = Nothing
            mensajeError = $"No se han podido cargar los impagados: {ex.Message}"
        End Try
    End Function

    ' Nesto#340 Fase 1C.14 slice 5: sustituye la lectura EF del detalle del asiento de impagados.
    Public Async Function CargarMovimientosImpagadoAsync(asiento As Integer) As Task
        Try
            Dim movimientos = Await _remesasService.LeerMovimientosImpagado(empresaActual, asiento)
            listaImpagadosDetalle = New ObservableCollection(Of MovimientoRemesaModel)(movimientos)
        Catch ex As Exception
            ' No puede quedarse pintado el detalle del asiento anterior (mismo criterio que
            ' los movimientos de la remesa).
            listaImpagadosDetalle = New ObservableCollection(Of MovimientoRemesaModel)
            mensajeError = $"No se han podido cargar los movimientos del impagado: {ex.Message}"
        End Try
    End Function

    ' NestoAPI#332: candidatos a remesa (modo simulación del servidor). Los preseleccionados
    ' vienen marcados; los retenidos (gating #172) llegan con motivo y sin marcar.
    ' NestoAPI#345: la primera vez se propone la fecha "hasta" del servidor (hoy +
    ' DiasAntelacionRemesa del usuario, saltando fines de semana y festivos).
    Public Async Function CargarCandidatosAsync() As Task
        Try
            estaOcupado = True
            Await InicializarFechaSeleccionAsync()
            Dim candidatos = Await _remesasService.LeerEfectosCandidatos(empresaActual, FechaSeleccionHasta)
            For Each candidato In candidatos
                candidato.Seleccionado = candidato.Preseleccionado
                AddHandler candidato.PropertyChanged, AddressOf CandidatoCambiado
            Next
            ListaCandidatos = New ObservableCollection(Of EfectoCandidatoModel)(candidatos)
        Catch ex As Exception
            ListaCandidatos = New ObservableCollection(Of EfectoCandidatoModel)
            mensajeError = $"No se han podido cargar los efectos candidatos: {ex.Message}"
        Finally
            estaOcupado = False
        End Try
    End Function

    Private Sub CandidatoCambiado(sender As Object, e As ComponentModel.PropertyChangedEventArgs)
        If e.PropertyName = NameOf(EfectoCandidatoModel.Seleccionado) Then
            RaisePropertyChanged(NameOf(ImporteSeleccionado))
            RaisePropertyChanged(NameOf(NumeroEfectosSeleccionados))
            CrearRemesaCommand.RaiseCanExecuteChanged()
        End If
    End Sub

    ' NestoAPI#332: crear la remesa con los efectos marcados. El servidor revalida TODO
    ' (candidatos frescos, gating, neteo) y contabiliza; aquí solo confirmación y refresco.
    Public Async Function CrearRemesaAsync() As Task
        Dim seleccionados = ListaCandidatos.Where(Function(c) c.Seleccionado).ToList()
        If Not seleccionados.Any() Then
            Return
        End If

        Dim clientesConNegativos = seleccionados.Where(Function(c) c.ClienteConNegativos) _
            .Select(Function(c) c.Cliente).Distinct().ToList()
        If clientesConNegativos.Any() Then
            mensajeError = "Hay clientes con movimientos negativos pendientes de revisar (liquidar en " &
                "Extracto de Cliente o desmarcarlos): " & String.Join(", ", clientesConNegativos)
            Return
        End If

        Dim importe = seleccionados.Sum(Function(c) c.ImportePendiente)
        ' NestoAPI#345: el modo de vencimientos forma parte de la confirmación, que el usuario
        ' vea exactamente qué va a pasar con las fechas de cargo.
        Dim textoVencimientos = If(RespetarVencimientos,
            "respetando el vencimiento original de cada efecto (un cargo al banco por fecha)",
            $"con fecha de cargo {FechaCargo:dd/MM/yyyy} para todos los efectos")
        Dim confirmado As Boolean = False
        dialogService.ShowConfirmation("Crear remesa",
            $"¿Crear la remesa con {seleccionados.Count} efectos por un total de {importe:C} al banco {BancoRemesa}, {textoVencimientos}?",
            Sub(r) confirmado = r.Result = Prism.Services.Dialogs.ButtonResult.OK)
        If Not confirmado Then
            Return
        End If

        Dim numeroCreado As Integer = 0
        Try
            estaOcupado = True
            Dim resultado = Await _remesasService.CrearRemesa(empresaActual, BancoRemesa,
                seleccionados.Select(Function(c) c.Id).ToList(),
                RespetarVencimientos, FechaCargo, FechaSeleccionHasta)
            numeroCreado = resultado.NumeroRemesa
            mensajeError = $"Remesa {resultado.NumeroRemesa} creada: {resultado.NumeroEfectos} efectos, {resultado.Importe:C}"
            ' Refrescar: la remesa nueva aparece en la lista y los efectos salen de candidatos
            Await CargarRemesasAsync(numRemesas)
            Await CargarCandidatosAsync()
        Catch ex As Exception
            mensajeError = ex.Message
        Finally
            estaOcupado = False
        End Try

        ' NestoAPI#353: ofrecer imprimir el informe nada más crear la remesa.
        If numeroCreado > 0 Then
            Dim imprimir As Boolean = False
            dialogService.ShowConfirmation("Imprimir remesa",
                $"¿Desea imprimir la remesa {numeroCreado}?",
                Sub(r) imprimir = r.Result = Prism.Services.Dialogs.ButtonResult.OK)
            If imprimir Then
                Await ImprimirRemesaAsync(numeroCreado)
            End If
        End If
    End Function

    ' NestoAPI#353: descarga el informe de la remesa (QuestPDF en el backend) y lo abre.
    ' La acción de abrir es sustituible para que los tests no lancen un visor de PDF real.
    Public Async Function ImprimirRemesaAsync(remesa As Integer) As Task
        Try
            estaOcupado = True
            Dim pdf As Byte() = Await _remesasService.DescargarInformeRemesaPdf(empresaActual, remesa)
            Dim fichero As String = IO.Path.Combine(IO.Path.GetTempPath(), $"Remesa_{remesa}.pdf")
            IO.File.WriteAllBytes(fichero, pdf)
            AbrirFicheroAccion.Invoke(fichero)
        Catch ex As Exception
            mensajeError = $"No se ha podido imprimir la remesa {remesa}: {ex.Message}"
        Finally
            estaOcupado = False
        End Try
    End Function

    Public Property AbrirFicheroAccion As Action(Of String) = AddressOf AbrirConShell
    Private Shared Sub AbrirConShell(ruta As String)
        Dim unused = System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(ruta) With {
            .UseShellExecute = True
        })
    End Sub

    ' Nesto#340 Fase 1C.14 slice 2: sustituye la lectura EF de DbContext.Remesas.
    ' top = numRemesas en la carga normal; Nothing = todas (botón "Ver Todas").
    Public Async Function CargarRemesasAsync(top As Integer?) As Task
        Try
            Dim remesas = Await _remesasService.LeerRemesas(empresaActual, top)
            listaRemesas = New ObservableCollection(Of RemesaModel)(remesas)
            remesaActual = listaRemesas.FirstOrDefault
            ' NestoAPI#332: banco por defecto para crear remesa = el de la última remesa
            If String.IsNullOrWhiteSpace(BancoRemesa) Then
                BancoRemesa = listaRemesas.FirstOrDefault()?.Banco
            End If
        Catch ex As Exception
            mensajeError = $"No se han podido cargar las remesas: {ex.Message}"
        End Try
    End Function


#Region "Propiedades"

    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            SetProperty(_titulo, value)
        End Set
    End Property

    'Private Property app As IPublicClientApplication
    Private Property InteractiveBrowserCredential As InteractiveBrowserCredential
    Private Property configuracion As IConfiguracion

    Private Property _listaEmpresas As ObservableCollection(Of EmpresaModel)
    Public Property listaEmpresas As ObservableCollection(Of EmpresaModel)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of EmpresaModel))
            _listaEmpresas = value
            RaisePropertyChanged("listaEmpresas")
        End Set
    End Property

    Private Property _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            ' Nesto#340 Fase 1C.14 slices 2 y 4: remesas e impagados por API (fire-and-forget;
            ' el error queda en mensajeError).
            CargarRemesasAsync(numRemesas)
            blnPuedeVerTodasLasRemesas = True
            CargarImpagadosAsync(numRemesas)
            RaisePropertyChanged("empresaActual")
        End Set
    End Property

    ' Nesto#340 Fase 1C.14 slice 2: POCO del API en vez de la entidad EF Remesas.
    Private Property _remesaActual As RemesaModel
    Public Property remesaActual As RemesaModel
        Get
            Return _remesaActual
        End Get
        Set(value As RemesaModel)
            _remesaActual = value
            If IsNothing(remesaActual) Then
                listaMovimientos = Nothing
            Else
                ' Nesto#340 Fase 1C.14 slice 3: los movimientos se leen del API (fire-and-forget;
                ' el error queda en mensajeError).
                CargarMovimientosAsync(remesaActual.Numero)
            End If
            ImprimirRemesaCommand?.RaiseCanExecuteChanged()
            RaisePropertyChanged("remesaActual")
        End Set
    End Property

    Private Property _contenidoFichero As Xml.Linq.XDocument
    Public Property contenidoFichero As Xml.Linq.XDocument
        Get
            Return _contenidoFichero
        End Get
        Set(value As Xml.Linq.XDocument)
            _contenidoFichero = value
            RaisePropertyChanged("contenidoFichero")
        End Set
    End Property

    Private Property _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            _mensajeError = value
            RaisePropertyChanged("mensajeError")
        End Set
    End Property

    Private Property _listaRemesas As ObservableCollection(Of RemesaModel)
    Public Property listaRemesas As ObservableCollection(Of RemesaModel)
        Get
            Return _listaRemesas
        End Get
        Set(value As ObservableCollection(Of RemesaModel))
            _listaRemesas = value
            RaisePropertyChanged("listaRemesas")
        End Set
    End Property

    Private Property _listaMovimientos As ObservableCollection(Of MovimientoRemesaModel)
    Public Property listaMovimientos As ObservableCollection(Of MovimientoRemesaModel)
        Get
            Return _listaMovimientos
        End Get
        Set(value As ObservableCollection(Of MovimientoRemesaModel))
            _listaMovimientos = value
            RaisePropertyChanged("listaMovimientos")
        End Set
    End Property

    Private Property _pestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _pestañaSeleccionada
        End Get
        Set(value As TabItem)
            SetProperty(_pestañaSeleccionada, value)
            If _pestañaSeleccionada.Header = "Impagados" AndAlso String.IsNullOrEmpty(usuarioTareas) Then
                usuarioTareas = configuracion.LeerParametroSync(empresaActual, "UsuarioAvisoImpagadoDefecto")
                RaisePropertyChanged(NameOf(usuarioTareas))
            End If
            ' Nesto#424: Cartera viva es ahora la primera pestaña; al entrar (incluida la
            ' apertura de la ventana) se cargan los candidatos solos si aún no hay lista.
            If _pestañaSeleccionada.Header = "Cartera viva" AndAlso
                (ListaCandidatos Is Nothing OrElse Not ListaCandidatos.Any) Then
                Dim unused = CargarCandidatosAsync()
            End If
        End Set
    End Property

    Private Property _listaImpagados As ObservableCollection(Of impagado)
    Public Property listaImpagados As ObservableCollection(Of impagado)
        Get
            Return _listaImpagados
        End Get
        Set(value As ObservableCollection(Of impagado))
            _listaImpagados = value
            RaisePropertyChanged("listaImpagados")
        End Set
    End Property

    ' Nesto#340 Fase 1C.14 slice 5: POCO del API en vez de la entidad EF ExtractoCliente.
    Private Property _listaImpagadosDetalle As ObservableCollection(Of MovimientoRemesaModel)
    Public Property listaImpagadosDetalle As ObservableCollection(Of MovimientoRemesaModel)
        Get
            Return _listaImpagadosDetalle
        End Get
        Set(value As ObservableCollection(Of MovimientoRemesaModel))
            _listaImpagadosDetalle = value
            RaisePropertyChanged("listaImpagadosDetalle")
        End Set
    End Property

    ' NestoAPI#332: pestaña Crear Remesa
    Private _listaCandidatos As ObservableCollection(Of EfectoCandidatoModel) = New ObservableCollection(Of EfectoCandidatoModel)
    Public Property ListaCandidatos As ObservableCollection(Of EfectoCandidatoModel)
        Get
            Return _listaCandidatos
        End Get
        Set(value As ObservableCollection(Of EfectoCandidatoModel))
            _listaCandidatos = value
            RaisePropertyChanged(NameOf(ListaCandidatos))
            RaisePropertyChanged(NameOf(ImporteSeleccionado))
            RaisePropertyChanged(NameOf(NumeroEfectosSeleccionados))
            CrearRemesaCommand?.RaiseCanExecuteChanged()
        End Set
    End Property

    Public ReadOnly Property ImporteSeleccionado As Decimal
        Get
            Return If(ListaCandidatos?.Where(Function(c) c.Seleccionado).Sum(Function(c) c.ImportePendiente), 0D)
        End Get
    End Property

    Public ReadOnly Property NumeroEfectosSeleccionados As Integer
        Get
            Return If(ListaCandidatos Is Nothing, 0, ListaCandidatos.Where(Function(c) c.Seleccionado).Count())
        End Get
    End Property

    Private _bancoRemesa As String
    Public Property BancoRemesa As String
        Get
            Return _bancoRemesa
        End Get
        Set(value As String)
            Dim unused = SetProperty(_bancoRemesa, value)
        End Set
    End Property

    ' NestoAPI#345: hasta qué VENCIMIENTO se cargan candidatos. Default local = siguiente día
    ' laborable (sin festivos); la propuesta del SERVIDOR (con DiasAntelacionRemesa y festivos)
    ' la refina en cuanto responde InicializarFechaSeleccionAsync.
    Private _fechaSeleccionInicializada As Boolean
    Private _fechaSeleccionHasta As Date = ProximoDiaLaborable(Today.AddDays(1))
    Public Property FechaSeleccionHasta As Date
        Get
            Return _fechaSeleccionHasta
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fechaSeleccionHasta, value)
        End Set
    End Property

    ' Fallback local sin festivos (solo salta fines de semana): el servidor manda cuando responde.
    Private Shared Function ProximoDiaLaborable(fecha As Date) As Date
        While fecha.DayOfWeek = DayOfWeek.Saturday OrElse fecha.DayOfWeek = DayOfWeek.Sunday
            fecha = fecha.AddDays(1)
        End While
        Return fecha
    End Function

    ' NestoAPI#345: pide al servidor la fecha propuesta (hoy + DiasAntelacionRemesa del usuario,
    ' saltando fines de semana y festivos). Solo la primera vez; si falla, queda el fallback local.
    Public Async Function InicializarFechaSeleccionAsync() As Task
        If _fechaSeleccionInicializada Then
            Return
        End If
        Try
            FechaSeleccionHasta = Await _remesasService.LeerFechaCargoPropuesta()
        Catch
            ' El fallback local (siguiente laborable) ya está puesto
        End Try
        _fechaSeleccionInicializada = True
    End Function

    ' NestoAPI#345: True (DEFAULT, criterio Carlos 22/07) = cada efecto conserva su vencimiento
    ' original (un cargo por fecha, suelo hoy); False = todos los efectos a FechaCargo.
    Private _respetarVencimientos As Boolean = True
    Public Property RespetarVencimientos As Boolean
        Get
            Return _respetarVencimientos
        End Get
        Set(value As Boolean)
            If SetProperty(_respetarVencimientos, value) Then
                RaisePropertyChanged(NameOf(ForzarFechaUnica))
            End If
        End Set
    End Property

    ' Espejo de RespetarVencimientos para el par de RadioButtons del XAML (sin converters).
    Public Property ForzarFechaUnica As Boolean
        Get
            Return Not RespetarVencimientos
        End Get
        Set(value As Boolean)
            RespetarVencimientos = Not value
        End Set
    End Property

    ' NestoAPI#345: fecha de cargo (default hoy; el servidor no admite fechas pasadas).
    Private _fechaCargo As Date = Today
    Public Property FechaCargo As Date
        Get
            Return _fechaCargo
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fechaCargo, value)
        End Set
    End Property

    Public Property CargarCandidatosCommand As DelegateCommand
    Public Property CrearRemesaCommand As DelegateCommand
    Public Property MarcarTodosCommand As DelegateCommand
    Public Property DesmarcarTodosCommand As DelegateCommand
    ' NestoAPI#353: imprimir el informe de cualquier remesa desde la pestaña del listado.
    Public Property ImprimirRemesaCommand As DelegateCommand
    Private Function CanImprimirRemesa() As Boolean
        Return remesaActual IsNot Nothing
    End Function
    Private Async Sub OnImprimirRemesa()
        Await ImprimirRemesaAsync(remesaActual.Numero)
    End Sub

    Private Async Sub OnCargarCandidatos()
        Await CargarCandidatosAsync()
    End Sub

    ' Marcar todos respeta las retenciones del servidor: solo marca los preseleccionados (los
    ' grises retenidos por el gating se pueden marcar uno a uno, como decisión consciente).
    ' Desmarcar todos desmarca TODO, para empezar de cero y elegir unos pocos.
    Private Sub OnMarcarTodos()
        If ListaCandidatos Is Nothing Then
            Return
        End If
        For Each candidato In ListaCandidatos.Where(Function(c) c.Preseleccionado)
            candidato.Seleccionado = True
        Next
    End Sub
    Private Sub OnDesmarcarTodos()
        If ListaCandidatos Is Nothing Then
            Return
        End If
        For Each candidato In ListaCandidatos
            candidato.Seleccionado = False
        Next
    End Sub
    Private Function CanCrearRemesa() As Boolean
        Return NumeroEfectosSeleccionados > 0 AndAlso Not String.IsNullOrWhiteSpace(BancoRemesa)
    End Function
    Private Async Sub OnCrearRemesa()
        Await CrearRemesaAsync()
    End Sub

    Private Property _impagadoActual As impagado
    Public Property impagadoActual As impagado
        Get
            Return _impagadoActual
        End Get
        Set(value As impagado)
            _impagadoActual = value
            If IsNothing(impagadoActual) Then
                listaImpagadosDetalle = Nothing
            Else
                ' Nesto#340 Fase 1C.14 slice 5: el detalle se lee del API (fire-and-forget;
                ' el error queda en mensajeError).
                CargarMovimientosImpagadoAsync(impagadoActual.asiento)
            End If
            RaisePropertyChanged("impagadoActual")
        End Set
    End Property

    Private Property _usuarioTareas As String
    Public Property usuarioTareas As String
        Get
            Return _usuarioTareas
        End Get
        Set(value As String)
            SetProperty(_usuarioTareas, value)
        End Set
    End Property

    Private Property _listaTiposRemesa As ObservableCollection(Of tipoRemesa)
    Public Property listaTiposRemesa As ObservableCollection(Of tipoRemesa)
        Get
            Return _listaTiposRemesa
        End Get
        Set(value As ObservableCollection(Of tipoRemesa))
            _listaTiposRemesa = value
            'RaisePropertyChanged("listaTiposRemesa")
        End Set
    End Property

    Private Property _tipoRemesaActual As tipoRemesa
    Public Property tipoRemesaActual As tipoRemesa
        Get
            Return _tipoRemesaActual
        End Get
        Set(value As tipoRemesa)
            _tipoRemesaActual = value
            RaisePropertyChanged("tipoRemesaActual")
        End Set
    End Property

    Private Property _fechaCobro As Date
    Public Property fechaCobro As Date
        Get
            Return _fechaCobro
        End Get
        Set(value As Date)
            _fechaCobro = value
            RaisePropertyChanged("fechaCobro")
        End Set
    End Property

    Private _estaOcupado As Boolean = False
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(ByVal value As Boolean)
            _estaOcupado = value
            RaisePropertyChanged("estaOcupado")
        End Set
    End Property

#End Region

#Region "Comandos"

    Private _cmdCrearFicheroRemesa As ICommand
    Public ReadOnly Property cmdCrearFicheroRemesa() As ICommand
        Get
            If _cmdCrearFicheroRemesa Is Nothing Then
                _cmdCrearFicheroRemesa = New RelayCommand(AddressOf CrearFicheroRemesa, AddressOf CanCrearFicheroRemesa)
            End If
            Return _cmdCrearFicheroRemesa
        End Get
    End Property
    Private Function CanCrearFicheroRemesa(ByVal param As Object) As Boolean
        Return Not remesaActual Is Nothing
    End Function
    Private Async Sub CrearFicheroRemesa(ByVal param As Object)
        Dim codigo As String
        codigo = tipoRemesaActual.id
        Dim nombreFichero As String = Await configuracion.leerParametro(empresaActual, Parametros.Claves.PathNorma19) + CStr(remesaActual.Numero) + ".xml"
        Try
            mensajeError = "Generando fichero..."
            estaOcupado = True
            ' Nesto#340 Fase 1C.14 slice 6: el XML SEPA lo genera el servidor (único call
            ' site del SP prdCrearRemesaIso20022); aquí solo se guarda y se copia al portapapeles.
            contenidoFichero = Await GenerarContenidoFicheroRemesa(codigo)
            contenidoFichero.Save(nombreFichero)
            Dim listaClipboard As StringCollection
            If Clipboard.ContainsFileDropList Then
                listaClipboard = Clipboard.GetFileDropList()
            Else
                listaClipboard = New StringCollection
            End If

            listaClipboard.Add(nombreFichero)
            Clipboard.SetFileDropList(listaClipboard)
            mensajeError = "Fichero " + nombreFichero + " creado correctamente (copiado al portapapeles)"
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        Finally
            estaOcupado = False
        End Try
    End Sub

    Private _cmdLeerFicheroImpagado As ICommand
    Public ReadOnly Property cmdLeerFicheroImpagado() As ICommand
        Get
            If _cmdLeerFicheroImpagado Is Nothing Then
                _cmdLeerFicheroImpagado = New RelayCommand(AddressOf LeerFicheroImpagado, AddressOf CanLeerFicheroImpagado)
            End If
            Return _cmdLeerFicheroImpagado
        End Get
    End Property
    Private Function CanLeerFicheroImpagado(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Async Sub LeerFicheroImpagado(ByVal param As Object)
        Dim elegirFichero = New OpenFileDialog
        elegirFichero.Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*"
        elegirFichero.FilterIndex = 1
        elegirFichero.RestoreDirectory = True
        elegirFichero.InitialDirectory = Await configuracion.leerParametro(empresaActual, "PathDefectoImpagados")

        If elegirFichero.ShowDialog() Then
            Try
                contenidoFichero = XDocument.Load(elegirFichero.FileName)
                estaOcupado = True
                mensajeError = "Contabilizando..."
                ' Nesto#340 Fase 1C.14 slice 7: contabiliza el servidor (único call site del
                ' SP prdContabilizarImpagadosSepa).
                Await _remesasService.ContabilizarImpagados(contenidoFichero.ToString)
                mensajeError = "Impagados contabilizados correctamente"
            Catch ex As Exception
                If IsNothing(ex.InnerException) Then
                    mensajeError = ex.Message
                Else
                    mensajeError = ex.InnerException.Message
                End If
            Finally
                estaOcupado = False
            End Try
        End If

        ' Nesto#340 Fase 1C.14 slice 4: recarga por API tras contabilizar (selecciona el
        ' primer asiento dentro; antes usaba .First, que petaba con la lista vacía).
        Await CargarImpagadosAsync(numRemesas)

    End Sub

    Private _cmdVerTodasLasRemesas As ICommand
    Public ReadOnly Property cmdVerTodasLasRemesas() As ICommand
        Get
            If _cmdVerTodasLasRemesas Is Nothing Then
                _cmdVerTodasLasRemesas = New RelayCommand(AddressOf VerTodasLasRemesas, AddressOf CanVerTodasLasRemesas)
            End If
            Return _cmdVerTodasLasRemesas
        End Get
    End Property
    Private Function CanVerTodasLasRemesas(ByVal param As Object) As Boolean
        Return blnPuedeVerTodasLasRemesas
    End Function
    Private Sub VerTodasLasRemesas(ByVal param As Object)
        ' Nesto#340 Fase 1C.14 slice 2: sin top = todas las remesas, por API.
        CargarRemesasAsync(Nothing)
        blnPuedeVerTodasLasRemesas = False
    End Sub

    ' Nesto#340 Fase 1C.14 slice 8: eliminado el comando muerto cmdCrearTareasOutlook (llevaba
    ' años comentado; su sustituto es CrearTareasPlannerCommand) y su botón inerte del XAML.

    Public Property CrearTareasPlannerCommand As DelegateCommand
    Private Function CanCrearTareasPlanner() As Boolean
        Return True
    End Function
    Private Async Sub OnCrearTareasPlanner()
        Const MAX_CHECKLIST_ITEMS As Integer = 20
        Dim p As New DialogParameters
        Dim continuar As Boolean = False
        p.Add("message", "¿Desea crear las tareas en Planner?")
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ButtonResult.OK Then
                                                                  continuar = True
                                                              End If
                                                          End Sub)

        If Not continuar Then
            Return
        End If
        Dim planId = Constantes.Planner.GestionCobro.PLAN_ID
        Dim bucketId = Constantes.Planner.GestionCobro.BUCKET_PENDIENTES
        Dim scopes = {"User.Read.All", "Group.ReadWrite.All"}
        Dim graphClient As New GraphServiceClient(InteractiveBrowserCredential, scopes)

        Dim cts As New System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(2))
        Dim users As Microsoft.Graph.IGraphServiceUsersCollectionPage
        Try
            users = Await graphClient.Users.Request().GetAsync(cts.Token)
        Catch ex As Exception When TypeOf ex Is OperationCanceledException OrElse
                                    TypeOf ex Is Azure.Identity.AuthenticationFailedException
            mensajeError = "No se han concedido los permisos de Office o se ha agotado el tiempo de espera"
            Return
        End Try

        Dim usuarios As String() = usuarioTareas.Split(New Char() {";"c})
        Dim usuariosAsignar As New List(Of String)
        For Each usuario In usuarios
            Dim usuarioAsignar = users.FirstOrDefault(Function(c) c.Mail = usuario.Trim).Id
            If Not String.IsNullOrEmpty(usuarioAsignar) Then
                usuariosAsignar.Add(usuarioAsignar)
            End If
        Next

        Dim tareasBucket = Await graphClient.Planner.Buckets(bucketId).Tasks.Request().GetAsync(cts.Token)

        ' Nesto#340 Fase 1C.14 slice 8: los datos (efectos + cliente) vienen del API, no de EF.
        Dim impagados As List(Of TareaImpagadoModel)
        Try
            impagados = Await _remesasService.LeerTareasImpagado(empresaActual, impagadoActual.asiento)
        Catch ex As Exception
            mensajeError = ex.Message
            Return
        End Try

        Dim plannerTask As PlannerTask

        Try
            For Each impagado In impagados
                Dim asignadas = New PlannerAssignments
                For Each usuario In usuariosAsignar
                    asignadas.AddAssignee(usuario)
                Next
                asignadas.ODataType = Nothing

                Dim tituloTarea = String.Format("Impagados cliente {0}", impagado.Cliente)

                Dim detallesAntiguos As PlannerTaskDetails = Nothing

                If tareasBucket.Any(Function(t) t.Title = tituloTarea) Then
                    plannerTask = tareasBucket.First(Function(t) t.Title = tituloTarea)
                    detallesAntiguos = Await graphClient.Planner.Tasks(plannerTask.Id).Details.Request().GetAsync()
                    If plannerTask.PercentComplete = 100 Then
                        Dim nuevaTask As New PlannerTask With {
                            .PercentComplete = 50, ' en curso
                            .DueDateTime = DateTime.Today
                        }
                        Await graphClient.Planner.Tasks(plannerTask.Id).Request().Header("Prefer", "return=representation").Header("If-Match", plannerTask.GetEtag).UpdateAsync(nuevaTask)
                    Else
                        Dim nuevaTask As New PlannerTask With {
                            .DueDateTime = DateTime.Today
                        }
                        Await graphClient.Planner.Tasks(plannerTask.Id).Request().Header("Prefer", "return=representation").Header("If-Match", plannerTask.GetEtag).UpdateAsync(nuevaTask)
                    End If
                Else
                    plannerTask = New PlannerTask With
                    {
                        .PlanId = planId,
                        .BucketId = bucketId,
                        .Title = tituloTarea,
                        .Assignments = asignadas
                    }
                End If

                Dim elementoCheckList As String = String.Format("Fecha {0}, importe {1} (más gastos). {2}.",
                        impagado.Fecha.ToShortDateString, FormatCurrency(impagado.Importe), impagado.Concepto)
                Dim detalles As PlannerTaskDetails = New PlannerTaskDetails()
                detalles.Checklist = New PlannerChecklistItems()
                detalles.Checklist.AddChecklistItem(Left(elementoCheckList, 100))
                detalles.Checklist.ODataType = Nothing

                'If detallesAntiguos.Checklist?.Count >= MAX_CHECKLIST_ITEMS Then
                '    Dim elementoEliminar = detallesAntiguos.Checklist.Where(Function(c) c.Value.IsChecked).OrderBy(Function(c) c.Value.LastModifiedDateTime).First
                '    detallesAntiguos.Checklist.AdditionalData.Remove(elementoEliminar.Key)
                'End If

                If IsNothing(detallesAntiguos) Then
                    Dim descripcion As String = String.Format("Llamar al cliente {0}/{1}. Vendedor: {2}. {3} en  {4}. Ruta: {5}. Empresa: {6}.",
                            impagado.Cliente, impagado.Contacto, impagado.Vendedor, impagado.NombreCliente, impagado.Direccion, impagado.Ruta, impagado.NombreEmpresa)
                    detalles.Description = descripcion
                    detalles.PreviewType = PlannerPreviewType.Checklist
                    plannerTask.Details = detalles
                End If

                If String.IsNullOrEmpty(plannerTask.Id) Then
                    plannerTask = Await graphClient.Planner.Tasks.Request().AddAsync(plannerTask)
                    tareasBucket.Add(plannerTask)
                Else
                    Await graphClient.Planner.Tasks(plannerTask.Id).Details.Request().Header("If-Match", detallesAntiguos.GetEtag).UpdateAsync(detalles)
                End If
            Next
            mensajeError = "Tareas del asiento " + CStr(impagadoActual.asiento) + " creadas correctamente"
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try

    End Sub

#End Region

#Region "Funciones Auxiliares"
    ' Nesto#340 Fase 1C.14 slice 6: pide el XML SEPA al servidor y lo parsea (si el contenido
    ' no es XML válido, lanza y el comando muestra el error). Separado del comando para poder
    ' testearlo sin fichero ni portapapeles.
    Public Async Function GenerarContenidoFicheroRemesa(codigo As String) As Task(Of XDocument)
        Dim contenido As String = Await _remesasService.CrearFicheroRemesa(remesaActual.Numero, codigo, fechaCobro)
        Return XDocument.Parse(contenido)
    End Function
#End Region

End Class

Public Class impagado
    Public Sub New()

    End Sub

    Private Property _asiento As Integer
    Public Property asiento As Integer
        Get
            Return _asiento
        End Get
        Set(value As Integer)
            _asiento = value
        End Set
    End Property

    Private Property _fecha As Date
    Public Property fecha As Date
        Get
            Return _fecha
        End Get
        Set(value As Date)
            _fecha = value
        End Set
    End Property

    Private Property _cuenta As Integer
    Public Property cuenta As Integer
        Get
            Return _cuenta
        End Get
        Set(value As Integer)
            _cuenta = value
        End Set
    End Property

End Class

