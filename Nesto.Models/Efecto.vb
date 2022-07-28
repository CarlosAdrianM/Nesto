Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Public Class Efecto
    Implements INotifyPropertyChanged
    Private _fechaVencimiento As Date
    Public Property FechaVencimiento As Date
        Get
            Return _fechaVencimiento
        End Get
        Set(value As Date)
            If _fechaVencimiento <> value Then
                _fechaVencimiento = value
                RaisePropertyChanged(NameOf(FechaVencimiento))
            End If
        End Set
    End Property
    Private _importe As Decimal
    Public Property Importe As Decimal
        Get
            Return _importe
        End Get
        Set(value As Decimal)
            If _importe <> value Then
                _importe = value
                RaisePropertyChanged(NameOf(Importe))
            End If
        End Set
    End Property
    Private _formaPago As String
    Public Property FormaPago As String
        Get
            Return _formaPago
        End Get
        Set(value As String)
            If _formaPago <> value Then
                _formaPago = value.ToUpper.Trim
                RaisePropertyChanged(NameOf(FormaPago))
            End If
        End Set
    End Property
    Private _ccc As String
    Public Property CCC As String
        Get
            Return _ccc
        End Get
        Set(value As String)
            If _ccc <> value Then
                _ccc = value.ToUpper.Trim
                RaisePropertyChanged(NameOf(CCC))
            End If
        End Set
    End Property

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub RaisePropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

End Class
