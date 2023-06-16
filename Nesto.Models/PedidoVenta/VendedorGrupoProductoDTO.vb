Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Public Class VendedorGrupoProductoDTO
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
            OnPropertyChanged("vendedor")
        End Set
    End Property
    Private _estado As Short
    Public Property estado As Short
        Get
            Return _estado
        End Get
        Set(value As Short)
            _estado = value
            OnPropertyChanged(NameOf(estado))
        End Set
    End Property
    Public Property grupoProducto As String
    Public Property usuario As String
    Private Sub OnPropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class
