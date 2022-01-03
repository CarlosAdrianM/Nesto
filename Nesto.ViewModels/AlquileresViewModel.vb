Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Controls
Imports Prism.Mvvm
Imports Prism.Commands
Imports Nesto.Models.Nesto.Models
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs
Imports Nesto.Contratos
Imports System.IO
Imports System.Text
Imports Nesto.Infrastructure.Shared
Imports Nesto.Infrastructure.Contracts

Public Class AlquileresViewModel
    Inherits BindableBase

    Private Shared DbContext As NestoEntities
    Private ReadOnly dialogService As IDialogService
    Private ReadOnly configuracion As IConfiguracion

    Public Sub New(dialogService As IDialogService, configuracion As IConfiguracion)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        DbContext = New NestoEntities
        colProductosAlquilerLista = New ObservableCollection(Of prdProductosAlquiler)(From c In DbContext.prdProductosAlquilerLista)
        bultos = 2

        ' Comandos Prism
        cmdIntercambiarNumeroSerie = New DelegateCommand(Of Object)(AddressOf OnIntercambiarNumeroSerie, AddressOf CanIntercambiarNumeroSerie)
        cmdInicializarAlquiler = New DelegateCommand(Of Object)(AddressOf OnInicializarAlquiler, AddressOf CanInicializarAlquiler)

        Titulo = "Alquileres"

        Me.dialogService = dialogService
        Me.configuracion = configuracion
    End Sub


