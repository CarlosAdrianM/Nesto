Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.Data.Metadata.Edm
Imports System.Data.Objects
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Controls
'Imports Nesto.Models.Nesto.Models.EF


Public Class AlquileresViewModel
    Inherits ViewModelBase

    Private Shared DbContext As NestoEntities
    Dim mainModel As New Nesto.Models.MainModel

    Public Sub New()
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        DbContext = New NestoEntities
        colProductosAlquilerLista = New ObservableCollection(Of prdProductosAlquiler)(From c In DbContext.prdProductosAlquilerLista)
        bultos = 2
    End Sub


#Region "Datos Publicados"

    Private _AlquileresCollection As ObservableCollection(Of CabAlquileres)
    Public Property AlquileresCollection() As ObservableCollection(Of CabAlquileres)
        Get
            Return _AlquileresCollection
        End Get
        Set(value As ObservableCollection(Of CabAlquileres))
            _AlquileresCollection = value
            OnPropertyChanged("AlquileresCollection")
        End Set
    End Property

    Private _LineaSeleccionada As CabAlquileres
    Public Property LineaSeleccionada As CabAlquileres
        Get
            Return _LineaSeleccionada
        End Get
        Set(value As CabAlquileres)
            _LineaSeleccionada = value
            OnPropertyChanged("LineaSeleccionada")
        End Set
    End Property

    Private _colExtractoInmovilizado As ObservableCollection(Of ExtractoInmovilizado)
    Public Property colExtractoInmovilizado() As ObservableCollection(Of ExtractoInmovilizado)
        Get
            Return _colExtractoInmovilizado
        End Get
        Set(value As ObservableCollection(Of ExtractoInmovilizado))
            _colExtractoInmovilizado = value
            OnPropertyChanged("colExtractoInmovilizado")
        End Set
    End Property

    Private _PestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _PestañaSeleccionada
        End Get
        Set(value As TabItem)
            If LineaSeleccionada IsNot Nothing Then
                If value.Name = "tabInmovilizados" Then
                    colExtractoInmovilizado = New ObservableCollection(Of ExtractoInmovilizado)(From x In DbContext.ExtractoInmovilizado Where x.Empresa = LineaSeleccionada.Empresa And x.Número = LineaSeleccionada.Inmovilizado Order By x.Fecha)
                ElseIf value.Name = "tabMovimientos" Then
                    colMovimientos = New ObservableCollection(Of LinPedidoVta)(From x In DbContext.LinPedidoVta Where x.Empresa = LineaSeleccionada.Empresa And x.Número = LineaSeleccionada.CabPedidoVta Order By x.Nº_Orden)
                ElseIf value.Name = "tabCompra" Then
                    colCompra = New ObservableCollection(Of LinPedidoCmp)(From x In DbContext.LinPedidoCmp Where x.Producto = LineaSeleccionada.Producto And x.NumSerie = LineaSeleccionada.NumeroSerie Order By x.NºOrden)
                End If
            End If
            _PestañaSeleccionada = value
            OnPropertyChanged("PestañaSeleccionada")
        End Set
    End Property

    Private _colMovimientos As ObservableCollection(Of LinPedidoVta)
    Public Property colMovimientos As ObservableCollection(Of LinPedidoVta)
        Get
            Return _colMovimientos
        End Get
        Set(value As ObservableCollection(Of LinPedidoVta))
            _colMovimientos = value
            OnPropertyChanged("colMovimientos")
        End Set
    End Property

    Private Property _colProductosAlquilerLista As ObservableCollection(Of prdProductosAlquiler)
    Public Property colProductosAlquilerLista As ObservableCollection(Of prdProductosAlquiler)
        Get
            Return _colProductosAlquilerLista
        End Get
        Set(value As ObservableCollection(Of prdProductosAlquiler))
            _colProductosAlquilerLista = value
            OnPropertyChanged("colProductosAlquilerLista")
        End Set
    End Property

    Private Property _ProductoSeleccionado As prdProductosAlquiler
    Public Property ProductoSeleccionado As prdProductosAlquiler
        Get
            Return _ProductoSeleccionado
        End Get
        Set(value As prdProductosAlquiler)
            _ProductoSeleccionado = value
            If Not IsNothing(ProductoSeleccionado) Then
                AlquileresCollection = New ObservableCollection(Of CabAlquileres)(From c In DbContext.CabAlquileres Where c.Producto = ProductoSeleccionado.Número Order By c.NumeroSerie)
            End If

            colExtractoInmovilizado = Nothing
            colMovimientos = Nothing
            OnPropertyChanged("ProductoSeleccionado")
        End Set
    End Property

    Private _colCompra As ObservableCollection(Of LinPedidoCmp)
    Public Property colCompra As ObservableCollection(Of LinPedidoCmp)
        Get
            Return _colCompra
        End Get
        Set(value As ObservableCollection(Of LinPedidoCmp))
            _colCompra = value
            OnPropertyChanged("colCompra")
        End Set
    End Property

    Private Property _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            _mensajeError = value
            OnPropertyChanged("mensajeError")
        End Set
    End Property

    Private Property _bultos As Integer
    Public Property bultos As Integer
        Get
            Return _bultos
        End Get
        Set(value As Integer)
            _bultos = value
        End Set
    End Property

