Imports System.Collections.ObjectModel
Imports ControlesUsuario.Behaviors
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs

''' <summary>
''' Nesto#340: mantenimiento de agencias de transporte. El usuario crea agencias nuevas (p.ej.
''' Innovatrans), edita sus campos (incluido el % de fuel) y las pone/quita de cuarentena. Las
''' agencias se guardan vía NestoAPI (api/Agencias); la cuarentena en el parámetro
''' AgenciasEnCuarentena (lista de nombres separados por comas).
''' </summary>
Public Class AgenciasMantenimientoViewModel
    Inherits BindableBase

    Private ReadOnly _servicio As IServicioAgenciasMantenimiento
    Private ReadOnly _configuracion As IConfiguracion
    Private ReadOnly _dialogService As IDialogService

    Public Sub New(servicio As IServicioAgenciasMantenimiento, configuracion As IConfiguracion, dialogService As IDialogService)
        _servicio = servicio
        _configuracion = configuracion
        _dialogService = dialogService
        GuardarCommand = New DelegateCommand(AddressOf OnGuardar, AddressOf CanGuardar)
        NuevaAgenciaCommand = New DelegateCommand(AddressOf OnNuevaAgencia)
        Dim unused = CargarAsync()
    End Sub

    Private _agencias As ObservableCollection(Of AgenciaMantenimiento)
    Public Property Agencias As ObservableCollection(Of AgenciaMantenimiento)
        Get
            Return _agencias
        End Get
        Set(value As ObservableCollection(Of AgenciaMantenimiento))
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
    Public Property NuevaAgenciaCommand As DelegateCommand

    Public Async Function CargarAsync() As Task
        Dim lista = Await _servicio.LeerAgencias()
        Dim enCuarentena = Await LeerCuarentenaAsync()
        For Each agencia In lista
            agencia.EnCuarentena = enCuarentena.Contains(agencia.Nombre)
        Next
        Agencias = New ObservableCollection(Of AgenciaMantenimiento)(lista)
    End Function

    Public Async Function GuardarAsync() As Task
        For Each agencia In Agencias
            ' Admite la cuenta en formato abreviado con punto (555.20 -> 55500020). Una cuenta ya
            ' expandida o vacía (Glovo/Canteras no tienen) se queda igual.
            agencia.CuentaReembolsos = ExpandirCuentaReembolsos(agencia.CuentaReembolsos)
            If agencia.EsNueva Then
                Await _servicio.CrearAgencia(agencia)
                agencia.EsNueva = False
            Else
                Await _servicio.GuardarAgencia(agencia)
            End If
        Next
        Await GuardarCuarentenaAsync()
    End Function

    Private Shared Function ExpandirCuentaReembolsos(cuenta As String) As String
        If String.IsNullOrWhiteSpace(cuenta) Then
            Return cuenta
        End If
        Dim cuentaExpandida As String = Nothing
        If Not CuentaContableHelper.TryExpandirCuenta(cuenta, cuentaExpandida) Then
            Throw New ApplicationException($"La cuenta de reembolsos '{cuenta}' no es válida.")
        End If
        Return cuentaExpandida
    End Function

    ' Nombres (distinct) marcados en cuarentena -> parámetro "Nombre1, Nombre2, ...".
    Private Async Function GuardarCuarentenaAsync() As Task
        Dim nombres = Agencias.
            Where(Function(a) a.EnCuarentena AndAlso Not String.IsNullOrWhiteSpace(a.Nombre)).
            Select(Function(a) a.Nombre.Trim()).
            Distinct()
        Dim valor = String.Join(", ", nombres)
        Await _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AgenciasEnCuarentena, valor)
    End Function

    Private Async Function LeerCuarentenaAsync() As Task(Of List(Of String))
        Dim valorParametro As String = Await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AgenciasEnCuarentena)
        If String.IsNullOrWhiteSpace(valorParametro) Then
            Return New List(Of String)
        End If
        Return valorParametro.
            Split(","c).
            Select(Function(a) a.Trim()).
            Where(Function(a) Not String.IsNullOrEmpty(a)).
            ToList()
    End Function

    Private Function CanGuardar() As Boolean
        Return Agencias IsNot Nothing AndAlso Agencias.Any()
    End Function

    Private Sub OnNuevaAgencia()
        If Agencias Is Nothing Then
            Agencias = New ObservableCollection(Of AgenciaMantenimiento)
        End If
        Dim siguienteNumero = If(Agencias.Any(), Agencias.Max(Function(a) a.Numero) + 1, 1)
        Agencias.Add(New AgenciaMantenimiento With {
            .Numero = siguienteNumero,
            .Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
            .EsNueva = True
        })
        GuardarCommand?.RaiseCanExecuteChanged()
    End Sub

    Private Async Sub OnGuardar()
        Try
            EstaOcupado = True
            Await GuardarAsync()
            _dialogService.ShowNotification("Agencias guardadas correctamente")
        Catch ex As Exception
            _dialogService.ShowError($"Error al guardar las agencias: {ex.Message}")
        Finally
            EstaOcupado = False
        End Try
    End Sub

End Class
