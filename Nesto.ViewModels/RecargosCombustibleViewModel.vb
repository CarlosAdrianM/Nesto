Imports System.Collections.ObjectModel
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs

''' <summary>
''' Nesto#340: mantenimiento del recargo de combustible (fuel) por agencia. El usuario edita el
''' porcentaje mensual de cada agencia; se guarda vía NestoAPI (api/Agencias/RecargosCombustible).
''' </summary>
Public Class RecargosCombustibleViewModel
    Inherits BindableBase

    Private ReadOnly _servicio As IServicioRecargosCombustible
    Private ReadOnly _dialogService As IDialogService

    Public Sub New(servicio As IServicioRecargosCombustible, dialogService As IDialogService)
        _servicio = servicio
        _dialogService = dialogService
        GuardarCommand = New DelegateCommand(AddressOf OnGuardar, AddressOf CanGuardar)
        Dim unused = CargarAsync()
    End Sub

    Private _agencias As ObservableCollection(Of RecargoCombustibleAgencia)
    Public Property Agencias As ObservableCollection(Of RecargoCombustibleAgencia)
        Get
            Return _agencias
        End Get
        Set(value As ObservableCollection(Of RecargoCombustibleAgencia))
            Dim unused = SetProperty(_agencias, value)
            GuardarCommand?.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _estaOcupado As Boolean
    Public Property EstaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_estaOcupado, value)
        End Set
    End Property

    Public Property GuardarCommand As DelegateCommand

    Public Async Function CargarAsync() As Task
        Dim lista = Await _servicio.LeerRecargos()
        Agencias = New ObservableCollection(Of RecargoCombustibleAgencia)(lista)
    End Function

    Public Async Function GuardarAsync() As Task
        For Each agencia In Agencias
            Await _servicio.GuardarRecargo(agencia.Numero, agencia.RecargoCombustible)
        Next
    End Function

    Private Function CanGuardar() As Boolean
        Return Agencias IsNot Nothing AndAlso Agencias.Any()
    End Function

    Private Async Sub OnGuardar()
        Try
            EstaOcupado = True
            Await GuardarAsync()
            _dialogService.ShowNotification("Recargos de combustible guardados correctamente")
        Catch ex As Exception
            _dialogService.ShowError($"Error al guardar los recargos de combustible: {ex.Message}")
        Finally
            EstaOcupado = False
        End Try
    End Sub

End Class
