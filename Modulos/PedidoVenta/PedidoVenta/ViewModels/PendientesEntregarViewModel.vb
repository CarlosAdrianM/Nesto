Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared

''' <summary>
''' Nesto#552 (sugerencia 717, Manuel): «Pendientes de entregar», todas las líneas de pedido pendientes de entregar de
''' todos los almacenes. Solo lectura: filtros de almacén, producto y vendedor (los aplica la API), la barra de filtro
''' de siempre sobre lo cargado, exportar a Excel y doble clic para abrir el pedido.
''' </summary>
Public Class PendientesEntregarViewModel
    Inherits ObservableObject

    Public Const TODOS_LOS_ALMACENES As String = "(Todos)"

    Private ReadOnly servicio As IPedidoVentaService
    Private ReadOnly abridorPedidos As IAbridorPedidos
    Private ReadOnly dialogService As IServicioDialogos

    Public Sub New(servicio As IPedidoVentaService, abridorPedidos As IAbridorPedidos, dialogService As IServicioDialogos)
        Me.servicio = servicio
        Me.abridorPedidos = abridorPedidos
        Me.dialogService = dialogService

        Titulo = "Pendientes de entregar"
        Almacenes = New List(Of String) From {TODOS_LOS_ALMACENES}
        Almacenes.AddRange(Constantes.Almacenes.ALMACENES_STOCK)
        Almacen = TODOS_LOS_ALMACENES
        _listaLineas = New ColeccionFiltrable() With {.SeleccionarPrimerElemento = False}

        CargarCommand = New AsyncRelayCommand(AddressOf CargarAsync)
        ExportarExcelCommand = New AsyncRelayCommand(AddressOf ExportarExcelAsync)
        AbrirPedidoCommand = New RelayCommand(Of Object)(AddressOf AbrirPedido)
    End Sub

#Region "Propiedades"
    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            SetProperty(_titulo, value)
        End Set
    End Property

    Public Property Empresa As String = Constantes.Empresas.EMPRESA_DEFECTO

    Public ReadOnly Property Almacenes As List(Of String)

    Private _almacen As String
    Public Property Almacen As String
        Get
            Return _almacen
        End Get
        Set(value As String)
            SetProperty(_almacen, value)
        End Set
    End Property

    Private _producto As String
    Public Property Producto As String
        Get
            Return _producto
        End Get
        Set(value As String)
            SetProperty(_producto, value)
        End Set
    End Property

    Private _vendedor As String
    Public Property Vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            SetProperty(_vendedor, value)
        End Set
    End Property

    Private WithEvents _listaLineas As ColeccionFiltrable
    Public ReadOnly Property ListaLineas As ColeccionFiltrable
        Get
            Return _listaLineas
        End Get
    End Property

    Private _estaOcupado As Boolean
    Public Property EstaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(value As Boolean)
            SetProperty(_estaOcupado, value)
        End Set
    End Property

    ''' <summary>Las líneas que se ven (con el filtro de texto aplicado).</summary>
    Public ReadOnly Property LineasVisibles As IEnumerable(Of LineaPendienteEntregar)
        Get
            If ListaLineas?.Lista Is Nothing Then
                Return Enumerable.Empty(Of LineaPendienteEntregar)()
            End If
            Return ListaLineas.Lista.OfType(Of LineaPendienteEntregar)()
        End Get
    End Property

    Public ReadOnly Property NumeroLineas As Integer
        Get
            Return LineasVisibles.Count()
        End Get
    End Property

    Public ReadOnly Property TotalBruto As Decimal
        Get
            Return LineasVisibles.Sum(Function(l) l.Bruto)
        End Get
    End Property

    ''' <summary>Abre el Excel descargado. Se sustituye en las pruebas.</summary>
    Friend Property AbrirFichero As Action(Of String) = Sub(ruta) Process.Start(New ProcessStartInfo(ruta) With {.UseShellExecute = True})
#End Region

#Region "Comandos"
    Public ReadOnly Property CargarCommand As AsyncRelayCommand
    Public ReadOnly Property ExportarExcelCommand As AsyncRelayCommand
    Public ReadOnly Property AbrirPedidoCommand As RelayCommand(Of Object)

    Private ReadOnly Property AlmacenFiltro As String
        Get
            Return If(String.IsNullOrWhiteSpace(Almacen) OrElse Almacen = TODOS_LOS_ALMACENES, Nothing, Almacen.Trim())
        End Get
    End Property

    Private Shared Function Limpio(texto As String) As String
        Return If(String.IsNullOrWhiteSpace(texto), Nothing, texto.Trim())
    End Function

    Public Async Function CargarAsync() As Task
        Try
            EstaOcupado = True
            Dim lineas As List(Of LineaPendienteEntregar) = Await servicio.LeerPendientesEntregar(Empresa, AlmacenFiltro, Limpio(Producto), Limpio(Vendedor))
            Dim filtro As String = ListaLineas.Filtro
            ListaLineas.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(If(lineas, New List(Of LineaPendienteEntregar)))
            If Not String.IsNullOrEmpty(filtro) Then
                ListaLineas.Filtro = filtro
            End If
            OnPropertyChanged(NameOf(NumeroLineas))
            OnPropertyChanged(NameOf(TotalBruto))
        Catch ex As Exception
            dialogService.ShowError("No se han podido cargar las líneas pendientes de entregar: " & ex.Message)
        Finally
            EstaOcupado = False
        End Try
    End Function

    Public Async Function ExportarExcelAsync() As Task
        Try
            EstaOcupado = True
            Dim excel As Byte() = Await servicio.DescargarPendientesEntregarExcel(Empresa, AlmacenFiltro, Limpio(Producto), Limpio(Vendedor))
            Dim ruta As String = Path.Combine(Path.GetTempPath(), $"PendientesEntregar_{Date.Now:yyyyMMdd_HHmmss}.xlsx")
            File.WriteAllBytes(ruta, excel)
            AbrirFichero.Invoke(ruta)
        Catch ex As Exception
            dialogService.ShowError("No se ha podido exportar a Excel: " & ex.Message)
        Finally
            EstaOcupado = False
        End Try
    End Function

    Private Sub AbrirPedido(arg As Object)
        Dim linea = TryCast(arg, LineaPendienteEntregar)
        If linea Is Nothing Then
            linea = TryCast(ListaLineas?.ElementoSeleccionado, LineaPendienteEntregar)
        End If
        If linea Is Nothing Then
            Return
        End If
        abridorPedidos.Abrir(Empresa, linea.Pedido)
    End Sub
#End Region

    Private Sub ListaLineas_PropertyChanged(sender As Object, e As PropertyChangedEventArgs) Handles _listaLineas.PropertyChanged
        If e.PropertyName = NameOf(ColeccionFiltrable.Lista) Then
            OnPropertyChanged(NameOf(NumeroLineas))
            OnPropertyChanged(NameOf(TotalBruto))
        End If
    End Sub
End Class
