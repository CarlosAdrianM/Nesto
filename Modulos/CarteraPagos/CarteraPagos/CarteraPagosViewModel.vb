Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.Regions
Imports Nesto.Contratos

Public Class CarteraPagosViewModel
    Inherits ViewModelBase

    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly servicio As ICarteraPagosService

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As ICarteraPagosService)
        'Me.container = container
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio

        cmdAbrirCarteraPagos = New DelegateCommand(Of Object)(AddressOf OnAbrirCarteraPagos, AddressOf CanAbrirCarteraPagos)
        cmdCrearFicheroRemesa = New DelegateCommand(Of Object)(AddressOf OnCrearFicheroRemesa, AddressOf CanCrearFicheroRemesa)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        Titulo = "Remesa de Pagos"

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

#Region "Propiedades de Nesto"
    Private _empresa As String
    Public Property empresa As String
        Get
            Return _empresa
        End Get
        Set(ByVal value As String)
            SetProperty(_empresa, value)
            cmdCrearFicheroRemesa.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _numeroRemesa As Integer
    Public Property numeroRemesa As Integer
        Get
            Return _numeroRemesa
        End Get
        Set(ByVal value As Integer)
            SetProperty(_numeroRemesa, value)
            cmdCrearFicheroRemesa.RaiseCanExecuteChanged()
        End Set
    End Property

#End Region

#Region "Comandos"

    Private _cmdAbrirCarteraPagos As DelegateCommand(Of Object)
    Public Property cmdAbrirCarteraPagos As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirCarteraPagos
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirCarteraPagos, value)
        End Set
    End Property
    Private Function CanAbrirCarteraPagos(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirCarteraPagos(arg As Object)
        regionManager.RequestNavigate("MainRegion", "CarteraPagosView")
    End Sub

    Private _cmdCrearFicheroRemesa As DelegateCommand(Of Object)
    Public Property cmdCrearFicheroRemesa As DelegateCommand(Of Object)
        Get
            Return _cmdCrearFicheroRemesa
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCrearFicheroRemesa, value)
        End Set
    End Property
    Private Function CanCrearFicheroRemesa(arg As Object) As Boolean
        Return numeroRemesa <> 0 AndAlso empresa.Trim <> ""
    End Function
    Private Async Sub OnCrearFicheroRemesa(arg As Object)
        Dim respuesta As String = Await servicio.crearFichero(empresa, numeroRemesa)

        If respuesta <> "" Then
            NotificationRequest.Raise(New Notification() With {
                .Title = "Fichero Creado",
                .Content = "Se ha creado correctamente el fichero " + respuesta
            })
        Else
            NotificationRequest.Raise(New Notification() With {
                .Title = "Error",
                .Content = "No se ha podido crear el fichero"
            })
        End If
    End Sub

#End Region

End Class
