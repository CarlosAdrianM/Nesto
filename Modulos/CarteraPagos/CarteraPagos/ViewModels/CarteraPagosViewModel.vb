Imports System.Collections.Specialized
Imports Prism.Commands
Imports Prism.Regions
Imports Nesto.Contratos
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs
Imports Prism.Mvvm

Public Class CarteraPagosViewModel
    Inherits BindableBase

    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly servicio As ICarteraPagosService
    Private ReadOnly dialogService As IDialogService

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As ICarteraPagosService, dialogService As IDialogService)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.dialogService = dialogService

        cmdAbrirCarteraPagos = New DelegateCommand(Of Object)(AddressOf OnAbrirCarteraPagos, AddressOf CanAbrirCarteraPagos)
        cmdCrearFicheroRemesa = New DelegateCommand(Of Object)(AddressOf OnCrearFicheroRemesa, AddressOf CanCrearFicheroRemesa)

        Titulo = "Remesa de Pagos"

    End Sub

#Region "Propiedades de Nesto"
    Private _banco As String = 5
    Public Property banco As String
        Get
            Return _banco
        End Get
        Set(ByVal value As String)
            SetProperty(_banco, value)
            cmdCrearFicheroRemesa.RaiseCanExecuteChanged()
        End Set
    End Property


    Private _numeroOrdenExtracto As Integer
    Public Property numeroOrdenExtracto As Integer
        Get
            Return _numeroOrdenExtracto
        End Get
        Set(ByVal value As Integer)
            SetProperty(_numeroOrdenExtracto, value)
            numeroRemesa = 0
            RaisePropertyChanged(NameOf(numeroRemesa))
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

    Public Property Titulo As String

    Private Function CanCrearFicheroRemesa(arg As Object) As Boolean
        Return numeroRemesa <> 0 OrElse
            (numeroRemesa = 0 AndAlso Not IsNothing(banco) AndAlso banco <> String.Empty AndAlso Not IsNothing(numeroOrdenExtracto) AndAlso numeroOrdenExtracto <> 0)
    End Function
    Private Async Sub OnCrearFicheroRemesa(arg As Object)
        Dim respuesta As String = ""
        Try
            If (numeroRemesa <> 0) Then
                respuesta = Await servicio.CrearFichero(numeroRemesa)
            Else
                respuesta = Await servicio.CrearFichero(numeroOrdenExtracto, banco)
            End If

            If respuesta = "" Then
                dialogService.ShowError("No se ha podido crear el fichero")
            End If

        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        End Try

        If respuesta <> "" Then
            respuesta = respuesta.Replace("\\", "\")
            respuesta = respuesta.Replace("""", "")
            Dim listaClipboard As StringCollection
            If Clipboard.ContainsFileDropList Then
                listaClipboard = Clipboard.GetFileDropList()
            Else
                listaClipboard = New StringCollection
            End If

            listaClipboard.Add(respuesta)
            Clipboard.SetFileDropList(listaClipboard)
            dialogService.ShowNotification("Fichero Creado", "Se ha creado correctamente el fichero: " + vbCrLf + respuesta)
        End If
    End Sub

#End Region

End Class
