Imports System.Collections.ObjectModel
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports Unity

''' <summary>
''' ViewModel para mostrar los errores de facturación de rutas
''' </summary>
Public Class ErroresFacturacionRutasPopupViewModel
    Inherits BindableBase
    Implements IDialogAware

    Private ReadOnly container As IUnityContainer

#Region "Constructor"

    Public Sub New(container As IUnityContainer)
        Me.container = container
        _errores = New ObservableCollection(Of PedidoConErrorDTO)()
        _cmdAbrirPedido = New DelegateCommand(Of PedidoConErrorDTO)(AddressOf OnAbrirPedido, AddressOf CanAbrirPedido)
    End Sub

#End Region

#Region "Properties"

    Private _errores As ObservableCollection(Of PedidoConErrorDTO)
    Public Property Errores As ObservableCollection(Of PedidoConErrorDTO)
        Get
            Return _errores
        End Get
        Set(value As ObservableCollection(Of PedidoConErrorDTO))
            If SetProperty(_errores, value) Then
                RaisePropertyChanged(NameOf(NumeroErrores))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Número total de errores (para mostrar en el resumen)
    ''' </summary>
    Public ReadOnly Property NumeroErrores As Integer
        Get
            Dim count = If(_errores Is Nothing, 0, _errores.Count)
            System.Diagnostics.Debug.WriteLine($"NumeroErrores getter - Devolviendo: {count}")
            Return count
        End Get
    End Property

#End Region

#Region "Commands"

    Private _cmdAbrirPedido As DelegateCommand(Of PedidoConErrorDTO)
    Public Property cmdAbrirPedido As DelegateCommand(Of PedidoConErrorDTO)
        Get
            Return _cmdAbrirPedido
        End Get
        Private Set(value As DelegateCommand(Of PedidoConErrorDTO))
            Dim unused = SetProperty(_cmdAbrirPedido, value)
        End Set
    End Property

    Private Function CanAbrirPedido(pedidoError As PedidoConErrorDTO) As Boolean
        Return Not IsNothing(pedidoError) AndAlso pedidoError.NumeroPedido > 0
    End Function

    Private Sub OnAbrirPedido(pedidoError As PedidoConErrorDTO)
        If IsNothing(pedidoError) OrElse String.IsNullOrWhiteSpace(pedidoError.Empresa) Then
            Return
        End If

        Try
            ' Usar el mismo código que comisiones para abrir el pedido
            PedidoVentaViewModel.CargarPedido(pedidoError.Empresa, pedidoError.NumeroPedido, container)
        Catch ex As Exception
            ' Log del error si es necesario
            System.Diagnostics.Debug.WriteLine(String.Format("Error al abrir pedido {0}: {1}", pedidoError.NumeroPedido, ex.Message))
        End Try
    End Sub

#End Region

#Region "Public Methods"

    ''' <summary>
    ''' Carga la lista de errores en el ViewModel
    ''' </summary>
    Public Sub CargarErrores(errores As List(Of PedidoConErrorDTO))
        System.Diagnostics.Debug.WriteLine($"CargarErrores - Errores recibidos: {If(errores Is Nothing, "Nothing", errores.Count.ToString())}")

        If IsNothing(errores) Then
            System.Diagnostics.Debug.WriteLine("CargarErrores - Errores es Nothing, saliendo")
            Return
        End If

        System.Diagnostics.Debug.WriteLine($"CargarErrores - Colección actual tiene {errores.Count} elementos")

        ' IMPORTANTE: Crear una NUEVA colección en lugar de Clear() + Add
        ' Esto evita problemas si OnDialogOpened se llama múltiples veces
        Dim nuevaColeccion As New ObservableCollection(Of PedidoConErrorDTO)()

        For Each errorItem In errores
            System.Diagnostics.Debug.WriteLine($"CargarErrores - Agregando pedido {errorItem.NumeroPedido}: {errorItem.MensajeError}")
            nuevaColeccion.Add(errorItem)
        Next

        ' Reemplazar la colección completa (el setter notificará automáticamente)
        System.Diagnostics.Debug.WriteLine($"CargarErrores - Asignando nueva colección con {nuevaColeccion.Count} elementos")
        Me.Errores = nuevaColeccion

        System.Diagnostics.Debug.WriteLine($"CargarErrores - Colección final tiene {Me.Errores.Count} elementos")
        System.Diagnostics.Debug.WriteLine($"CargarErrores - NumeroErrores: {NumeroErrores}")
    End Sub

#End Region

#Region "IDialogAware Implementation"

    Public ReadOnly Property Title As String Implements IDialogAware.Title
        Get
            Return "Errores en Facturación de Rutas"
        End Get
    End Property

    Public Event RequestClose As Action(Of IDialogResult) Implements IDialogAware.RequestClose

    Public Function CanCloseDialog() As Boolean Implements IDialogAware.CanCloseDialog
        Return True
    End Function

    Public Sub OnDialogClosed() Implements IDialogAware.OnDialogClosed
        ' Limpieza si es necesaria
    End Sub

    Public Sub OnDialogOpened(parameters As IDialogParameters) Implements IDialogAware.OnDialogOpened
        ' Obtener los errores desde los parámetros del diálogo
        System.Diagnostics.Debug.WriteLine($"OnDialogOpened - parameters is Nothing: {parameters Is Nothing}")

        If parameters IsNot Nothing AndAlso parameters.ContainsKey("errores") Then
            System.Diagnostics.Debug.WriteLine("OnDialogOpened - Clave 'errores' encontrada")
            Dim errores = parameters.GetValue(Of List(Of PedidoConErrorDTO))("errores")
            System.Diagnostics.Debug.WriteLine($"OnDialogOpened - Errores recibidos: {If(errores Is Nothing, 0, errores.Count)}")
            CargarErrores(errores)
        Else
            System.Diagnostics.Debug.WriteLine("OnDialogOpened - Clave 'errores' NO encontrada o parameters es Nothing")
        End If
    End Sub

    ''' <summary>
    ''' Cierra el diálogo
    ''' </summary>
    Public Sub Cerrar()
        RaiseEvent RequestClose(New DialogResult(ButtonResult.OK))
    End Sub

#End Region

End Class
