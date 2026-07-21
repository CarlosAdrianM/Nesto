Imports System.Windows.Input
Imports System.ComponentModel
Imports System.Collections.ObjectModel
Imports System.Windows
Imports Microsoft.Win32
Imports Microsoft.Office.Interop
Imports System.Windows.Controls
Imports Nesto.Contratos
Imports Nesto.Models.Nesto.Models
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

    ' Nesto#340: ya no es Shared. El Shared hacía que el DbContext fuera un singleton
    ' para toda la vida del proceso, con tres problemas: (1) ChangeTracker acumula
    ' entidades indefinidamente (memory leak); (2) los datos quedan stale frente a
    ' cambios externos a este cliente; (3) no es thread-safe. Pasar a instancia limita
    ' esos riesgos a la vida del ViewModel. Eliminar EF de este VM por completo queda
    ' como paso pendiente del roadmap.
    Private DbContext As NestoEntities
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
        DbContext = New NestoEntities
        ' Nesto#340 Fase 1C.14 slice 1: las empresas se leen del API (async); el resto sigue en EF.
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
        CargarCandidatosCommand = New DelegateCommand(AddressOf OnCargarCandidatos)
        CrearRemesaCommand = New DelegateCommand(AddressOf OnCrearRemesa, AddressOf CanCrearRemesa)
        MarcarTodosCommand = New DelegateCommand(AddressOf OnMarcarTodos)
        DesmarcarTodosCommand = New DelegateCommand(AddressOf OnDesmarcarTodos)
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
    Public Async Function CargarCandidatosAsync() As Task
        Try
            estaOcupado = True
            Dim candidatos = Await _remesasService.LeerEfectosCandidatos(empresaActual)
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
        Dim confirmado As Boolean = False
        dialogService.ShowConfirmation("Crear remesa",
            $"¿Crear la remesa con {seleccionados.Count} efectos por un total de {importe:C} al banco {BancoRemesa}?",
            Sub(r) confirmado = r.Result = Prism.Services.Dialogs.ButtonResult.OK)
        If Not confirmado Then
            Return
        End If

        Try
            estaOcupado = True
            Dim resultado = Await _remesasService.CrearRemesa(empresaActual, BancoRemesa,
                seleccionados.Select(Function(c) c.Id).ToList())
            mensajeError = $"Remesa {resultado.NumeroRemesa} creada: {resultado.NumeroEfectos} efectos, {resultado.Importe:C}"
            ' Refrescar: la remesa nueva aparece en la lista y los efectos salen de candidatos
            Await CargarRemesasAsync(numRemesas)
            Await CargarCandidatosAsync()
        Catch ex As Exception
            mensajeError = ex.Message
        Finally
            estaOcupado = False
        End Try
    End Function

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

    Public Property CargarCandidatosCommand As DelegateCommand
    Public Property CrearRemesaCommand As DelegateCommand
    Public Property MarcarTodosCommand As DelegateCommand
    Public Property DesmarcarTodosCommand As DelegateCommand

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
        Dim strContenido As String = String.Empty
        Dim listaContenido As List(Of String)
        Dim codigo As String
        codigo = tipoRemesaActual.id
        Dim nombreFichero As String = Await configuracion.leerParametro(empresaActual, Parametros.Claves.PathNorma19) + CStr(remesaActual.Numero) + ".xml"
        'Dim nombreFichero As String = "c:\banco\prueba.xml"
        Try
            mensajeError = "Generando fichero..."
            DbContext.Database.CommandTimeout = 6000

            estaOcupado = True
            Await Task.Run(Sub()
                               listaContenido = crearFicheroRemesa(remesaActual.Numero, codigo, fechaCobro)
                               DbContext.Database.CommandTimeout = 180
                               For Each linea In listaContenido
                                   strContenido += linea
                               Next
                           End Sub)
            contenidoFichero = XDocument.Parse(strContenido)
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
                Await Task.Run(Sub()
                                   contabilizarImpagados(contenidoFichero.ToString)
                                   mensajeError = "Impagados contabilizados correctamente"
                               End Sub)
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

    'Private _cmdCrearTareasOutlook As ICommand
    'Public ReadOnly Property cmdCrearTareasOutlook() As ICommand
    '    Get
    '        If _cmdCrearTareasOutlook Is Nothing Then
    '            _cmdCrearTareasOutlook = New RelayCommand(AddressOf CrearTareasOutlook, AddressOf CanCrearTareasOutlook)
    '        End If
    '        Return _cmdCrearTareasOutlook
    '    End Get
    'End Property
    'Private Function CanCrearTareasOutlook(ByVal param As Object) As Boolean
    '    Return Not impagadoActual Is Nothing
    'End Function
    'Private Sub CrearTareasOutlook(ByVal param As Object)
    '    Dim objOL As Outlook.Application
    '    objOL = New Outlook.Application
    '    Dim newTask As Outlook.TaskItem
    '    Dim ruta As New Rutas
    '    Dim impagados = From e In DbContext.ExtractoCliente Join c In DbContext.Clientes On e.Empresa Equals c.Empresa And e.Número Equals c.Nº_Cliente And e.Contacto Equals c.Contacto Where e.Empresa = empresaActual And e.Asiento = impagadoActual.asiento And Not e.Concepto.StartsWith("Gastos Impagado ")

    '    Try
    '        For Each impagado In impagados
    '            newTask = objOL.CreateItem(Outlook.OlItemType.olTaskItem)
    '            If Not IsNothing(newTask) Then
    '                ruta = (From r In DbContext.Rutas Where r.Empresa = impagado.c.Empresa And r.Número = impagado.c.Ruta).FirstOrDefault
    '                newTask.Subject = ruta.Descripción.Trim + " - Llamar al cliente " + impagado.e.Número.Trim + "/" + impagado.e.Contacto.Trim +
    '                    ". Vendedor: " + impagado.c.Vendedor.Trim + ". " + impagado.c.Nombre.Trim + " en " + impagado.c.Dirección.Trim
    '                newTask.Body = "Ha llegado un impagado de este cliente, con fecha " + impagado.e.Fecha.ToShortDateString + " e importe de " + FormatCurrency(impagado.e.Importe) + " (más gastos)." + vbCrLf +
    '                    "Motivo: " + impagado.e.Concepto + vbCrLf +
    '                    "Ruta: " + impagado.c.Ruta + vbCrLf +
    '                    "Empresa: " + impagado.c.Empresas.Nombre.Trim
    '                newTask.Assign()
    '                'If impagado.c.Ruta.Trim = "00" Or impagado.c.Ruta.Trim = "02" Or impagado.c.Ruta.Trim = "03" Then
    '                '    usuarioTareas = "laura@nuevavision.es"
    '                'Else
    '                '    usuarioTareas = "aidarubio@nuevavision.es"
    '                'End If
    '                newTask.Recipients.Add(usuarioTareas)
    '                newTask.Recipients.ResolveAll()
    '                newTask.Send()
    '            End If
    '        Next
    '        mensajeError = "Tareas del asiento " + CStr(impagadoActual.asiento) + " creadas correctamente"
    '    Catch ex As Exception
    '        If IsNothing(ex.InnerException) Then
    '            mensajeError = ex.Message
    '        Else
    '            mensajeError = ex.InnerException.Message
    '        End If
    '    End Try
    'End Sub

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

        Dim impagados = From e In DbContext.ExtractoCliente Join c In DbContext.Clientes On e.Empresa Equals c.Empresa And e.Número Equals c.Nº_Cliente And e.Contacto Equals c.Contacto Where e.Empresa = empresaActual And e.Asiento = impagadoActual.asiento And Not e.Concepto.StartsWith("Gastos Impagado ")

        Dim plannerTask As PlannerTask

        Try
            For Each impagado In impagados
                Dim asignadas = New PlannerAssignments
                For Each usuario In usuariosAsignar
                    asignadas.AddAssignee(usuario)
                Next
                asignadas.ODataType = Nothing

                Dim tituloTarea = String.Format("Impagados cliente {0}", impagado.e.Número.Trim)

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
                        impagado.e.Fecha.ToShortDateString, FormatCurrency(impagado.e.Importe), impagado.e.Concepto.Trim)
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
                            impagado.e.Número.Trim(), impagado.e.Contacto.Trim, impagado.c.Vendedor.Trim, impagado.c.Nombre.Trim, impagado.c.Dirección.Trim, impagado.c.Ruta, impagado.c.Empresas.Nombre.Trim)
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
    Private Sub contabilizarImpagados(contenidoFichero As String)
        DbContext.Database.CommandTimeout = 6000
        DbContext.prdContabilizarImpagadosSepa(contenidoFichero)
    End Sub

    Private Function crearFicheroRemesa(remesa As Integer, codigo As String, fechaCobro As Date) As List(Of String)
        Return DbContext.CrearFicheroRemesa(remesa, codigo, fechaCobro).ToList
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

