Imports Nesto.Models
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs

''' <summary>
''' NestoAPI#352 (decisión Carlos 20/08/26): una línea de inmovilizado comisiona por el grupo
''' que elige QUIEN mete el pedido — el servidor rechaza la línea sin grupo (la comisión nunca
''' sale mal o vacía en silencio). Este diálogo se abre al guardar, una vez por línea de
''' inmovilizado sin grupo, con los grupos reales de la tabla GruposProducto (vía API).
''' Aceptar solo se habilita con un grupo elegido; Cancelar aborta el guardado del pedido.
''' </summary>
Public Class SelectorGrupoComisionDialogViewModel
    Inherits BindableBase
    Implements IDialogAware

    Public ReadOnly Property Title As String Implements IDialogAware.Title
        Get
            Return "Grupo de comisión del inmovilizado"
        End Get
    End Property

    Private _mensaje As String
    Public Property Mensaje As String
        Get
            Return _mensaje
        End Get
        Set(value As String)
            Dim unused = SetProperty(_mensaje, value)
        End Set
    End Property

    Private _grupos As List(Of GrupoProductoDTO) = New List(Of GrupoProductoDTO)
    Public Property Grupos As List(Of GrupoProductoDTO)
        Get
            Return _grupos
        End Get
        Set(value As List(Of GrupoProductoDTO))
            Dim unused = SetProperty(_grupos, value)
        End Set
    End Property

    Private _grupoSeleccionado As String
    Public Property GrupoSeleccionado As String
        Get
            Return _grupoSeleccionado
        End Get
        Set(value As String)
            If SetProperty(_grupoSeleccionado, value) Then
                AceptarCommand.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _aceptarCommand As DelegateCommand
    Public ReadOnly Property AceptarCommand As DelegateCommand
        Get
            If _aceptarCommand Is Nothing Then
                _aceptarCommand = New DelegateCommand(
                    Sub()
                        Dim resultado As New DialogResult(ButtonResult.OK, New DialogParameters From {
                            {"grupo", GrupoSeleccionado}
                        })
                        RaiseEvent RequestClose(resultado)
                    End Sub,
                    Function() Not String.IsNullOrWhiteSpace(GrupoSeleccionado))
            End If
            Return _aceptarCommand
        End Get
    End Property

    Private _cancelarCommand As DelegateCommand
    Public ReadOnly Property CancelarCommand As DelegateCommand
        Get
            If _cancelarCommand Is Nothing Then
                _cancelarCommand = New DelegateCommand(
                    Sub() RaiseEvent RequestClose(New DialogResult(ButtonResult.Cancel)))
            End If
            Return _cancelarCommand
        End Get
    End Property

    Public Event RequestClose As Action(Of IDialogResult) Implements IDialogAware.RequestClose

    Public Function CanCloseDialog() As Boolean Implements IDialogAware.CanCloseDialog
        Return True
    End Function

    Public Sub OnDialogClosed() Implements IDialogAware.OnDialogClosed
    End Sub

    Public Sub OnDialogOpened(parameters As IDialogParameters) Implements IDialogAware.OnDialogOpened
        If parameters.ContainsKey("mensaje") Then
            Mensaje = parameters.GetValue(Of String)("mensaje")
        End If
        If parameters.ContainsKey("grupos") Then
            Grupos = parameters.GetValue(Of List(Of GrupoProductoDTO))("grupos")
        End If
    End Sub
End Class
