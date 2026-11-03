Imports System.Collections.ObjectModel
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Nesto#597 (gemela de NestoAPI#790): lo que se comprueba antes de contabilizar el inventario. Los bloqueantes (en rojo) no
''' dejan seguir; con solo avisos (en ámbar) se puede «Continuar de todos modos». Vuelve OK para seguir y Cancel si no.
''' </summary>
Public Class ComprobacionesInventarioDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "ComprobacionesInventarioDialog"
    Public Const PARAMETRO_COMPROBACIONES As String = "comprobaciones"

    Public Sub New()
        Title = "Comprobaciones del inventario"
        ContinuarCommand = New RelayCommand(AddressOf OnContinuar, Function() PuedeContinuar)
        CancelarCommand = New RelayCommand(Sub() RaiseRequestClose(ResultadoBoton.Cancel))
    End Sub

    Public Property ContinuarCommand As RelayCommand
    Public Property CancelarCommand As RelayCommand

    Public ReadOnly Property Bloqueantes As New ObservableCollection(Of ComprobacionInventarioDTO)
    Public ReadOnly Property Avisos As New ObservableCollection(Of ComprobacionInventarioDTO)
    Public ReadOnly Property Recuento As New ObservableCollection(Of RecuentoUsuarioInventarioDTO)

    Private _cabecera As String
    Public Property Cabecera As String
        Get
            Return _cabecera
        End Get
        Private Set(value As String)
            SetProperty(_cabecera, value)
        End Set
    End Property

    Public ReadOnly Property HayBloqueantes As Boolean
        Get
            Return Bloqueantes.Count > 0
        End Get
    End Property

    Public ReadOnly Property HayAvisos As Boolean
        Get
            Return Avisos.Count > 0
        End Get
    End Property

    Public ReadOnly Property PuedeContinuar As Boolean
        Get
            Return Not HayBloqueantes
        End Get
    End Property

    Public ReadOnly Property TextoContinuar As String
        Get
            Return If(HayAvisos, "Continuar de todos modos", "Continuar")
        End Get
    End Property

    Public ReadOnly Property Explicacion As String
        Get
            If HayBloqueantes Then
                Return "Hay problemas que impiden contabilizar el inventario. Corríjalos y vuelva a intentarlo."
            End If
            If HayAvisos Then
                Return "Revise los avisos: no impiden contabilizar, pero conviene comprobarlos antes."
            End If
            Return "No se ha encontrado ningún problema."
        End Get
    End Property

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        Cargar(parameters?.GetValue(Of ComprobacionesInventarioDTO)(PARAMETRO_COMPROBACIONES))
    End Sub

    Public Sub Cargar(comprobaciones As ComprobacionesInventarioDTO)
        Bloqueantes.Clear()
        Avisos.Clear()
        Recuento.Clear()
        If comprobaciones IsNot Nothing Then
            For Each c In If(comprobaciones.Bloqueantes, New List(Of ComprobacionInventarioDTO))
                Bloqueantes.Add(c)
            Next
            For Each c In If(comprobaciones.Avisos, New List(Of ComprobacionInventarioDTO))
                Avisos.Add(c)
            Next
            For Each r In If(comprobaciones.Recuento, New List(Of RecuentoUsuarioInventarioDTO))
                Recuento.Add(r)
            Next
            Cabecera = $"Inventario de {comprobaciones.Almacen?.Trim()} del {comprobaciones.Fecha:dd/MM/yyyy}"
        Else
            Cabecera = String.Empty
        End If
        OnPropertyChanged(NameOf(HayBloqueantes))
        OnPropertyChanged(NameOf(HayAvisos))
        OnPropertyChanged(NameOf(PuedeContinuar))
        OnPropertyChanged(NameOf(TextoContinuar))
        OnPropertyChanged(NameOf(Explicacion))
        ContinuarCommand.NotifyCanExecuteChanged()
    End Sub

    Private Sub OnContinuar()
        If PuedeContinuar Then
            RaiseRequestClose(ResultadoBoton.OK)
        End If
    End Sub
End Class
