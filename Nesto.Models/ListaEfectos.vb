Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports Prism.Commands

Public Class ListaEfectos
    Inherits ObservableCollection(Of Efecto)
    Implements INotifyPropertyChanged
    Public Sub New()
        Me.New(0)
    End Sub
    Public Sub New(importeTotal As Decimal)
        Me.New(importeTotal, String.Empty, String.Empty)
    End Sub
    Public Sub New(importeTotal As Decimal, formaPago As String, ccc As String)
        AddHandler Me.CollectionChanged, AddressOf ContentCollectionChanged
        AnnadirEfectoCommand = New DelegateCommand(AddressOf OnAnnadirEfecto)
        BorrarEfectoCommand = New DelegateCommand(Of Efecto)(AddressOf OnBorrarEfecto, AddressOf CanBorrarEfecto)

        Me.ImporteTotal = importeTotal
        FormaPagoCliente = formaPago
        CccCliente = ccc
    End Sub
    Private evento As New PropertyChangedEventHandler(AddressOf EfectoPropertyChanged)
    Private Sub ContentCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        If e.Action = NotifyCollectionChangedAction.Add Then
            For Each newItem In e.NewItems
                CuadrarImporteTotal(newItem)
            Next
        End If

        If e.Action = NotifyCollectionChangedAction.Remove Then
            For Each oldItem In e.OldItems
                Dim efecto As Efecto = DirectCast(oldItem, Efecto)
                Dim evento As New PropertyChangedEventHandler(AddressOf EfectoPropertyChanged)
                RemoveHandler efecto.PropertyChanged, evento
                If Me.Any AndAlso ReferenceEquals(oldItem, Me.Last) Then
                    Me.First().Importe += efecto.Importe
                Else
                    Me.Last().Importe += efecto.Importe
                End If
            Next
        End If
    End Sub

    Private Function EfectoPropertyChanged(sender As Object, e As PropertyChangedEventArgs) As Object
        Dim efecto As Efecto = DirectCast(sender, Efecto)
        If e.PropertyName = NameOf(efecto.Importe) Then
            CuadrarImporteTotal(efecto)
        End If
        If e.PropertyName = NameOf(efecto.FechaVencimiento) Then
            RaisePropertyChanged(NameOf(DiasFinanciacion))
        End If
    End Function

    Private Sub CuadrarImporteTotal(efectoModificado As Efecto)
        If ImporteTotal = 0 Then
            Return
        End If
        For Each efecto In Me
            RemoveHandler efecto.PropertyChanged, evento
        Next
        If Me.Any AndAlso Me.Sum(Function(c) c.Importe) <> ImporteTotal Then
            Dim importeSumar As Decimal = Me.Sum(Function(c) c.Importe)
            If ReferenceEquals(efectoModificado, Me.Last) Then
                Me.First().Importe += ImporteTotal - importeSumar
            Else
                Me.Last().Importe += ImporteTotal - importeSumar
            End If
        End If
        For Each efecto In Me
            AddHandler DirectCast(efecto, Efecto).PropertyChanged, evento
        Next
        RaisePropertyChanged(NameOf(DiasFinanciacion))
    End Sub

    Private _cccCliente As String
    Public Property CccCliente As String
        Get
            Return _cccCliente
        End Get
        Set(value As String)
            If _cccCliente <> value Then
                _cccCliente = value
                RaisePropertyChanged(NameOf(CccCliente))
            End If
        End Set
    End Property
    Public ReadOnly Property DiasFinanciacion As Integer
        Get
            Dim totalDiasImporte As Integer = 0
            For Each efecto In Me
                Dim diasHastaVencimiento = efecto.FechaVencimiento - DateTime.Today
                If diasHastaVencimiento.Days < 0 Then
                    diasHastaVencimiento = TimeSpan.Zero
                End If
                totalDiasImporte += diasHastaVencimiento.Days * efecto.Importe
            Next
            Return If(ImporteTotal <> 0, totalDiasImporte / ImporteTotal, 0)
        End Get
    End Property

    Private _formaPagoCliente As String
    Public Property FormaPagoCliente As String
        Get
            Return _formaPagoCliente
        End Get
        Set(value As String)
            If _formaPagoCliente <> value Then
                _formaPagoCliente = value
                RaisePropertyChanged(NameOf(FormaPagoCliente))
            End If
        End Set
    End Property

    Private _importeTotal As Decimal
    Public Property ImporteTotal As Decimal
        Get
            Return _importeTotal
        End Get
        Set(value As Decimal)
            If _importeTotal <> value Then
                _importeTotal = value
                RaisePropertyChanged()
                If Not Me.Any Then
                    AnnadirEfectoCommand.Execute()
                End If
                If Me.Any AndAlso Me.Sum(Function(c) c.Importe) <> ImporteTotal Then
                    Me.Last().Importe += ImporteTotal - Me.Sum(Function(c) c.Importe)
                End If
                RaisePropertyChanged(NameOf(DiasFinanciacion))
            End If
        End Set
    End Property

    Private _annadirEfectoCommand As DelegateCommand
    Public Property AnnadirEfectoCommand As DelegateCommand
        Get
            Return _annadirEfectoCommand
        End Get
        Private Set(value As DelegateCommand)
            If Not value.Equals(_annadirEfectoCommand) Then
                _annadirEfectoCommand = value
                RaisePropertyChanged(NameOf(AnnadirEfectoCommand))
            End If
        End Set
    End Property
    Private Sub OnAnnadirEfecto()
        Dim igualarImportes As Boolean = Me.Any AndAlso Not Me.Any(Function(e) e.Importe > Me.ElementAt(0).Importe + 0.05 OrElse e.Importe < Me.ElementAt(0).Importe - 0.05)
        Dim importe As Decimal
        Dim formaPago As String
        Dim ccc As String
        Dim fechaVencimiento As Date
        If Me.Any Then
            importe = Me.Last.Importe
            formaPago = Me.Last.FormaPago
            ccc = Me.Last.CCC
            fechaVencimiento = Me.Last.FechaVencimiento.AddMonths(1)
        Else
            importe = Math.Round(ImporteTotal / (Me.Count + 1), 2)
            formaPago = FormaPagoCliente
            ccc = CccCliente
            fechaVencimiento = Today
        End If
        Dim nuevoEfecto = New Efecto() With {.Importe = importe, .CCC = ccc, .FechaVencimiento = fechaVencimiento, .FormaPago = formaPago}
        AddHandler DirectCast(nuevoEfecto, Efecto).PropertyChanged, evento
        Me.Add(nuevoEfecto)
        If igualarImportes AndAlso Me.Count > 1 Then
            Me.IgualarImportes()
        End If
    End Sub

    Private Sub IgualarImportes()
        For Each efecto In Me
            RemoveHandler efecto.PropertyChanged, evento
            If ReferenceEquals(efecto, Me.Last()) Then
                Dim importeSumar = Math.Round(Me.ImporteTotal - Me.Sum(Function(s) s.Importe), 2)
                If efecto.Importe + importeSumar <> 0 Then
                    efecto.Importe += importeSumar
                Else
                    BorrarEfectoCommand.Execute(efecto)
                    Return
                End If
            Else
                efecto.Importe = Math.Round(Me.ImporteTotal / Me.Count, 2)
            End If
            AddHandler DirectCast(efecto, Efecto).PropertyChanged, evento
            RaisePropertyChanged(NameOf(DiasFinanciacion))
        Next
    End Sub

    Private _borrarEfectoCommand As DelegateCommand(Of Efecto)

    Public Property BorrarEfectoCommand As DelegateCommand(Of Efecto)
        Get
            Return _borrarEfectoCommand
        End Get
        Private Set(value As DelegateCommand(Of Efecto))
            If Not value.Equals(_borrarEfectoCommand) Then
                _borrarEfectoCommand = value
                RaisePropertyChanged(NameOf(BorrarEfectoCommand))
            End If
        End Set
    End Property
    Private Function CanBorrarEfecto(efecto As Efecto) As Boolean
        Return Me.Any AndAlso Me.Count > 1
    End Function
    Private Sub OnBorrarEfecto(efecto As Efecto)
        Dim igualarImportes As Boolean = Me.Any AndAlso Not Me.Any(Function(e) e.Importe > Me.ElementAt(0).Importe + 0.05 OrElse e.Importe < Me.ElementAt(0).Importe - 0.05)
        RemoveHandler efecto.PropertyChanged, evento
        Me.Remove(efecto)
        If igualarImportes AndAlso Me.Count > 1 Then
            Me.IgualarImportes()
        End If
    End Sub

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub RaisePropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

End Class
