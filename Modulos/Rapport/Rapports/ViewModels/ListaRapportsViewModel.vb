Imports System.Collections.ObjectModel
Imports Prism.Commands
Imports Prism.Regions
Imports Nesto.Contratos
Imports Nesto.Modulos.Rapports.RapportsModel
Imports Prism.Mvvm
Imports Unity
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Class ListaRapportsViewModel
    Inherits BindableBase
    Implements INavigationAware

    Private ReadOnly regionManager As IRegionManager
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IRapportService
    Private ReadOnly container As IUnityContainer


    Private _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
        End Set
    End Property

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IRapportService, container As IUnityContainer)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.container = container

        cmdAbrirModulo = New DelegateCommand(Of Object)(AddressOf OnAbrirModulo, AddressOf CanAbrirModulo)
        cmdCargarListaRapports = New DelegateCommand(Of Object)(AddressOf OnCargarListaRapports, AddressOf CanCargarListaRapports)
        cmdCargarListaRapportsFiltrada = New DelegateCommand(AddressOf OnCargarListaRapportsFiltrada, AddressOf CanCargarListaRapportsFiltrada)
        cmdCrearRapport = New DelegateCommand(Of Object)(AddressOf OnCrearRapport, AddressOf CanCrearRapport)

        listaTiposRapports = servicio.CargarListaTipos()
        listaEstadosRapport = servicio.CargarListaEstados()

        Titulo = "Lista de Rapports"
    End Sub

#Region "Propiedades"
    Private _clienteSeleccionado As String
    Public Property clienteSeleccionado As String
        Get
            Return _clienteSeleccionado
        End Get
        Set(value As String)
            SetProperty(_clienteSeleccionado, value)
            cmdCargarListaRapports.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _contactoSeleccionado As String
    Public Property contactoSeleccionado As String
        Get
            Return _contactoSeleccionado
        End Get
        Set(value As String)
            SetProperty(_contactoSeleccionado, value)
        End Set
    End Property

    Private _estaOcupado As Boolean
    Public Property EstaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(value As Boolean)
            SetProperty(_estaOcupado, value)
        End Set
    End Property

    Private empresaPorDefecto As String = "1"

    Private _fechaSeleccionada As Date = DateTime.Today
    Public Property fechaSeleccionada As Date
        Get
            Return _fechaSeleccionada
        End Get
        Set(value As Date)
            SetProperty(_fechaSeleccionada, value)
        End Set
    End Property

    Private _filtro As String
    Public Property Filtro As String
        Get
            Return _filtro
        End Get
        Set(value As String)
            SetProperty(_filtro, value)
            cmdCargarListaRapportsFiltrada.Execute()
        End Set
    End Property

    Private _listaEstadosRapport As List(Of idShortDescripcion)
    Public Property listaEstadosRapport As List(Of idShortDescripcion)
        Get
            Return _listaEstadosRapport
        End Get
        Set(value As List(Of idShortDescripcion))
            SetProperty(_listaEstadosRapport, value)
        End Set
    End Property

    Private _listaRapports As ObservableCollection(Of SeguimientoClienteDTO)
    Public Property listaRapports As ObservableCollection(Of SeguimientoClienteDTO)
        Get
            Return _listaRapports
        End Get
        Set(value As ObservableCollection(Of SeguimientoClienteDTO))
            SetProperty(_listaRapports, value)
        End Set
    End Property

    Private _listaTiposRapports As List(Of idDescripcion)
    Public Property listaTiposRapports As List(Of idDescripcion)
        Get
            Return _listaTiposRapports
        End Get
        Set(value As List(Of idDescripcion))
            SetProperty(_listaTiposRapports, value)
        End Set
    End Property

    Private _rapportSeleccionado As SeguimientoClienteDTO
    Public Property rapportSeleccionado As SeguimientoClienteDTO
        Get
            Return _rapportSeleccionado
        End Get
        Set(value As SeguimientoClienteDTO)
            SetProperty(_rapportSeleccionado, value)
            Dim parameters As NavigationParameters = New NavigationParameters()
            parameters.Add("rapportParameter", rapportSeleccionado)
            regionManager.RequestNavigate("RapportDetailRegion", "RapportView", parameters)
        End Set
    End Property

    Private _esUsuarioElVendedor As Boolean = True
    Public Property esUsuarioElVendedor As Boolean
        Get
            Return _esUsuarioElVendedor
        End Get
        Set(value As Boolean)
            SetProperty(_esUsuarioElVendedor, value)
        End Set
    End Property

