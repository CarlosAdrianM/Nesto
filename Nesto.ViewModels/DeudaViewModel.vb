Imports Nesto.Models.MainModel
Imports System.Windows.Controls
Imports System.Windows
Imports System.Windows.Input
Imports System.Collections.ObjectModel
Imports System.Windows.Media


Public Class DeudaViewModel
    Inherits ViewModelBase

    Public Sub New()
        Me._RatiosDeuda = RatioDeuda.CargarRatiosDeuda
        Titulo = "Ratio de Deuda"
    End Sub

    Private _RatiosDeuda As RatioDeuda
    Public ReadOnly Property RatiosDeuda As RatioDeuda
        Get
            Return _RatiosDeuda
        End Get

        'Set(ByVal value As RatioDeuda)
        '    Me._RatiosDeuda = value
        '    OnPropertyChanged("RatiosDeuda")
        'End Set
    End Property

    Private _DetalleVentaReal As DetalleVentaReal
    Public Property DetalleVentaReal As DetalleVentaReal
        Get
            Return _DetalleVentaReal
        End Get
        Set(value As DetalleVentaReal)
            _DetalleVentaReal = value
            OnPropertyChanged("DetalleVentaReal")
        End Set
    End Property

    Private _DetalleVentaPeriodo As DetalleVentaPeriodo
    Public Property DetalleVentaPeriodo As DetalleVentaPeriodo
        Get
            Return _DetalleVentaPeriodo
        End Get
        Set(value As DetalleVentaPeriodo)
            _DetalleVentaPeriodo = value
            OnPropertyChanged("DetalleVentaPeriodo")
        End Set
    End Property

    Private _DetalleDeuda As DetalleDeuda
    Public Property DetalleDeuda As DetalleDeuda
        Get
            Return _DetalleDeuda
        End Get
        Set(value As DetalleDeuda)
            _DetalleDeuda = value
            OnPropertyChanged("DetalleDeuda")
        End Set
    End Property

    Private _DeudaAgrupada As IEnumerable
    Public Property DeudaAgrupada As IEnumerable
        Get
            Return _DeudaAgrupada
        End Get
        Set(value As IEnumerable)
            _DeudaAgrupada = value
            OnPropertyChanged("DeudaAgrupada")
        End Set
    End Property

    Private _RatioDeudaSeleccionado As RatioDeuda
    Public Property RatioDeudaSeleccionado As RatioDeuda
        Get
            Return _RatioDeudaSeleccionado
        End Get
        Set(value As RatioDeuda)
            _RatioDeudaSeleccionado = value
            OnPropertyChanged("RatioDeudaSeleccionado")
        End Set
    End Property

    Public ReadOnly Property MediaRatioDeuda() As Decimal
        Get
            If Me.RatiosDeuda.Item(0) IsNot Nothing Then
                Return Me.RatiosDeuda.Item(0).SumaRatioPeriodo
            Else
                Return 0
            End If
        End Get
    End Property

