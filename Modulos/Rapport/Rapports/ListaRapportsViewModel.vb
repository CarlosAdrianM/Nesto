Imports System.Collections.ObjectModel
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Nesto.Contratos
Imports Nesto.Modulos.Rapports.RapportsModel
Imports Nesto.ViewModels

Public Class ListaRapportsViewModel
    Inherits Contratos.ViewModelBase

    Private ReadOnly regionManager As IRegionManager
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IRapportService
    Private _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
        End Set
    End Property

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IRapportService)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio

        cmdAbrirModulo = New DelegateCommand(Of Object)(AddressOf OnAbrirModulo, AddressOf CanAbrirModulo)
        cmdCargarListaRapports = New DelegateCommand(Of Object)(AddressOf OnCargarListaRapports, AddressOf CanCargarListaRapports)
        cmdCrearRapport = New DelegateCommand(Of Object)(AddressOf OnCrearRapport, AddressOf CanCrearRapport)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        Titulo = "Lista de Rapports"
    End Sub

#Region "Propiedades de Prism"
    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    Private _ConfirmationRequest As InteractionRequest(Of IConfirmation)
    Public Property ConfirmationRequest As InteractionRequest(Of IConfirmation)
        Get
            Return _ConfirmationRequest
        End Get
        Private Set(value As InteractionRequest(Of IConfirmation))
            _ConfirmationRequest = value
        End Set
    End Property

    Private resultMessage As String
    Public Property InteractionResultMessage As String
        Get
            Return Me.resultMessage
        End Get
        Set(value As String)
            Me.resultMessage = value
            Me.OnPropertyChanged("InteractionResultMessage")
        End Set
    End Property
#End Region

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

    Private _listaRapports As ObservableCollection(Of SeguimientoClienteDTO)
    Public Property listaRapports As ObservableCollection(Of SeguimientoClienteDTO)
        Get
            Return _listaRapports
        End Get
        Set(value As ObservableCollection(Of SeguimientoClienteDTO))
            SetProperty(_listaRapports, value)
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

        rapportSeleccionado = rapportNuevo

        listaRapports.Add(rapportNuevo)
    End Sub

#End Region


End Class
