Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts

''' <summary>
''' Sugerencia 575 (Alfredo, almacén; aprobada por Carlos el 13/10/26): antes de sacar el picking, los pedidos que van por
''' OnTime y que seguramente saldrán por nuestra ruta (AT/16), con la ruta propuesta y el motivo. Almacén marca los que
''' quiere pasar (y puede elegir la otra ruta propia) y sigue con el picking; nada se cambia solo. Solo lo ve almacén al
''' sacar el picking: quien mete el pedido no ve nada de esto.
''' Botones: «Cambiar la ruta y sacar el picking» (OK, con los marcados en "cambios"), «Sacar el picking sin cambiar» (No) y
''' «Cancelar» (Cancel: no se saca el picking).
''' </summary>
Public Class SugerenciasRutaPickingPopupViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "SugerenciasRutaPickingPopup"

    Public Sub New()
        Title = "Pedidos que podrían ir por nuestra ruta"
    End Sub

    Public ReadOnly Property RutasPropias As IReadOnlyList(Of String) = SugerenciaRutaPicking.RutasPropias

    Private _sugerencias As ObservableCollection(Of SugerenciaRutaPicking) = New ObservableCollection(Of SugerenciaRutaPicking)
    Public Property Sugerencias As ObservableCollection(Of SugerenciaRutaPicking)
        Get
            Return _sugerencias
        End Get
        Private Set(value As ObservableCollection(Of SugerenciaRutaPicking))
            For Each s In _sugerencias
                RemoveHandler s.PropertyChanged, AddressOf OnSugerenciaCambiada
            Next
            SetProperty(_sugerencias, value)
            For Each s In _sugerencias
                AddHandler s.PropertyChanged, AddressOf OnSugerenciaCambiada
            Next
            OnPropertyChanged(NameOf(Cabecera))
            ActualizarMarcados()
        End Set
    End Property

    Public ReadOnly Property Cabecera As String
        Get
            Dim n As Integer = Sugerencias.Count
            Return If(n = 1, "Este pedido podría ir por nuestra ruta", $"Estos {n} pedidos podrían ir por nuestra ruta") +
                " (AT o 16) en vez de por la agencia. Marca los que quieras cambiar antes de sacar el picking; los demás salen como están."
        End Get
    End Property

    Public ReadOnly Property HayMarcados As Boolean
        Get
            Return Sugerencias.Any(Function(s) s.Marcado)
        End Get
    End Property

    Private Sub OnSugerenciaCambiada(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(SugerenciaRutaPicking.Marcado) Then
            ActualizarMarcados()
        End If
    End Sub

    Private Sub ActualizarMarcados()
        OnPropertyChanged(NameOf(HayMarcados))
        _cambiarYSacarCommand?.NotifyCanExecuteChanged()
    End Sub

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        Dim lista As IEnumerable(Of SugerenciaRutaPicking) = Nothing
        If parameters IsNot Nothing AndAlso parameters.ContainsKey("sugerencias") Then
            lista = parameters.GetValue(Of IEnumerable(Of SugerenciaRutaPicking))("sugerencias")
        End If
        Sugerencias = New ObservableCollection(Of SugerenciaRutaPicking)(If(lista, Enumerable.Empty(Of SugerenciaRutaPicking)()))
    End Sub

    Private _cambiarYSacarCommand As RelayCommand
    Public ReadOnly Property CambiarYSacarCommand As RelayCommand
        Get
            If _cambiarYSacarCommand Is Nothing Then
                _cambiarYSacarCommand = New RelayCommand(
                    Sub()
                        Dim cambios As List(Of SugerenciaRutaPicking) = Sugerencias.Where(Function(s) s.Marcado).ToList()
                        RaiseRequestClose(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {
                            {"cambios", cambios}
                        }))
                    End Sub,
                    Function() HayMarcados)
            End If
            Return _cambiarYSacarCommand
        End Get
    End Property

    Private _sacarSinCambiarCommand As RelayCommand
    Public ReadOnly Property SacarSinCambiarCommand As RelayCommand
        Get
            If _sacarSinCambiarCommand Is Nothing Then
                _sacarSinCambiarCommand = New RelayCommand(Sub() RaiseRequestClose(ResultadoBoton.No))
            End If
            Return _sacarSinCambiarCommand
        End Get
    End Property

    Private _cancelarCommand As RelayCommand
    Public ReadOnly Property CancelarCommand As RelayCommand
        Get
            If _cancelarCommand Is Nothing Then
                _cancelarCommand = New RelayCommand(Sub() RaiseRequestClose(ResultadoBoton.Cancel))
            End If
            Return _cancelarCommand
        End Get
    End Property
End Class
