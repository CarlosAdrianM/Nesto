Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts

''' <summary>
''' Novedad 1146 (Alfredo, jefe de almacén, 10/11/26: «No siempre se quita picking de un pedido entero, lo más común es quitar alguna
''' línea, por ejemplo, los portes»): las líneas del pedido con picking, con casillas. Todas las que se pueden quitar salen marcadas
''' (= el pedido entero, como antes); las que no se pueden, desmarcadas y con el motivo.
''' Botones: «Quitar picking de las líneas marcadas» (OK, con los Nº Orden en "lineas" y "todas" = True si están todas) y «Cancelar».
''' </summary>
Public Class QuitarPickingLineasPopupViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "QuitarPickingLineasPopup"

    Public Sub New()
        Title = "Quitar picking"
    End Sub

    Private _pedido As Integer
    Public ReadOnly Property Cabecera As String
        Get
            Return $"Líneas del pedido {_pedido} con picking. Desmarque las que deban seguir en el picking; " +
                "las marcadas se quitan del picking (y sueltan lo reservado en Ubicaciones)."
        End Get
    End Property

    Private _lineas As ObservableCollection(Of LineaConPicking) = New ObservableCollection(Of LineaConPicking)
    Public Property Lineas As ObservableCollection(Of LineaConPicking)
        Get
            Return _lineas
        End Get
        Private Set(value As ObservableCollection(Of LineaConPicking))
            For Each l In _lineas
                RemoveHandler l.PropertyChanged, AddressOf OnLineaCambiada
            Next
            SetProperty(_lineas, If(value, New ObservableCollection(Of LineaConPicking)))
            For Each l In _lineas
                AddHandler l.PropertyChanged, AddressOf OnLineaCambiada
            Next
            ActualizarMarcadas()
        End Set
    End Property

    Public ReadOnly Property HayMarcadas As Boolean
        Get
            Return Lineas.Any(Function(l) l.Marcado)
        End Get
    End Property

    ''' <summary>Todas las que se pueden quitar están marcadas: es el pedido entero (como antes de la novedad 1146).</summary>
    Public ReadOnly Property EstanTodas As Boolean
        Get
            Return Lineas.Where(Function(l) l.SePuedeQuitar).All(Function(l) l.Marcado)
        End Get
    End Property

    Private Sub OnLineaCambiada(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(LineaConPicking.Marcado) Then
            ActualizarMarcadas()
        End If
    End Sub

    Private Sub ActualizarMarcadas()
        OnPropertyChanged(NameOf(HayMarcadas))
        OnPropertyChanged(NameOf(EstanTodas))
        _quitarCommand?.NotifyCanExecuteChanged()
    End Sub

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        Dim lista As IEnumerable(Of LineaConPicking) = Nothing
        If parameters IsNot Nothing AndAlso parameters.ContainsKey("lineas") Then
            lista = parameters.GetValue(Of IEnumerable(Of LineaConPicking))("lineas")
        End If
        If parameters IsNot Nothing AndAlso parameters.ContainsKey("pedido") Then
            _pedido = parameters.GetValue(Of Integer)("pedido")
            OnPropertyChanged(NameOf(Cabecera))
        End If
        Dim nuevas = New ObservableCollection(Of LineaConPicking)(If(lista, Enumerable.Empty(Of LineaConPicking)()).Where(Function(l) l IsNot Nothing))
        For Each l In nuevas
            l.Marcado = l.SePuedeQuitar
        Next
        Lineas = nuevas
    End Sub

    ''' <summary>Al cerrar, se sueltan los manejadores de las líneas (null-safe).</summary>
    Public Overrides Sub OnDialogClosed()
        If _lineas IsNot Nothing Then
            For Each l In _lineas
                If l IsNot Nothing Then
                    RemoveHandler l.PropertyChanged, AddressOf OnLineaCambiada
                End If
            Next
        End If
        MyBase.OnDialogClosed()
    End Sub

    Private _quitarCommand As RelayCommand
    Public ReadOnly Property QuitarCommand As RelayCommand
        Get
            If _quitarCommand Is Nothing Then
                _quitarCommand = New RelayCommand(
                    Sub()
                        Dim marcadas As List(Of Integer) = Lineas.Where(Function(l) l.Marcado).Select(Function(l) l.Linea).ToList()
                        RaiseRequestClose(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {
                            {"lineas", marcadas},
                            {"todas", EstanTodas}
                        }))
                    End Sub,
                    Function() HayMarcadas)
            End If
            Return _quitarCommand
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