#Region "Datos Publicados"

    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            SetProperty(_titulo, value)
        End Set
    End Property

    Private _AlquileresCollection As ObservableCollection(Of CabAlquileres)
    Public Property AlquileresCollection() As ObservableCollection(Of CabAlquileres)
        Get
            Return _AlquileresCollection
        End Get
        Set(value As ObservableCollection(Of CabAlquileres))
            SetProperty(_AlquileresCollection, value)
        End Set
    End Property

    Private _LineaSeleccionada As CabAlquileres
    Public Property LineaSeleccionada As CabAlquileres
        Get
            Return _LineaSeleccionada
        End Get
        Set(value As CabAlquileres)
            SetProperty(_LineaSeleccionada, value)
            cmdIntercambiarNumeroSerie.RaiseCanExecuteChanged()
            cmdInicializarAlquiler.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _colExtractoInmovilizado As ObservableCollection(Of ExtractoInmovilizado)
    Public Property colExtractoInmovilizado() As ObservableCollection(Of ExtractoInmovilizado)
        Get
            Return _colExtractoInmovilizado
        End Get
        Set(value As ObservableCollection(Of ExtractoInmovilizado))
            SetProperty(_colExtractoInmovilizado, value)
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
            SetProperty(_PestañaSeleccionada, value)
        End Set
    End Property

    Private _colMovimientos As ObservableCollection(Of LinPedidoVta)
    Public Property colMovimientos As ObservableCollection(Of LinPedidoVta)
        Get
            Return _colMovimientos
        End Get
        Set(value As ObservableCollection(Of LinPedidoVta))
            SetProperty(_colMovimientos, value)
        End Set
    End Property

    Private _colProductosAlquilerLista As ObservableCollection(Of prdProductosAlquiler)
    Public Property colProductosAlquilerLista As ObservableCollection(Of prdProductosAlquiler)
        Get
            Return _colProductosAlquilerLista
        End Get
        Set(value As ObservableCollection(Of prdProductosAlquiler))
            SetProperty(_colProductosAlquilerLista, value)
        End Set
    End Property

    Private _ProductoSeleccionado As prdProductosAlquiler
    Public Property ProductoSeleccionado As prdProductosAlquiler
        Get
            Return _ProductoSeleccionado
        End Get
        Set(value As prdProductosAlquiler)
            SetProperty(_ProductoSeleccionado, value)
            If Not IsNothing(ProductoSeleccionado) Then
                AlquileresCollection = New ObservableCollection(Of CabAlquileres)(From c In DbContext.CabAlquileres Where c.Producto = ProductoSeleccionado.Número Order By c.NumeroSerie)
            End If

            colExtractoInmovilizado = Nothing
            colMovimientos = Nothing
        End Set
    End Property

    Private _colCompra As ObservableCollection(Of LinPedidoCmp)
    Public Property colCompra As ObservableCollection(Of LinPedidoCmp)
        Get
            Return _colCompra
        End Get
        Set(value As ObservableCollection(Of LinPedidoCmp))
            SetProperty(_colCompra, value)
        End Set
    End Property

    Private _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            SetProperty(_mensajeError, value)
        End Set
    End Property

    Private _bultos As Integer
    Public Property bultos As Integer
        Get
            Return _bultos
        End Get
        Set(value As Integer)
            SetProperty(_bultos, value)
        End Set
    End Property

    Private _numeroSerieIntercambiar As String
    Public Property numeroSerieIntercambiar() As String
        Get
            Return _numeroSerieIntercambiar
        End Get
        Set(ByVal value As String)
            SetProperty(_numeroSerieIntercambiar, value)
            cmdIntercambiarNumeroSerie.RaiseCanExecuteChanged()
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
        Return DbContext.ChangeTracker.HasChanges()
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
        DbContext.CabAlquileres.Add(alqui)
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
        DbContext.CabAlquileres.Remove(LineaSeleccionada)
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
    Private Async Sub ImprimirEtiquetaMaquina(ByVal param As Object)

        Dim puerto As String = Await configuracion.leerParametro(LineaSeleccionada.Empresa, Parametros.Claves.ImpresoraCodBarras)

        'Dim objFSO
        'Dim objStream
        'objFSO = CreateObject("Scripting.FileSystemObject")
        'objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  

        Try
            Dim builder As New StringBuilder
            builder.AppendLine("I8,A,034")
            builder.AppendLine("N")
            builder.AppendLine("A50,500,3,4,3,2,N,""  UnióN LáseR""")
            builder.AppendLine("A140,500,3,4,1,1,R,""     Aparatología Estética     """)
            builder.AppendLine("A190,10,0,5,1,1,N,""" + ProductoSeleccionado.Nombre + """")
            builder.AppendLine("A190,190,0,3,1,1,N,""N/S: " + LineaSeleccionada.NumeroSerie + """")
            builder.AppendLine("B190,90,0,3,2,7,70,N,""" + LineaSeleccionada.NumeroSerie + """")
            builder.AppendLine("A190,250,0,3,1,1,N,""Fecha Etiquetado: " + Now.ToShortDateString + """")
            builder.AppendLine("A190,300,0,3,1,1,N,""Revisada por: """)
            builder.AppendLine("A190,400,0,3,1,1,N,""Observaciones: """)
            builder.AppendLine("P1")
            builder.AppendLine("")
            RawPrinterHelper.SendStringToPrinter(puerto, builder.ToString)
        Catch ex As Exception
            mensajeError = ex.InnerException.Message
            dialogService.ShowError(mensajeError)
            'Finally
            '    objStream.Close()
            '    objFSO = Nothing
            '    objStream = Nothing
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
    Private Async Sub ImprimirEtiquetaPedido(ByVal param As Object)

        Dim puerto As String = Await configuracion.leerParametro(LineaSeleccionada.Empresa, Parametros.Claves.ImpresoraAgencia)

        'Dim objFSO
        'Dim objStream
        'objFSO = CreateObject("Scripting.FileSystemObject")
        'objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  
        Dim i As Integer

        Try
            Dim builder As New StringBuilder
            For i = 1 To bultos
                builder.AppendLine("I8,A,034")
                builder.AppendLine("N")
                builder.AppendLine("A50,500,3,4,3,2,N,""  UnióN LáseR""")
                builder.AppendLine("A140,500,3,4,1,1,R,""     Aparatología Estética     """)
                builder.AppendLine("A190,10,0,4,1,1,N,""" + LineaSeleccionada.Clientes.Nombre.Trim + """")
                builder.AppendLine("A190,60,0,4,1,1,N,""" + LineaSeleccionada.Clientes.Dirección + """")
                builder.AppendLine("A190,110,0,4,1,1,N,""" + LineaSeleccionada.Clientes.CodPostal.Trim + " " + LineaSeleccionada.Clientes.Población.Trim + """")
                builder.AppendLine("A190,160,0,4,1,1,N,""" + LineaSeleccionada.Clientes.Provincia.Trim + "")
                builder.AppendLine("A190,210,0,4,1,1,N,""Bulto: " + i.ToString + "/" + bultos.ToString + "")
                builder.AppendLine("B190,260,0,3,2,7,70,N,""" + LineaSeleccionada.CabPedidoVta.ToString + """")
                builder.AppendLine("P1")
                builder.AppendLine("")
            Next
            RawPrinterHelper.SendStringToPrinter(puerto, builder.ToString)
        Catch ex As Exception
            mensajeError = ex.InnerException.Message
            'Finally
            '    objStream.Close()
            '    objFSO = Nothing
            '    objStream = Nothing
        End Try
    End Sub

    Private _cmdIntercambiarNumeroSerie As DelegateCommand(Of Object)
    Public Property cmdIntercambiarNumeroSerie As DelegateCommand(Of Object)
        Get
            Return _cmdIntercambiarNumeroSerie
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdIntercambiarNumeroSerie = value
        End Set
    End Property
    Private Function CanIntercambiarNumeroSerie(arg As Object) As Boolean
        Return Not IsNothing(LineaSeleccionada) AndAlso Not IsNothing(numeroSerieIntercambiar) AndAlso numeroSerieIntercambiar.Trim.Length > 0
    End Function
    Private Sub OnIntercambiarNumeroSerie(arg As Object)
        Dim alquiler As CabAlquileres = (From a In AlquileresCollection Where a.NumeroSerie = numeroSerieIntercambiar).SingleOrDefault

        ' Comprobamos que no sea el mismo alquiler el que tenga ese nº de serie
        If LineaSeleccionada.NumeroSerie.Trim = numeroSerieIntercambiar.Trim Then
            dialogService.ShowError("El número de serie origen es el mismo que el destino")
            Return
        End If

        ' Comprobamos que exista el alquiler
        If IsNothing(alquiler) Then
            dialogService.ShowError("No se encuentra el producto con número de serie " + numeroSerieIntercambiar)
            Return
        End If

        ' Procedemos al cambio
        Try
            Dim numeroSerieIntermedio As String = LineaSeleccionada.NumeroSerie
            LineaSeleccionada.NumeroSerie = numeroSerieIntercambiar.Trim
            alquiler.NumeroSerie = numeroSerieIntermedio.Trim
            DbContext.SaveChanges()
            dialogService.ShowNotification("Números de serie actualizados correctamente")
        Catch ex As Exception
            dialogService.ShowError("No se pudo realizar el cambio de número de serie")
        End Try

    End Sub

    Private _cmdInicializarAlquiler As DelegateCommand(Of Object)
    Public Property cmdInicializarAlquiler As DelegateCommand(Of Object)
        Get
            Return _cmdInicializarAlquiler
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdInicializarAlquiler = value
        End Set
    End Property
    Private Function CanInicializarAlquiler(arg As Object) As Boolean
        Return Not IsNothing(LineaSeleccionada)
    End Function
    Private Sub OnInicializarAlquiler(arg As Object)
        Dim continuar As Boolean
        dialogService.ShowConfirmation("Inicializar", "¿Desea inicializar los campos de este alquiler?", Sub(r)
                                                                                                             continuar = (r.Result = ButtonResult.OK)
                                                                                                         End Sub)
        If Not continuar OrElse IsNothing(LineaSeleccionada) Then
            Return
        End If

        With LineaSeleccionada
            .Cliente = Nothing
            .Contacto = Nothing
            .FechaEntrega = Nothing
            .FechaSeñal = Nothing
            .ImporteSeñal = Nothing
            .Importe = Nothing
            .CabPedidoVta = Nothing
        End With

    End Sub

#End Region

#Region "Funciones de ayuda"

#End Region
End Class