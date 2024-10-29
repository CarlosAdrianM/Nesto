Imports System.Collections.ObjectModel
Imports Prism.Commands
Imports Prism.Regions
Imports Nesto.Modulos.Rapports.RapportsModel
Imports Prism.Mvvm
Imports Unity
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs
Imports Prism.Events
Imports Nesto.Infrastructure.Events
Imports Prism

Public Class ListaRapportsViewModel
    Inherits ViewModelBase
    Implements INavigationAware, IActiveAware

    Private ReadOnly regionManager As IRegionManager
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IRapportService
    Private ReadOnly container As IUnityContainer
    Private ReadOnly _dialogService As IDialogService
    Private ReadOnly _eventAggregator As IEventAggregator
    Private _subscriptionToken As SubscriptionToken
    Private _empresaPorDefecto As String = Constantes.Empresas.EMPRESA_DEFECTO

    Private _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
        End Set
    End Property

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IRapportService, container As IUnityContainer, dialogService As IDialogService, eventAggregator As IEventAggregator)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.container = container
        _dialogService = dialogService
        _eventAggregator = eventAggregator

        cmdAbrirModulo = New DelegateCommand(Of Object)(AddressOf OnAbrirModulo, AddressOf CanAbrirModulo)
        cmdCargarListaRapports = New DelegateCommand(Of Object)(AddressOf OnCargarListaRapports, AddressOf CanCargarListaRapports)
        cmdCargarListaRapportsFiltrada = New DelegateCommand(AddressOf OnCargarListaRapportsFiltrada, AddressOf CanCargarListaRapportsFiltrada)
        cmdCrearRapport = New DelegateCommand(Of ClienteProbabilidadVenta)(AddressOf OnCrearRapport, AddressOf CanCrearRapport)

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
            If _clienteSeleccionado = String.Empty Then
                cmdCargarListaRapports.Execute(Nothing) ' Por fecha
            End If
        End Set
    End Property

    Private _clienteProbabilidadSeleccionado As ClienteProbabilidadVenta
    Public Property ClienteProbabilidadSeleccionado As ClienteProbabilidadVenta
        Get
            Return _clienteProbabilidadSeleccionado
        End Get
        Set(value As ClienteProbabilidadVenta)
            SetProperty(_clienteProbabilidadSeleccionado, value)
            If Not IsNothing(value) Then
                cmdCrearRapport.Execute(value)
            End If
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

    Private _esUsuarioElVendedor As Boolean = True
    Public Property esUsuarioElVendedor As Boolean
        Get
            Return _esUsuarioElVendedor
        End Get
        Set(value As Boolean)
            SetProperty(_esUsuarioElVendedor, value)
        End Set
    End Property

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

    ' Implementación de IActiveAware
    Private _isActive As Boolean
    Public Property IsActive As Boolean Implements IActiveAware.IsActive
        Get
            Return _isActive
        End Get
        Set(value As Boolean)
            If _isActive <> value Then
                _isActive = value
                RaiseEvent IsActiveChanged(Me, EventArgs.Empty)

                If _isActive Then
                    OnLoaded()
                Else
                    OnUnloaded()
                End If
            End If
        End Set
    End Property

    Private _isLoadingClientesProbabilidad As Boolean
    Public Property IsLoadingClientesProbabilidad As Boolean
        Get
            Return _isLoadingClientesProbabilidad
        End Get
        Set(value As Boolean)
            SetProperty(_isLoadingClientesProbabilidad, value)
        End Set
    End Property

    Private _listaClientesProbabilidad As ObservableCollection(Of ClienteProbabilidadVenta)
    Public Property ListaClientesProbabilidad As ObservableCollection(Of ClienteProbabilidadVenta)
        Get
            Return _listaClientesProbabilidad
        End Get
        Set(value As ObservableCollection(Of ClienteProbabilidadVenta))
            SetProperty(_listaClientesProbabilidad, value)
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
            vendedor = Await configuracion.leerParametro(_empresaPorDefecto, "Vendedor")
        End If
        If Not IsNothing(clienteSeleccionado) Then
            listaRapports = Await servicio.cargarListaRapports(_empresaPorDefecto, clienteSeleccionado, contactoSeleccionado)
        Else
            Dim parametroVendedor As String
            parametroVendedor = IIf(esUsuarioElVendedor, configuracion.usuario, vendedor)
            listaRapports = Await servicio.cargarListaRapports(parametroVendedor, fechaSeleccionada)
        End If
        rapportSeleccionado = listaRapports.FirstOrDefault
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
        Dim todosLosClientes As String = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.PermitirVerClientesTodosLosVendedores)
        Try
            EstaOcupado = True
            If todosLosClientes = "1" Then
                listaRapports = Await servicio.cargarListaRapportsFiltrada(String.Empty, Filtro)
            Else
                listaRapports = Await servicio.cargarListaRapportsFiltrada(vendedor, Filtro)
            End If

            rapportSeleccionado = listaRapports.FirstOrDefault
        Catch ex As Exception
            _dialogService.ShowError(ex.Message)
        Finally
            EstaOcupado = False
        End Try

    End Sub



    Private _cmdCrearRapport As DelegateCommand(Of ClienteProbabilidadVenta)
    Public Event IsActiveChanged As EventHandler Implements IActiveAware.IsActiveChanged

    Public Property cmdCrearRapport As DelegateCommand(Of ClienteProbabilidadVenta)
        Get
            Return _cmdCrearRapport
        End Get
        Private Set(value As DelegateCommand(Of ClienteProbabilidadVenta))
            SetProperty(_cmdCrearRapport, value)
        End Set
    End Property

    Private Function CanCrearRapport(cliente As ClienteProbabilidadVenta) As Boolean
        Return True
    End Function
    Private Async Sub OnCrearRapport(cliente As ClienteProbabilidadVenta)

        If Not IsNothing(cliente) Then
            clienteSeleccionado = cliente.cliente
            contactoSeleccionado = cliente.contacto
            Await Task.Run(Sub() cmdCargarListaRapports.Execute(Nothing))
        End If


        Dim rapportNuevo As New SeguimientoClienteDTO With {
            .Empresa = _empresaPorDefecto,
            .Estado = SeguimientoClienteDTO.EstadoSeguimientoDTO.Vigente,
            .Fecha = IIf(fechaSeleccionada >= Today, Now, fechaSeleccionada),
            .Tipo = SeguimientoClienteDTO.TipoSeguimientoDTO.TELEFONO,
            .TipoCentro = SeguimientoClienteDTO.TiposCentro.NoSeSabe,
            .Vendedor = vendedor,
            .Usuario = configuracion.usuario
        }

        If Not IsNothing(cliente) Then
            rapportNuevo.Cliente = cliente.cliente
            rapportNuevo.Contacto = cliente.contacto
        End If

        'no entiendo por qué es necesaria esta línea
        'pero si la quitamos solo crea bien un rapport de cada dos
        rapportSeleccionado = Nothing

        rapportSeleccionado = rapportNuevo
        If IsNothing(listaRapports) Then
            listaRapports = New ObservableCollection(Of SeguimientoClienteDTO)
        End If
        listaRapports.Add(rapportNuevo)
    End Sub


    Private Async Sub ActualizarClientesProbabilidad()
        IsLoadingClientesProbabilidad = True
        Try
            ListaClientesProbabilidad = New ObservableCollection(Of ClienteProbabilidadVenta)(Await servicio.CargarClientesProbabilidad(vendedor))
        Finally
            IsLoadingClientesProbabilidad = False
        End Try
    End Sub

    Private Sub OnLoaded()
        ' Suscríbete solo si no hay una suscripción activa
        If _subscriptionToken Is Nothing Then
            _subscriptionToken = _eventAggregator.GetEvent(Of RapportGuardadoEvent).Subscribe(AddressOf ActualizarClientesProbabilidad)
        End If
    End Sub

    Private Sub OnUnloaded()
        ' Desuscribirse del evento si hay una suscripción activa
        If _subscriptionToken IsNot Nothing Then
            _eventAggregator.GetEvent(Of RapportGuardadoEvent).Unsubscribe(_subscriptionToken)
            _subscriptionToken = Nothing
        End If
    End Sub


    Public Overrides Async Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo
        If IsNothing(vendedor) Then
            vendedor = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.Vendedor)
        End If
        ActualizarClientesProbabilidad()
    End Sub

    Public Overrides Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean Implements INavigationAware.IsNavigationTarget
        Return True
    End Function

    Public Overloads Sub OnNavigatedFrom(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedFrom

    End Sub

#End Region


End Class
