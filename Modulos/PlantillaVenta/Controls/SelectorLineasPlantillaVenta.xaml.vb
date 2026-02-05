Imports System.Collections
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports Nesto.Infrastructure.Shared

''' <summary>
''' Control reutilizable para mostrar y seleccionar lineas de productos en PlantillaVenta.
''' Issue #94: Sistema Ganavisiones - FASE 5/7
'''
''' Uso:
''' - En SeleccionProductos: muestra productos con cantidad cobrada y oferta
''' - En SeleccionRegalos (FASE 7): muestra productos bonificables sin cantidad oferta
'''
''' Incluye:
''' - BarraFiltro integrada (opcional)
''' - Atajos de teclado: Ctrl+1/2/3 para seleccionar, Ctrl+/- para cantidad
''' </summary>
Partial Public Class SelectorLineasPlantillaVenta
    Inherits UserControl

    Public Sub New()
        InitializeComponent()
        ' Sincronizar SelectedItem del ListView interno con la DependencyProperty
        AddHandler InternalListView.SelectionChanged, AddressOf InternalListView_SelectionChanged
        ' Handlers para atajos de teclado
        AddHandler BarraFiltroInternal.KeyUp, AddressOf BarraFiltro_KeyUp
        AddHandler BarraFiltroInternal.GotFocus, AddressOf BarraFiltro_GotFocus
        AddHandler BarraFiltroInternal.MouseUp, AddressOf BarraFiltro_MouseUp
    End Sub

    Private Sub InternalListView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        ' Actualizar la DependencyProperty cuando cambia la seleccion en el ListView interno
        If SelectedItem IsNot InternalListView.SelectedItem Then
            SelectedItem = InternalListView.SelectedItem
        End If
    End Sub

    ''' <summary>
    ''' Expone la coleccion Items del ListView interno para compatibilidad con code-behind existente.
    ''' Permite acceder a lstProductos.Items.Count, lstProductos.Items(index), etc.
    ''' </summary>
    Public ReadOnly Property Items As ItemCollection
        Get
            Return InternalListView.Items
        End Get
    End Property

    ''' <summary>
    ''' Da foco a la barra de filtro.
    ''' </summary>
    Public Sub EnfocarBarraFiltro()
        If MostrarBarraFiltro Then
            Dim unused = Keyboard.Focus(BarraFiltroInternal)
            Dim unused2 = BarraFiltroInternal.Focus()
        End If
    End Sub

    ''' <summary>
    ''' Expone la BarraFiltro interna para compatibilidad con code-behind existente.
    ''' Por ejemplo, para FocusAfterBusy del BusyIndicator.
    ''' </summary>
    Public ReadOnly Property BarraFiltro As ControlesUsuario.BarraFiltro
        Get
            Return BarraFiltroInternal
        End Get
    End Property

#Region "Atajos de teclado"

    Private Async Sub BarraFiltro_KeyUp(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Enter Then
            Await Task.Delay(500)
            Dim unused = Keyboard.Focus(BarraFiltroInternal)
            BarraFiltroInternal.SelectAll()
        End If

        If IsNothing(InternalListView) OrElse InternalListView.Items.Count = 0 Then
            Return
        End If

        ' Ctrl+1, Ctrl+2, Ctrl+3: Seleccionar elemento
        If e.Key = Key.D1 AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso InternalListView.Items.Count > 0 Then
            InternalListView.SelectedItem = InternalListView.Items(0)
        End If
        If e.Key = Key.D2 AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso InternalListView.Items.Count > 1 Then
            InternalListView.SelectedItem = InternalListView.Items(1)
        End If
        If e.Key = Key.D3 AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso InternalListView.Items.Count > 2 Then
            InternalListView.SelectedItem = InternalListView.Items(2)
        End If

        ' Ctrl++: Cantidad + 1
        If (e.Key = Key.OemPlus OrElse e.Key = Key.Add) AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control Then
            If IsNothing(InternalListView.SelectedItem) Then
                InternalListView.SelectedItem = InternalListView.Items(0)
            End If
            Dim linea = TryCast(InternalListView.SelectedItem, ILineaConCantidad)
            If linea IsNot Nothing Then
                linea.cantidad += 1
                ' Ejecutar comando de actualización si está definido
                If ActualizarProductoCommand IsNot Nothing AndAlso ActualizarProductoCommand.CanExecute(InternalListView.SelectedItem) Then
                    ActualizarProductoCommand.Execute(InternalListView.SelectedItem)
                End If
            End If
            BarraFiltroInternal.SelectAll()
        End If

        ' Ctrl+Shift++: Oferta + 1
        If (e.Key = Key.OemPlus OrElse e.Key = Key.Add) AndAlso e.KeyboardDevice.Modifiers = (ModifierKeys.Control Or ModifierKeys.Shift) Then
            If IsNothing(InternalListView.SelectedItem) Then
                InternalListView.SelectedItem = InternalListView.Items(0)
            End If
            Dim linea = TryCast(InternalListView.SelectedItem, ILineaConCantidad)
            If linea IsNot Nothing AndAlso linea.aplicarDescuentoFicha.GetValueOrDefault(False) Then
                linea.cantidadOferta += 1
                If ActualizarProductoCommand IsNot Nothing AndAlso ActualizarProductoCommand.CanExecute(InternalListView.SelectedItem) Then
                    ActualizarProductoCommand.Execute(InternalListView.SelectedItem)
                End If
                BarraFiltroInternal.SelectAll()
            End If
        End If

        ' Ctrl+-: Cantidad - 1
        If (e.Key = Key.OemMinus OrElse e.Key = Key.Subtract) AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control Then
            If IsNothing(InternalListView.SelectedItem) Then
                InternalListView.SelectedItem = InternalListView.Items(0)
            End If
            Dim linea = TryCast(InternalListView.SelectedItem, ILineaConCantidad)
            If linea IsNot Nothing AndAlso linea.cantidad > 0 Then
                linea.cantidad -= 1
                If ActualizarProductoCommand IsNot Nothing AndAlso ActualizarProductoCommand.CanExecute(InternalListView.SelectedItem) Then
                    ActualizarProductoCommand.Execute(InternalListView.SelectedItem)
                End If
            End If
            BarraFiltroInternal.SelectAll()
        End If

        ' Ctrl+Shift+-: Oferta - 1
        If (e.Key = Key.OemMinus OrElse e.Key = Key.Subtract) AndAlso e.KeyboardDevice.Modifiers = (ModifierKeys.Control Or ModifierKeys.Shift) Then
            If IsNothing(InternalListView.SelectedItem) Then
                InternalListView.SelectedItem = InternalListView.Items(0)
            End If
            Dim linea = TryCast(InternalListView.SelectedItem, ILineaConCantidad)
            If linea IsNot Nothing AndAlso linea.aplicarDescuentoFicha.GetValueOrDefault(False) AndAlso linea.cantidadOferta > 0 Then
                linea.cantidadOferta -= 1
                If ActualizarProductoCommand IsNot Nothing AndAlso ActualizarProductoCommand.CanExecute(InternalListView.SelectedItem) Then
                    ActualizarProductoCommand.Execute(InternalListView.SelectedItem)
                End If
                BarraFiltroInternal.SelectAll()
            End If
        End If

        ' Ctrl+6: 6+1
        If e.Key = Key.D6 AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control Then
            If IsNothing(InternalListView.SelectedItem) Then
                InternalListView.SelectedItem = InternalListView.Items(0)
            End If
            Dim linea = TryCast(InternalListView.SelectedItem, ILineaConCantidad)
            If linea IsNot Nothing AndAlso linea.aplicarDescuentoFicha.GetValueOrDefault(False) Then
                linea.cantidad = 6
                linea.cantidadOferta = 1
                If ActualizarProductoCommand IsNot Nothing AndAlso ActualizarProductoCommand.CanExecute(InternalListView.SelectedItem) Then
                    ActualizarProductoCommand.Execute(InternalListView.SelectedItem)
                End If
            End If
        End If
    End Sub

    Private Sub BarraFiltro_GotFocus(sender As Object, e As RoutedEventArgs)
        BarraFiltroInternal.SelectAll()
    End Sub

    Private Sub BarraFiltro_MouseUp(sender As Object, e As MouseButtonEventArgs)
        BarraFiltroInternal.SelectAll()
    End Sub

#End Region

#Region "DependencyProperties"

    ''' <summary>
    ''' Lista de items a mostrar en el ListView
    ''' </summary>
    Public Shared ReadOnly ItemsSourceProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(ItemsSource),
            GetType(IEnumerable),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(Nothing))

    Public Property ItemsSource As IEnumerable
        Get
            Return CType(GetValue(ItemsSourceProperty), IEnumerable)
        End Get
        Set(value As IEnumerable)
            SetValue(ItemsSourceProperty, value)
        End Set
    End Property

    ''' <summary>
    ''' Elemento seleccionado en el ListView
    ''' </summary>
    Public Shared ReadOnly SelectedItemProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(SelectedItem),
            GetType(Object),
            GetType(SelectorLineasPlantillaVenta),
            New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AddressOf OnSelectedItemChanged))

    Public Property SelectedItem As Object
        Get
            Return GetValue(SelectedItemProperty)
        End Get
        Set(value As Object)
            SetValue(SelectedItemProperty, value)
        End Set
    End Property

    Private Shared Sub OnSelectedItemChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim control = CType(d, SelectorLineasPlantillaVenta)
        ' Sincronizar con el ListView interno cuando se asigna desde code-behind
        If control.InternalListView IsNot Nothing AndAlso control.InternalListView.SelectedItem IsNot e.NewValue Then
            control.InternalListView.SelectedItem = e.NewValue
        End If
    End Sub

    ''' <summary>
    ''' Comando que se ejecuta cuando se actualiza la cantidad de un producto
    ''' </summary>
    Public Shared ReadOnly ActualizarProductoCommandProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(ActualizarProductoCommand),
            GetType(ICommand),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(Nothing))

    Public Property ActualizarProductoCommand As ICommand
        Get
            Return CType(GetValue(ActualizarProductoCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(ActualizarProductoCommandProperty, value)
        End Set
    End Property

    ''' <summary>
    ''' Indica si se permite editar la cantidad de oferta.
    ''' True para SeleccionProductos, False para SeleccionRegalos.
    ''' </summary>
    Public Shared ReadOnly PermitirCantidadOfertaProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(PermitirCantidadOferta),
            GetType(Boolean),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(True, AddressOf OnPermitirCantidadOfertaChanged))

    Public Property PermitirCantidadOferta As Boolean
        Get
            Return CBool(GetValue(PermitirCantidadOfertaProperty))
        End Get
        Set(value As Boolean)
            SetValue(PermitirCantidadOfertaProperty, value)
        End Set
    End Property

    Private Shared Sub OnPermitirCantidadOfertaChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim control = CType(d, SelectorLineasPlantillaVenta)
        control.UpdatePermitirCantidadOfertaVisibility()
    End Sub

    Private Sub UpdatePermitirCantidadOfertaVisibility()
        PermitirCantidadOfertaVisibility = If(PermitirCantidadOferta, Visibility.Visible, Visibility.Collapsed)
    End Sub

    ''' <summary>
    ''' Visibilidad calculada para la columna de cantidad oferta
    ''' </summary>
    Public Shared ReadOnly PermitirCantidadOfertaVisibilityProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(PermitirCantidadOfertaVisibility),
            GetType(Visibility),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(Visibility.Visible))

    Public Property PermitirCantidadOfertaVisibility As Visibility
        Get
            Return CType(GetValue(PermitirCantidadOfertaVisibilityProperty), Visibility)
        End Get
        Set(value As Visibility)
            SetValue(PermitirCantidadOfertaVisibilityProperty, value)
        End Set
    End Property

    ''' <summary>
    ''' Indica si se muestra el precio del producto.
    ''' True para mostrar el valor que le regalamos al cliente.
    ''' </summary>
    Public Shared ReadOnly MostrarPrecioProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(MostrarPrecio),
            GetType(Boolean),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(True, AddressOf OnMostrarPrecioChanged))

    Public Property MostrarPrecio As Boolean
        Get
            Return CBool(GetValue(MostrarPrecioProperty))
        End Get
        Set(value As Boolean)
            SetValue(MostrarPrecioProperty, value)
        End Set
    End Property

    Private Shared Sub OnMostrarPrecioChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim control = CType(d, SelectorLineasPlantillaVenta)
        control.UpdateMostrarPrecioVisibility()
    End Sub

    Private Sub UpdateMostrarPrecioVisibility()
        MostrarPrecioVisibility = If(MostrarPrecio, Visibility.Visible, Visibility.Collapsed)
    End Sub

    ''' <summary>
    ''' Visibilidad calculada para el precio
    ''' </summary>
    Public Shared ReadOnly MostrarPrecioVisibilityProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(MostrarPrecioVisibility),
            GetType(Visibility),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(Visibility.Visible))

    Public Property MostrarPrecioVisibility As Visibility
        Get
            Return CType(GetValue(MostrarPrecioVisibilityProperty), Visibility)
        End Get
        Set(value As Visibility)
            SetValue(MostrarPrecioVisibilityProperty, value)
        End Set
    End Property

    ''' <summary>
    ''' Colección filtrable para la BarraFiltro.
    ''' El ListView se vincula directamente a ListaFiltrable.Lista en XAML.
    ''' </summary>
    Public Shared ReadOnly ListaFiltrableProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(ListaFiltrable),
            GetType(ColeccionFiltrable),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(Nothing))

    Public Property ListaFiltrable As ColeccionFiltrable
        Get
            Return CType(GetValue(ListaFiltrableProperty), ColeccionFiltrable)
        End Get
        Set(value As ColeccionFiltrable)
            SetValue(ListaFiltrableProperty, value)
        End Set
    End Property

    ''' <summary>
    ''' Indica si se muestra la barra de filtro.
    ''' </summary>
    Public Shared ReadOnly MostrarBarraFiltroProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(MostrarBarraFiltro),
            GetType(Boolean),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(True, AddressOf OnMostrarBarraFiltroChanged))

    Public Property MostrarBarraFiltro As Boolean
        Get
            Return CBool(GetValue(MostrarBarraFiltroProperty))
        End Get
        Set(value As Boolean)
            SetValue(MostrarBarraFiltroProperty, value)
        End Set
    End Property

    Private Shared Sub OnMostrarBarraFiltroChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim control = CType(d, SelectorLineasPlantillaVenta)
        control.UpdateMostrarBarraFiltroVisibility()
    End Sub

    Private Sub UpdateMostrarBarraFiltroVisibility()
        MostrarBarraFiltroVisibility = If(MostrarBarraFiltro, Visibility.Visible, Visibility.Collapsed)
    End Sub

    ''' <summary>
    ''' Visibilidad calculada para la barra de filtro
    ''' </summary>
    Public Shared ReadOnly MostrarBarraFiltroVisibilityProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(MostrarBarraFiltroVisibility),
            GetType(Visibility),
            GetType(SelectorLineasPlantillaVenta),
            New PropertyMetadata(Visibility.Visible))

    Public Property MostrarBarraFiltroVisibility As Visibility
        Get
            Return CType(GetValue(MostrarBarraFiltroVisibilityProperty), Visibility)
        End Get
        Set(value As Visibility)
            SetValue(MostrarBarraFiltroVisibilityProperty, value)
        End Set
    End Property

#End Region

End Class
