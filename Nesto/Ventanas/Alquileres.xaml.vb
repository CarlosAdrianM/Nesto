Imports System.ComponentModel
Imports Nesto.Modules.Producto.ViewModels
Imports Nesto.ViewModels

' Nesto#490 (4C.3): el ViewModel llega por constructor (antes, ViewModelLocator de Prism).
Partial Public Class Alquileres

    Private ReadOnly _viewModel As AlquileresViewModel
    Private ReadOnly _historia As HistoriaAparatoViewModel
    Private ReadOnly _descuadres As DescuadresAparatosViewModel

    ' NestoAPI#913 (corte 1): las pestañas «Historia del aparato» y «Descuadres» son controles del módulo Producto (C#) con su
    ' ViewModel; aquí solo se conectan con la relación: la historia se lee del registro elegido cuando su pestaña está a la vista,
    ' los descuadres la primera vez que se abre la suya, y el doble clic en un descuadre va a su registro.
    Public Sub New(viewModel As AlquileresViewModel, historia As HistoriaAparatoViewModel, descuadres As DescuadresAparatosViewModel)
        InitializeComponent()
        DataContext = viewModel
        _viewModel = viewModel
        _historia = historia
        _descuadres = descuadres
        vistaHistoriaAparato.DataContext = historia
        vistaDescuadres.DataContext = descuadres
        If descuadres IsNot Nothing Then
            descuadres.IrAlRegistro = AddressOf IrAlRegistro
        End If
        If viewModel IsNot Nothing Then
            AddHandler viewModel.PropertyChanged, AddressOf AlCambiarViewModel
        End If
        AddHandler tabPestanas.SelectionChanged, AddressOf AlCambiarPestana
        AddHandler Unloaded, AddressOf AlDescargar
    End Sub

    Private Sub AlDescargar(sender As Object, e As RoutedEventArgs)
        If _viewModel IsNot Nothing Then
            RemoveHandler _viewModel.PropertyChanged, AddressOf AlCambiarViewModel
        End If
        If tabPestanas IsNot Nothing Then
            RemoveHandler tabPestanas.SelectionChanged, AddressOf AlCambiarPestana
        End If
        If _descuadres IsNot Nothing Then
            _descuadres.IrAlRegistro = Nothing
        End If
        RemoveHandler Unloaded, AddressOf AlDescargar
    End Sub

    Private Sub AlCambiarViewModel(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(AlquileresViewModel.LineaSeleccionada) AndAlso tabPestanas?.SelectedItem Is tabHistoriaAparato Then
            CargarHistoria()
        End If
    End Sub

    Private Sub AlCambiarPestana(sender As Object, e As SelectionChangedEventArgs)
        If e.OriginalSource IsNot tabPestanas Then
            Return ' los DataGrid de dentro también lanzan SelectionChanged
        End If
        If tabPestanas.SelectedItem Is tabHistoriaAparato Then
            CargarHistoria()
        ElseIf tabPestanas.SelectedItem Is tabDescuadres AndAlso _descuadres IsNot Nothing AndAlso _descuadres.Descuadres.Count = 0 AndAlso Not _descuadres.EstaCargando Then
            Dim unused = _descuadres.CargarAsync()
        End If
    End Sub

    Private Sub CargarHistoria()
        If _historia Is Nothing Then
            Return
        End If
        Dim linea = _viewModel?.LineaSeleccionada
        Dim unused = _historia.CargarAsync(linea?.Producto, linea?.NumeroSerie)
    End Sub

    Private Async Sub IrAlRegistro(producto As String, numeroSerie As String)
        If _viewModel Is Nothing Then
            Return
        End If
        If Await _viewModel.IrAlRegistroAsync(producto, numeroSerie) AndAlso tabPestanas IsNot Nothing Then
            tabPestanas.SelectedItem = tabAlquiler
        End If
    End Sub

End Class