#End Region

#Region "Comandos"
    Private _cmdAbrirModulo As DelegateCommand(Of Object)
    Public Property cmdAbrirModulo As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirModulo
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirModulo, value)
        End Set
    End Property
    Private Function CanAbrirModulo(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirModulo(arg As Object)
        regionManager.RequestNavigate("MainRegion", "ListaRapportsView")
    End Sub

    Private _cmdCargarListaRapports As DelegateCommand(Of Object)
    Public Property cmdCargarListaRapports As DelegateCommand(Of Object)
        Get
            Return _cmdCargarListaRapports
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarListaRapports, value)
        End Set
    End Property
    Private Function CanCargarListaRapports(arg As Object) As Boolean
        Return Not IsNothing(clienteSeleccionado) Or Not IsNothing(fechaSeleccionada)
    End Function
    Private Async Sub OnCargarListaRapports(arg As Object)
        If IsNothing(vendedor) Then
            vendedor = Await configuracion.leerParametro(empresaPorDefecto, "Vendedor")
        End If
        If Not IsNothing(clienteSeleccionado) Then
            listaRapports = Await servicio.cargarListaRapports(empresaPorDefecto, clienteSeleccionado, contactoSeleccionado)
        Else
            Dim parametroVendedor As String
            parametroVendedor = IIf(esUsuarioElVendedor, configuracion.usuario, vendedor)
            listaRapports = Await servicio.cargarListaRapports(parametroVendedor, fechaSeleccionada)
        End If
        rapportSeleccionado = listaRapports.LastOrDefault
    End Sub

    Private _cmdCargarListaRapportsFiltrada As DelegateCommand
    Public Property cmdCargarListaRapportsFiltrada As DelegateCommand
        Get
            Return _cmdCargarListaRapportsFiltrada
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCargarListaRapportsFiltrada, value)
        End Set
    End Property
    Private Function CanCargarListaRapportsFiltrada() As Boolean
        Return Not IsNothing(Filtro)
    End Function
    Private Async Sub OnCargarListaRapportsFiltrada()
        If IsNothing(vendedor) Then
            vendedor = Await configuracion.leerParametro(empresaPorDefecto, "Vendedor")
        End If
        Try
            EstaOcupado = True
            listaRapports = Await servicio.cargarListaRapportsFiltrada(vendedor, Filtro)
            rapportSeleccionado = listaRapports.LastOrDefault
        Catch ex As Exception
            Throw ex
        Finally
            EstaOcupado = False
        End Try

    End Sub

    Private _cmdCrearRapport As DelegateCommand(Of Object)
    Public Property cmdCrearRapport As DelegateCommand(Of Object)
        Get
            Return _cmdCrearRapport
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCrearRapport, value)
        End Set
    End Property

    Public ReadOnly Property Titulo As String

    Private Function CanCrearRapport(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnCrearRapport(arg As Object)
        Dim rapportNuevo As New SeguimientoClienteDTO With {
            .Empresa = empresaPorDefecto,
            .Estado = SeguimientoClienteDTO.EstadoSeguimientoDTO.Vigente,
            .Fecha = IIf(fechaSeleccionada >= Today, Now, fechaSeleccionada),
            .Tipo = SeguimientoClienteDTO.TipoSeguimientoDTO.TELEFONO,
            .TipoCentro = SeguimientoClienteDTO.TiposCentro.NoSeSabe,
            .Vendedor = vendedor,
            .Usuario = configuracion.usuario
        }

        'no entiendo por qué es necesaria esta línea
        'pero si la quitamos solo crea bien un rapport de cada dos
        rapportSeleccionado = Nothing

        rapportSeleccionado = rapportNuevo
        If IsNothing(listaRapports) Then
            listaRapports = New ObservableCollection(Of SeguimientoClienteDTO)
        End If
        listaRapports.Add(rapportNuevo)
    End Sub

    Public Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo

    End Sub

    Public Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean Implements INavigationAware.IsNavigationTarget
        Return True
    End Function

    Public Sub OnNavigatedFrom(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedFrom

    End Sub

#End Region


End Class