#Region "Comandos"
    Private _cmdCargarDetalleVentaReal As ICommand
    Public ReadOnly Property cmdCargarDetalleVentaReal() As ICommand
        Get
            If _cmdCargarDetalleVentaReal Is Nothing Then
                _cmdCargarDetalleVentaReal = New RelayCommand(AddressOf CargarDetalleVentaReal, AddressOf CanCargarDetalleVentaReal)
            End If
            Return _cmdCargarDetalleVentaReal
        End Get
    End Property
    Private Function CanCargarDetalleVentaReal(ByVal param As Object) As Boolean
        Return param IsNot Nothing
    End Function
    Private Sub CargarDetalleVentaReal(ByVal param As Object)
        DetalleVentaReal = DetalleVentaReal.CargarDetalleVentaReal(param.ToString)
    End Sub

    Private _cmdCargarDetalleVentaPeriodo As ICommand
    Public ReadOnly Property cmdCargarDetalleVentaPeriodo() As ICommand
        Get
            If _cmdCargarDetalleVentaPeriodo Is Nothing Then
                _cmdCargarDetalleVentaPeriodo = New RelayCommand(AddressOf CargarDetalleVentaPeriodo, AddressOf CanCargarDetalleVentaPeriodo)
            End If
            Return _cmdCargarDetalleVentaPeriodo
        End Get
    End Property
    Private Function CanCargarDetalleVentaPeriodo(ByVal param As Object) As Boolean
        Return param IsNot Nothing
    End Function
    Private Sub CargarDetalleVentaPeriodo(ByVal param As Object)
        DetalleVentaPeriodo = DetalleVentaPeriodo.CargarDetalleVentaPeriodo(param.ToString)
    End Sub

    Private _cmdCargarDetalleDeuda As ICommand
    Public ReadOnly Property cmdCargarDetalleDeuda() As ICommand
        Get
            If _cmdCargarDetalleDeuda Is Nothing Then
                _cmdCargarDetalleDeuda = New RelayCommand(AddressOf CargarDetalleDeuda, AddressOf CanCargarDetalleDeuda)
            End If
            Return _cmdCargarDetalleDeuda
        End Get
    End Property
    Private Function CanCargarDetalleDeuda(ByVal param As Object) As Boolean
        Return param IsNot Nothing
    End Function
    Private Sub CargarDetalleDeuda(ByVal param As Object)
        DetalleDeuda = DetalleDeuda.CargarDetalleDeuda(param.ToString)
        DeudaAgrupada = From x In DetalleDeuda.AsEnumerable()
                                Group By x.Cliente, x.Contacto, x.Nombre, x.Direccion, x.Poblacion, x.CodPostal, x.Provincia, x.Telefono Into g = Group
                                Where g.Sum(Function(d) d.Deuda) <> 0
                                Order By g.Sum(Function(d) d.Deuda) Descending
                                Select New With {
                                    Cliente, Contacto, Nombre, Direccion, Poblacion, CodPostal, Provincia, Telefono, .SumaDeuda = g.Sum(Function(d) d.Deuda), .Detalle = g.Select(Function(y) New With {
                                        y.Concepto,
                                        y.Deuda,
                                        y.FechaVto,
                                        y.Tipo
                                    }
                                 )}


    End Sub

    Private _cmdBorrarDetalleDeuda As ICommand
    Public ReadOnly Property cmdBorrarDetalleDeuda() As ICommand
        Get
            If _cmdBorrarDetalleDeuda Is Nothing Then
                _cmdBorrarDetalleDeuda = New RelayCommand(AddressOf BorrarDetalleDeuda, AddressOf CanBorrarDetalleDeuda)
            End If
            Return _cmdBorrarDetalleDeuda
        End Get
    End Property
    Private Function CanBorrarDetalleDeuda(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub BorrarDetalleDeuda(ByVal param As Object)
        DetalleDeuda = Nothing
        DeudaAgrupada = Nothing
    End Sub

    Private _cmdCargarDetalleVencido As ICommand
    Public ReadOnly Property cmdCargarDetalleVencido() As ICommand
        Get
            If _cmdCargarDetalleVencido Is Nothing Then
                _cmdCargarDetalleVencido = New RelayCommand(AddressOf CargarDetalleVencido, AddressOf CanCargarDetalleVencido)
            End If
            Return _cmdCargarDetalleVencido
        End Get
    End Property
    Private Function CanCargarDetalleVencido(ByVal param As Object) As Boolean
        Return (param IsNot Nothing) And (DetalleDeuda IsNot Nothing)
    End Function
    Private Sub CargarDetalleVencido(ByVal param As Object)

        DeudaAgrupada = From x In DetalleDeuda.AsEnumerable()
                                Group By x.Cliente, x.Contacto, x.Nombre, x.Direccion, x.Poblacion, x.CodPostal, x.Provincia, x.Telefono Into g = Group
                                Where (g.Where(Function(h) h.FechaVto < Today).Sum(Function(d) d.Deuda) <> 0)
                                Order By g.Where(Function(h) h.FechaVto < Today).Sum(Function(d) d.Deuda) Descending
                                Select New With {
                                    Cliente, Contacto, Nombre, Direccion, Poblacion, CodPostal, Provincia, Telefono, .SumaDeuda = g.Where(Function(h) h.FechaVto < Today).Sum(Function(d) d.Deuda), .Detalle = (From y In g Where y.FechaVto < Today Select New With {
                                        y.Concepto,
                                        y.Deuda,
                                        y.FechaVto,
                                        y.Tipo
                                    }
                                 )
                              }





    End Sub

#End Region

End Class
Public Class DeudaDataTemplateSelector
    Inherits DataTemplateSelector
    Public Overrides Function SelectTemplate(ByVal item As Object, ByVal container As DependencyObject) As DataTemplate

        Dim element As FrameworkElement
        element = TryCast(container, FrameworkElement)

        If element IsNot Nothing AndAlso item IsNot Nothing Then

            Dim RatioItem As Double = TryCast(item.Item("RatioPeriodo"), Object)

            ' Carlos 08/01/13: porcentaje a partir de que se premia o castiga
            Dim Bonus As Double = TryCast(item.Item("Bonus"), Object)
            Dim Malus As Double = TryCast(item.Item("Malus"), Object)

            If CDbl(RatioItem.ToString) <= Bonus Then
                Return TryCast(element.FindResource("DeudaTemplateVerde"), DataTemplate)
            ElseIf CDbl(RatioItem.ToString) > Malus Then
                Return TryCast(element.FindResource("DeudaTemplateRojo"), DataTemplate)
            Else
                Return TryCast(element.FindResource("DeudaTemplateNegro"), DataTemplate)
            End If
        End If

        Return Nothing
    End Function
End Class



