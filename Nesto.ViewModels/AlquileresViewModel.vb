Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
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
Imports Nesto.Infrastructure.Models.Alquileres
Imports Unity

Public Class AlquileresViewModel
    Inherits BindableBase

    Private ReadOnly dialogService As IDialogService
    Private ReadOnly configuracion As IConfiguracion
    ' Nesto#340 Fase 1C: la lectura de productos, detalle y el grid principal ya no usan EF, sino el API.
    Private ReadOnly _productosAlquilerService As IProductosAlquilerService
    ' Nesto#340 Fase 1C.3: dirty flag que sustituye a DbContext.ChangeTracker.HasChanges().
    Private _hayCambios As Boolean

    Public Sub New(dialogService As IDialogService, configuracion As IConfiguracion, container As IUnityContainer)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Me.dialogService = dialogService
        Me.configuracion = configuracion
        Dim servicioAutenticacion = container.Resolve(Of IServicioAutenticacion)()
        _productosAlquilerService = New ProductosAlquilerService(configuracion, servicioAutenticacion)
        Inicializar()
    End Sub

    ' Constructor para tests: permite inyectar un IProductosAlquilerService fake.
    Public Sub New(dialogService As IDialogService, configuracion As IConfiguracion, productosAlquilerService As IProductosAlquilerService)
        Me.dialogService = dialogService
        Me.configuracion = configuracion
        _productosAlquilerService = productosAlquilerService
        Inicializar()
    End Sub

    Private Sub Inicializar()
        bultos = 2

        ' Comandos Prism
        cmdIntercambiarNumeroSerie = New DelegateCommand(Of Object)(AddressOf OnIntercambiarNumeroSerie, AddressOf CanIntercambiarNumeroSerie)
        cmdInicializarAlquiler = New DelegateCommand(Of Object)(AddressOf OnInicializarAlquiler, AddressOf CanInicializarAlquiler)

        Titulo = "Alquileres"

        ' La lectura es asíncrona (API); se lanza fire-and-forget desde el constructor.
        CargarProductosAlquilerAsync(seleccionarUltimo:=False)
    End Sub

    ' Nesto#340 Fase 1C.1: sustituye la lectura EF DbContext.prdProductosAlquilerLista.
    ' Es Function As Task (no Sub) para que los tests puedan await; los call sites de
    ' fire-and-forget la invocan como sentencia y la excepción queda capturada aquí dentro.
    Public Async Function CargarProductosAlquilerAsync(seleccionarUltimo As Boolean) As Task
        Try
            Dim productos = Await _productosAlquilerService.LeerProductosAlquiler()
            colProductosAlquilerLista = New ObservableCollection(Of ProductoAlquilerModel)(productos)
            If seleccionarUltimo Then
                ProductoSeleccionado = colProductosAlquilerLista.LastOrDefault
            End If
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Function

    ' Nesto#340 Fase 1C.2: carga los movimientos del alquiler desde el API (pestaña Movimientos).
    ' Fire-and-forget desde el setter de PestañaSeleccionada; la excepción se captura aquí.
    Public Async Function CargarMovimientosAsync(empresa As String, pedido As Integer) As Task
        Try
            Dim movimientos = Await _productosAlquilerService.LeerMovimientosAlquiler(empresa, pedido)
            colMovimientos = New ObservableCollection(Of MovimientoAlquilerModel)(movimientos)
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Function

    ' Nesto#340 Fase 1C.2: carga las compras del alquiler desde el API (pestaña Compra).
    ' Fire-and-forget desde el setter de PestañaSeleccionada; la excepción se captura aquí.
    Public Async Function CargarCompraAsync(producto As String, numSerie As String) As Task
        Try
            Dim compras = Await _productosAlquilerService.LeerComprasAlquiler(producto, numSerie)
            colCompra = New ObservableCollection(Of CompraAlquilerModel)(compras)
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Function

    ' Nesto#340 Fase 1C.2: carga el extracto del inmovilizado desde el API (pestaña Inmovilizados).
    ' Fire-and-forget desde el setter de PestañaSeleccionada; la excepción se captura aquí.
    Public Async Function CargarInmovilizadosAsync(empresa As String, numero As String) As Task
        Try
            Dim inmovilizados = Await _productosAlquilerService.LeerInmovilizadosAlquiler(empresa, numero)
            colExtractoInmovilizado = New ObservableCollection(Of ExtractoInmovilizadoModel)(inmovilizados)
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Function

    ' Nesto#340 Fase 1C.3: carga el grid principal (cabeceras del producto) desde el API.
    ' Sustituye la lectura EF DbContext.CabAlquileres del setter ProductoSeleccionado.
    Public Async Function CargarCabecerasAsync(empresa As String, producto As String) As Task
        Try
            Dim cabeceras = Await _productosAlquilerService.LeerCabecerasAlquiler(empresa, producto)
            AlquileresCollection = New ObservableCollection(Of AlquilerModel)(cabeceras)
            SuscribirCambios(AlquileresCollection)
            _hayCambios = False
            mensajeError = ""
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Function

    ' Nesto#340 Fase 1C.3: vigila ediciones (PropertyChanged) y altas/bajas (CollectionChanged) para
    ' marcar cambios pendientes, sustituyendo el seguimiento que hacía el ChangeTracker de EF.
    Private Sub SuscribirCambios(coleccion As ObservableCollection(Of AlquilerModel))
        AddHandler coleccion.CollectionChanged, AddressOf OnCabecerasCollectionChanged
        For Each item In coleccion
            AddHandler item.PropertyChanged, AddressOf OnCabeceraPropertyChanged
        Next
    End Sub

    Private Sub OnCabecerasCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        If e.NewItems IsNot Nothing Then
            For Each item As AlquilerModel In e.NewItems
                AddHandler item.PropertyChanged, AddressOf OnCabeceraPropertyChanged
            Next
        End If
        _hayCambios = True
    End Sub

    Private Sub OnCabeceraPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        _hayCambios = True
    End Sub

    ' Empresa/producto del grid actual (todas las filas comparten producto = el seleccionado).
    Private Function EmpresaActual() As String
        Return If(ProductoSeleccionado?.Empresa, If(LineaSeleccionada?.Empresa, "1"))
    End Function

    Private Function ProductoActual() As String
        Return If(ProductoSeleccionado?.Numero, LineaSeleccionada?.Producto)
    End Function


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

    Private _AlquileresCollection As ObservableCollection(Of AlquilerModel)
    Public Property AlquileresCollection() As ObservableCollection(Of AlquilerModel)
        Get
            Return _AlquileresCollection
        End Get
        Set(value As ObservableCollection(Of AlquilerModel))
            SetProperty(_AlquileresCollection, value)
        End Set
    End Property

    Private _LineaSeleccionada As AlquilerModel
    Public Property LineaSeleccionada As AlquilerModel
        Get
            Return _LineaSeleccionada
        End Get
        Set(value As AlquilerModel)
            SetProperty(_LineaSeleccionada, value)
            cmdIntercambiarNumeroSerie.RaiseCanExecuteChanged()
            cmdInicializarAlquiler.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _colExtractoInmovilizado As ObservableCollection(Of ExtractoInmovilizadoModel)
    Public Property colExtractoInmovilizado() As ObservableCollection(Of ExtractoInmovilizadoModel)
        Get
            Return _colExtractoInmovilizado
        End Get
        Set(value As ObservableCollection(Of ExtractoInmovilizadoModel))
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
                    ' Nesto#340 Fase 1C.2: la lectura EF se sustituye por el API (carga asíncrona).
                    If Not String.IsNullOrEmpty(LineaSeleccionada.Inmovilizado) Then
                        CargarInmovilizadosAsync(LineaSeleccionada.Empresa, LineaSeleccionada.Inmovilizado)
                    Else
                        colExtractoInmovilizado = New ObservableCollection(Of ExtractoInmovilizadoModel)
                    End If
                ElseIf value.Name = "tabMovimientos" Then
                    ' Nesto#340 Fase 1C.2: la lectura EF se sustituye por el API (carga asíncrona).
                    If LineaSeleccionada.CabPedidoVta.HasValue Then
                        CargarMovimientosAsync(LineaSeleccionada.Empresa, LineaSeleccionada.CabPedidoVta.Value)
                    Else
                        colMovimientos = New ObservableCollection(Of MovimientoAlquilerModel)
                    End If
                ElseIf value.Name = "tabCompra" Then
                    ' Nesto#340 Fase 1C.2: la lectura EF se sustituye por el API (carga asíncrona).
                    If Not String.IsNullOrEmpty(LineaSeleccionada.Producto) AndAlso Not String.IsNullOrEmpty(LineaSeleccionada.NumeroSerie) Then
                        CargarCompraAsync(LineaSeleccionada.Producto, LineaSeleccionada.NumeroSerie)
                    Else
                        colCompra = New ObservableCollection(Of CompraAlquilerModel)
                    End If
                End If
            End If
            SetProperty(_PestañaSeleccionada, value)
        End Set
    End Property

    ' Nesto#340 Fase 1C.2: los movimientos se leen del API (POCO), ya no de la entidad EF LinPedidoVta.
    Private _colMovimientos As ObservableCollection(Of MovimientoAlquilerModel)
    Public Property colMovimientos As ObservableCollection(Of MovimientoAlquilerModel)
        Get
            Return _colMovimientos
        End Get
        Set(value As ObservableCollection(Of MovimientoAlquilerModel))
            SetProperty(_colMovimientos, value)
        End Set
    End Property

    Private _colProductosAlquilerLista As ObservableCollection(Of ProductoAlquilerModel)
    Public Property colProductosAlquilerLista As ObservableCollection(Of ProductoAlquilerModel)
        Get
            Return _colProductosAlquilerLista
        End Get
        Set(value As ObservableCollection(Of ProductoAlquilerModel))
            SetProperty(_colProductosAlquilerLista, value)
        End Set
    End Property

    Private _ProductoSeleccionado As ProductoAlquilerModel
    Public Property ProductoSeleccionado As ProductoAlquilerModel
        Get
            Return _ProductoSeleccionado
        End Get
        Set(value As ProductoAlquilerModel)
            SetProperty(_ProductoSeleccionado, value)
            If Not IsNothing(ProductoSeleccionado) Then
                ' Nesto#340 Fase 1C.3: el grid se carga del API (carga asíncrona) en vez de EF.
                CargarCabecerasAsync(ProductoSeleccionado.Empresa, ProductoSeleccionado.Numero)
            End If

            colExtractoInmovilizado = Nothing
            colMovimientos = Nothing
        End Set
    End Property

    ' Nesto#340 Fase 1C.2: las compras se leen del API (POCO), ya no de la entidad EF LinPedidoCmp.
    Private _colCompra As ObservableCollection(Of CompraAlquilerModel)
    Public Property colCompra As ObservableCollection(Of CompraAlquilerModel)
        Get
            Return _colCompra
        End Get
        Set(value As ObservableCollection(Of CompraAlquilerModel))
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
        Return _hayCambios
    End Function
    Private Async Sub Guardar(ByVal param As Object)
        Try
            Dim producto As String = ProductoActual()
            If String.IsNullOrEmpty(producto) OrElse AlquileresCollection Is Nothing Then
                Return
            End If

            ' Nesto#340 Fase 1C.3: la API reconcilia altas/ediciones/bajas; sustituye SaveChanges.
            Await _productosAlquilerService.GuardarCabecerasAlquiler(EmpresaActual(), producto, AlquileresCollection.ToList())

            ' Recargamos la lista de productos (StockAlquileres cambia) manteniendo la selección,
            ' lo que recarga las cabeceras del producto con los Números asignados a las altas.
            Dim numeroActual As String = producto
            Await CargarProductosAlquilerAsync(seleccionarUltimo:=False)
            ProductoSeleccionado = colProductosAlquilerLista?.FirstOrDefault(Function(p) p.Numero = numeroActual)
            mensajeError = ""
        Catch ex As Exception
            If Not IsNothing(ex.InnerException) Then
                mensajeError = ex.InnerException.Message
            Else
                mensajeError = ex.ToString
            End If
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
        Dim alqui As New AlquilerModel
        alqui.Empresa = "1"
        If ProductoSeleccionado IsNot Nothing Then
            alqui.Producto = ProductoSeleccionado.Numero
        ElseIf LineaSeleccionada IsNot Nothing Then
            alqui.Producto = LineaSeleccionada.Producto
        End If
        If AlquileresCollection Is Nothing Then
            AlquileresCollection = New ObservableCollection(Of AlquilerModel)
            SuscribirCambios(AlquileresCollection)
        End If
        ' El alta es Número = 0; la BD le asigna el Número al guardar (identity).
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
        If LineaSeleccionada Is Nothing OrElse AlquileresCollection Is Nothing Then
            Return
        End If
        ' La baja se persiste al guardar (la API reconcilia lo que ya no viene en la lista).
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
                builder.AppendLine("A190,10,0,4,1,1,N,""" + If(LineaSeleccionada.NombreCliente, "").Trim + """")
                builder.AppendLine("A190,60,0,4,1,1,N,""" + If(LineaSeleccionada.DireccionCliente, "") + """")
                builder.AppendLine("A190,110,0,4,1,1,N,""" + If(LineaSeleccionada.CodPostalCliente, "").Trim + " " + If(LineaSeleccionada.PoblacionCliente, "").Trim + """")
                builder.AppendLine("A190,160,0,4,1,1,N,""" + If(LineaSeleccionada.ProvinciaCliente, "").Trim + "")
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
    Private Async Sub OnIntercambiarNumeroSerie(arg As Object)
        Dim alquiler As AlquilerModel = (From a In AlquileresCollection Where a.NumeroSerie = numeroSerieIntercambiar).SingleOrDefault

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

        ' Procedemos al cambio: intercambiamos en memoria y persistimos vía API (Nesto#340 Fase 1C.3).
        Try
            Dim numeroSerieIntermedio As String = LineaSeleccionada.NumeroSerie
            LineaSeleccionada.NumeroSerie = numeroSerieIntercambiar.Trim
            alquiler.NumeroSerie = numeroSerieIntermedio.Trim
            Await _productosAlquilerService.GuardarCabecerasAlquiler(EmpresaActual(), ProductoActual(), AlquileresCollection.ToList())
            _hayCambios = False
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