#End Region

#Region "Comandos"
    Private _cmdGuardar As ICommand
    Public ReadOnly Property cmdGuardar() As ICommand
        Get
            If _cmdGuardar Is Nothing Then
                _cmdGuardar = New RelayCommand(AddressOf Guardar, AddressOf CanGuardar)
            End If
            Return _cmdGuardar
        End Get
    End Property
    Private Function CanGuardar(ByVal param As Object) As Boolean

        'For Each alquiler In DbContext.CabAlquileres
        '    If alquiler.EntityState <> EntityState.Unchanged Then
        '        Return True
        '    End If
        'Next
        'Return False

        Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)

        Return changes.Any



    End Function
    Private Sub Guardar(ByVal param As Object)
        Try
            DbContext.SaveChanges()
            colProductosAlquilerLista = New ObservableCollection(Of prdProductosAlquiler)(From c In DbContext.prdProductosAlquilerLista)
            ProductoSeleccionado = colProductosAlquilerLista.LastOrDefault
            mensajeError = ""
        Catch ex As Exception
            mensajeError = ex.InnerException.Message
        End Try
    End Sub

    Private _cmdAñadir As ICommand
    Public ReadOnly Property cmdAñadir() As ICommand
        Get
            If _cmdAñadir Is Nothing Then
                _cmdAñadir = New RelayCommand(AddressOf Añadir, AddressOf CanAñadir)
            End If
            Return _cmdAñadir
        End Get
    End Property
    Private Function CanAñadir(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub Añadir(ByVal param As Object)
        Dim alqui As New CabAlquileres
        alqui.Empresa = "1"
        If ProductoSeleccionado IsNot Nothing Then
            alqui.Producto = ProductoSeleccionado.Número
        ElseIf LineaSeleccionada IsNot Nothing Then
            alqui.Producto = LineaSeleccionada.Producto
        End If
        DbContext.AddToCabAlquileres(alqui)
        AlquileresCollection.Add(alqui)
    End Sub

    Private _cmdBorrar As ICommand
    Public ReadOnly Property cmdBorrar() As ICommand
        Get
            If _cmdBorrar Is Nothing Then
                _cmdBorrar = New RelayCommand(AddressOf Borrar, AddressOf canBorrar)
            End If
            Return _cmdBorrar
        End Get
    End Property
    Private Function canBorrar(ByVal param As Object) As Boolean
        Return LineaSeleccionada IsNot Nothing
    End Function
    Private Sub Borrar(ByVal param As Object)
        DbContext.CabAlquileres.DeleteObject(LineaSeleccionada)
        AlquileresCollection.Remove(LineaSeleccionada)
    End Sub

    Private _cmdUpdNumeroSerie As ICommand
    Public ReadOnly Property cmdUpdNumeroSerie() As ICommand
        Get
            If _cmdUpdNumeroSerie Is Nothing Then
                _cmdUpdNumeroSerie = New RelayCommand(AddressOf UpdNumeroSerie, AddressOf canUpdNumeroSerie)
            End If
            Return _cmdUpdNumeroSerie
        End Get
    End Property
    Private Function canUpdNumeroSerie(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub UpdNumeroSerie(ByVal param As Object)
        MessageBox.Show("Pasa")
    End Sub

    Private _cmdImprimirEtiquetaMaquina As ICommand
    Public ReadOnly Property cmdImprimirEtiquetaMaquina() As ICommand
        Get
            If _cmdImprimirEtiquetaMaquina Is Nothing Then
                _cmdImprimirEtiquetaMaquina = New RelayCommand(AddressOf ImprimirEtiquetaMaquina, AddressOf canImprimirEtiquetaMaquina)
            End If
            Return _cmdImprimirEtiquetaMaquina
        End Get
    End Property
    Private Function canImprimirEtiquetaMaquina(ByVal param As Object) As Boolean
        Return LineaSeleccionada IsNot Nothing
    End Function
    Private Sub ImprimirEtiquetaMaquina(ByVal param As Object)

        Dim puerto As String = mainModel.leerParametro(LineaSeleccionada.Empresa, "ImpresoraBolsas")

        Dim objFSO
        Dim objStream
        objFSO = CreateObject("Scripting.FileSystemObject")
        objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  

        Try

            objStream.Writeline("I8,A,034")
            objStream.Writeline("N")
            objStream.Writeline("A50,500,3,4,3,2,N,""  UnióN LáseR""")
            objStream.Writeline("A140,500,3,4,1,1,R,""     Aparatología Estética     """)
            objStream.Writeline("A190,10,0,5,1,1,N,""" + ProductoSeleccionado.Nombre + """")
            objStream.Writeline("A190,190,0,3,1,1,N,""N/S: " + LineaSeleccionada.NumeroSerie + """")
            objStream.Writeline("B190,90,0,3,2,7,70,N,""" + LineaSeleccionada.NumeroSerie + """")
            objStream.Writeline("A190,250,0,3,1,1,N,""Fecha Etiquetado: " + Now.ToShortDateString + """")
            objStream.Writeline("A190,300,0,3,1,1,N,""Revisada por: """)
            objStream.Writeline("A190,400,0,3,1,1,N,""Observaciones: """)
            objStream.Writeline("P1")
            objStream.Writeline("")

        Catch ex As Exception
            mensajeError = ex.InnerException.Message
        Finally
            objStream.Close()
            objFSO = Nothing
            objStream = Nothing
        End Try
    End Sub

    Private _cmdImprimirEtiquetaPedido As ICommand
    Public ReadOnly Property cmdImprimirEtiquetaPedido() As ICommand
        Get
            If _cmdImprimirEtiquetaPedido Is Nothing Then
                _cmdImprimirEtiquetaPedido = New RelayCommand(AddressOf ImprimirEtiquetaPedido, AddressOf canImprimirEtiquetaPedido)
            End If
            Return _cmdImprimirEtiquetaPedido
        End Get
    End Property
    Private Function canImprimirEtiquetaPedido(ByVal param As Object) As Boolean
        Return LineaSeleccionada IsNot Nothing AndAlso LineaSeleccionada.CabPedidoVta IsNot Nothing
    End Function
    Private Sub ImprimirEtiquetaPedido(ByVal param As Object)

        Dim puerto As String = mainModel.leerParametro(LineaSeleccionada.Empresa, "ImpresoraBolsas")

        Dim objFSO
        Dim objStream
        objFSO = CreateObject("Scripting.FileSystemObject")
        objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  
        Dim i As Integer

        Try
            For i = 1 To bultos
                objStream.Writeline("I8,A,034")
                objStream.Writeline("N")
                objStream.Writeline("A50,500,3,4,3,2,N,""  UnióN LáseR""")
                objStream.Writeline("A140,500,3,4,1,1,R,""     Aparatología Estética     """)
                objStream.Writeline("A190,10,0,4,1,1,N,""" + LineaSeleccionada.Clientes.Nombre.Trim + """")
                objStream.Writeline("A190,60,0,4,1,1,N,""" + LineaSeleccionada.Clientes.Dirección + """")
                objStream.Writeline("A190,110,0,4,1,1,N,""" + LineaSeleccionada.Clientes.CodPostal.Trim + " " + LineaSeleccionada.Clientes.Población.Trim + """")
                objStream.Writeline("A190,160,0,4,1,1,N,""" + LineaSeleccionada.Clientes.Provincia.Trim + "")
                objStream.Writeline("A190,210,0,4,1,1,N,""Bulto: " + i.ToString + "/" + bultos.ToString + "")
                objStream.Writeline("B190,260,0,3,2,7,70,N,""" + LineaSeleccionada.CabPedidoVta.ToString + """")
                objStream.Writeline("P1")
                objStream.Writeline("")
            Next

        Catch ex As Exception
            mensajeError = ex.InnerException.Message
        Finally
            objStream.Close()
            objFSO = Nothing
            objStream = Nothing
        End Try
    End Sub


#End Region

#Region "Funciones de ayuda"

#End Region
End Class



