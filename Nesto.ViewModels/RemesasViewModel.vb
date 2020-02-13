Imports Nesto.Models
Imports System.Windows.Input
Imports System.ComponentModel
Imports System.Collections.ObjectModel
Imports System.Windows
Imports Microsoft.Win32
Imports Microsoft.Office.Interop
Imports System.Windows.Controls
Imports System.Threading.Tasks
Imports Nesto.Contratos
Imports Nesto.Models.Nesto.Models

Public Class RemesasViewModel
    Inherits ViewModelBase

    Const numRemesas = 100

    Private Shared DbContext As NestoEntities
    Dim mainViewModel As New MainViewModel
    Dim empresaDefecto As String = "1" 'mainModel.leerParametro("1", "EmpresaPorDefecto")
    Dim blnPuedeVerTodasLasRemesas As Boolean = True

    Public Structure tipoRemesa
        Public Sub New(
       ByVal _id As String,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As String
        Property descripcion As String
    End Structure

    Public Sub New()
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Titulo = "Remesas"
        DbContext = New NestoEntities
        listaEmpresas = New ObservableCollection(Of Empresas)(From c In DbContext.Empresas)
        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        listaRemesas = New ObservableCollection(Of Remesas)(From c In DbContext.Remesas Where c.Empresa = empresaActual Order By c.Número Descending Take numRemesas)
        remesaActual = listaRemesas.FirstOrDefault
        listaImpagados = New ObservableCollection(Of impagado)(From c In DbContext.ExtractoCliente Where c.Empresa = empresaActual And c.TipoApunte = "4" Group By c.Asiento, c.Fecha Into Count() Order By Asiento Descending Take numRemesas Select New impagado With {.asiento = Asiento, .fecha = Fecha, .cuenta = Count})
        impagadoActual = listaImpagados.FirstOrDefault
        'usuarioTareas = mainModel.leerParametro(empresaActual, "UsuarioAvisoImpagadoDefecto")
        usuarioTareas = "carmengarcia@nuevavision.es" 'mainViewModel.leerParametro(empresaActual, "UsuarioAvisoImpagadoDefecto").Result
        listaTiposRemesa = New ObservableCollection(Of tipoRemesa)
        tipoRemesaActual = New tipoRemesa("B2B", "Profesionales (B2B)")
        listaTiposRemesa.Add(tipoRemesaActual)
        listaTiposRemesa.Add(New tipoRemesa("CORE", "Particulares (CORE)"))
        tipoRemesaActual = listaTiposRemesa.LastOrDefault ' Por defecto es CORE -> FirstOrDefault para B2B
        fechaCobro = Today
    End Sub


#Region "Propiedades"
    Private Property _listaEmpresas As ObservableCollection(Of Empresas)
    Public Property listaEmpresas As ObservableCollection(Of Empresas)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of Empresas))
            _listaEmpresas = value
            OnPropertyChanged("listaEmpresas")
        End Set
    End Property

    Private Property _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            listaRemesas = New ObservableCollection(Of Remesas)(From c In DbContext.Remesas Where c.Empresa = empresaActual Order By c.Número Descending Take numRemesas)
            blnPuedeVerTodasLasRemesas = True
            listaImpagados = New ObservableCollection(Of impagado)(From c In DbContext.ExtractoCliente Where c.Empresa = empresaActual And c.TipoApunte = "4" Group By c.Asiento, c.Fecha Into Count() Order By Asiento Descending Take numRemesas Select New impagado With {.asiento = Asiento, .fecha = Fecha, .cuenta = Count})
            OnPropertyChanged("empresaActual")
        End Set
    End Property

    Private Property _remesaActual As Remesas
    Public Property remesaActual As Remesas
        Get
            Return _remesaActual
        End Get
        Set(value As Remesas)
            _remesaActual = value
            If IsNothing(remesaActual) Then
                listaMovimientos = Nothing
            Else
                listaMovimientos = New ObservableCollection(Of ExtractoCliente)(From e In DbContext.ExtractoCliente Where e.Empresa = empresaActual And e.Remesa = remesaActual.Número And e.TipoApunte = 3)
            End If
            OnPropertyChanged("remesaActual")
        End Set
    End Property

    Private Property _contenidoFichero As Xml.Linq.XDocument
    Public Property contenidoFichero As Xml.Linq.XDocument
        Get
            Return _contenidoFichero
        End Get
        Set(value As Xml.Linq.XDocument)
            _contenidoFichero = value
            OnPropertyChanged("contenidoFichero")
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

    Private Property _listaRemesas As ObservableCollection(Of Remesas)
    Public Property listaRemesas As ObservableCollection(Of Remesas)
        Get
            Return _listaRemesas
        End Get
        Set(value As ObservableCollection(Of Remesas))
            _listaRemesas = value
            OnPropertyChanged("listaRemesas")
        End Set
    End Property

    Private Property _listaMovimientos As ObservableCollection(Of ExtractoCliente)
    Public Property listaMovimientos As ObservableCollection(Of ExtractoCliente)
        Get
            Return _listaMovimientos
        End Get
        Set(value As ObservableCollection(Of ExtractoCliente))
            _listaMovimientos = value
            OnPropertyChanged("listaMovimientos")
        End Set
    End Property

    Private Property _PestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _PestañaSeleccionada
        End Get
        Set(value As TabItem)
            _PestañaSeleccionada = value
            OnPropertyChanged("PestañaSeleccionada")
        End Set
    End Property

    Private Property _listaImpagados As ObservableCollection(Of impagado)
    Public Property listaImpagados As ObservableCollection(Of impagado)
        Get
            Return _listaImpagados
        End Get
        Set(value As ObservableCollection(Of impagado))
            _listaImpagados = value
            OnPropertyChanged("listaImpagados")
        End Set
    End Property

    Private Property _listaImpagadosDetalle As ObservableCollection(Of ExtractoCliente)
    Public Property listaImpagadosDetalle As ObservableCollection(Of ExtractoCliente)
        Get
            Return _listaImpagadosDetalle
        End Get
        Set(value As ObservableCollection(Of ExtractoCliente))
            _listaImpagadosDetalle = value
            OnPropertyChanged("listaImpagadosDetalle")
        End Set
    End Property

    Private Property _impagadoActual As impagado
    Public Property impagadoActual As impagado
        Get
            Return _impagadoActual
        End Get
        Set(value As impagado)
            _impagadoActual = value
            If IsNothing(impagadoActual) Then
                listaImpagadosDetalle = Nothing
            Else
                listaImpagadosDetalle = New ObservableCollection(Of ExtractoCliente)(From e In DbContext.ExtractoCliente Where e.Empresa = empresaActual And e.Asiento = impagadoActual.asiento And e.TipoApunte = 4)
            End If
            OnPropertyChanged("impagadoActual")
        End Set
    End Property

    Private Property _usuarioTareas As String
    Public Property usuarioTareas As String
        Get
            Return _usuarioTareas
        End Get
        Set(value As String)
            _usuarioTareas = value
            OnPropertyChanged("usuarioActual")
        End Set
    End Property

    Private Property _listaTiposRemesa As ObservableCollection(Of tipoRemesa)
    Public Property listaTiposRemesa As ObservableCollection(Of tipoRemesa)
        Get
            Return _listaTiposRemesa
        End Get
        Set(value As ObservableCollection(Of tipoRemesa))
            _listaTiposRemesa = value
            'OnPropertyChanged("listaTiposRemesa")
        End Set
    End Property

    Private Property _tipoRemesaActual As tipoRemesa
    Public Property tipoRemesaActual As tipoRemesa
        Get
            Return _tipoRemesaActual
        End Get
        Set(value As tipoRemesa)
            _tipoRemesaActual = value
            OnPropertyChanged("tipoRemesaActual")
        End Set
    End Property

    Private Property _fechaCobro As Date
    Public Property fechaCobro As Date
        Get
            Return _fechaCobro
        End Get
        Set(value As Date)
            _fechaCobro = value
            OnPropertyChanged("fechaCobro")
        End Set
    End Property

    Private _estaOcupado As Boolean = False
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(ByVal value As Boolean)
            _estaOcupado = value
            OnPropertyChanged("estaOcupado")
        End Set
    End Property

#End Region

#Region "Comandos"

    Private _cmdCrearFicheroRemesa As ICommand
    Public ReadOnly Property cmdCrearFicheroRemesa() As ICommand
        Get
            If _cmdCrearFicheroRemesa Is Nothing Then
                _cmdCrearFicheroRemesa = New RelayCommand(AddressOf CrearFicheroRemesa, AddressOf CanCrearFicheroRemesa)
            End If
            Return _cmdCrearFicheroRemesa
        End Get
    End Property
    Private Function CanCrearFicheroRemesa(ByVal param As Object) As Boolean
        Return Not remesaActual Is Nothing
    End Function
    Private Async Sub CrearFicheroRemesa(ByVal param As Object)
        Dim strContenido As String
        Dim listaContenido As List(Of String)
        Dim codigo As String
        codigo = tipoRemesaActual.id
        Dim nombreFichero As String = Await mainViewModel.leerParametro(empresaActual, Parametros.Claves.PathNorma19) + CStr(remesaActual.Número) + ".xml"
        'Dim nombreFichero As String = "c:\banco\prueba.xml"
        Try
            mensajeError = "Generando fichero..."
            'DbContext.Database.CommandTimeout = 6000

            estaOcupado = True
            Await Task.Run(Sub()
                               listaContenido = crearFicheroRemesa(remesaActual.Número, codigo, fechaCobro)
                               'DbContext.Database.CommandTimeout = 180
                               strContenido = ""
                               For Each linea In listaContenido
                                   strContenido = strContenido + linea
                               Next
                               contenidoFichero = XDocument.Parse(strContenido)
                               contenidoFichero.Save(nombreFichero)
                               mensajeError = "Fichero " + nombreFichero + " creado correctamente"
                           End Sub)

        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        Finally
            estaOcupado = False
        End Try
    End Sub

    Private _cmdLeerFicheroImpagado As ICommand
    Public ReadOnly Property cmdLeerFicheroImpagado() As ICommand
        Get
            If _cmdLeerFicheroImpagado Is Nothing Then
                _cmdLeerFicheroImpagado = New RelayCommand(AddressOf LeerFicheroImpagado, AddressOf CanLeerFicheroImpagado)
            End If
            Return _cmdLeerFicheroImpagado
        End Get
    End Property
    Private Function CanLeerFicheroImpagado(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Async Sub LeerFicheroImpagado(ByVal param As Object)
        Dim elegirFichero = New OpenFileDialog
        elegirFichero.Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*"
        elegirFichero.FilterIndex = 1
        elegirFichero.RestoreDirectory = True
        elegirFichero.InitialDirectory = Await mainViewModel.leerParametro(empresaActual, "PathDefectoImpagados")

        If elegirFichero.ShowDialog() Then
            Try
                contenidoFichero = XDocument.Load(elegirFichero.FileName)
                estaOcupado = True
                mensajeError = "Contabilizando..."
                Await Task.Run(Sub()
                                   contabilizarImpagados(contenidoFichero.ToString)
                                   mensajeError = "Impagados contabilizados correctamente"
                               End Sub)
            Catch ex As Exception
                If IsNothing(ex.InnerException) Then
                    mensajeError = ex.Message
                Else
                    mensajeError = ex.InnerException.Message
                End If
            Finally
                estaOcupado = False
            End Try
        End If

        listaImpagados = New ObservableCollection(Of impagado)(From c In DbContext.ExtractoCliente Where c.Empresa = empresaActual And c.TipoApunte = "4" Group By c.Asiento, c.Fecha Into Count() Order By Asiento Descending Take numRemesas Select New impagado With {.asiento = Asiento, .fecha = Fecha, .cuenta = Count})
        impagadoActual = listaImpagados.First

    End Sub

    Private _cmdVerTodasLasRemesas As ICommand
    Public ReadOnly Property cmdVerTodasLasRemesas() As ICommand
        Get
            If _cmdVerTodasLasRemesas Is Nothing Then
                _cmdVerTodasLasRemesas = New RelayCommand(AddressOf VerTodasLasRemesas, AddressOf CanVerTodasLasRemesas)
            End If
            Return _cmdVerTodasLasRemesas
        End Get
    End Property
    Private Function CanVerTodasLasRemesas(ByVal param As Object) As Boolean
        Return blnPuedeVerTodasLasRemesas
    End Function
    Private Sub VerTodasLasRemesas(ByVal param As Object)
        listaRemesas = New ObservableCollection(Of Remesas)(From c In DbContext.Remesas Where c.Empresa = empresaActual Order By c.Número Descending)
        blnPuedeVerTodasLasRemesas = False
    End Sub

    Private _cmdCrearTareasOutlook As ICommand
    Public ReadOnly Property cmdCrearTareasOutlook() As ICommand
        Get
            If _cmdCrearTareasOutlook Is Nothing Then
                _cmdCrearTareasOutlook = New RelayCommand(AddressOf CrearTareasOutlook, AddressOf CanCrearTareasOutlook)
            End If
            Return _cmdCrearTareasOutlook
        End Get
    End Property
    Private Function CanCrearTareasOutlook(ByVal param As Object) As Boolean
        Return Not impagadoActual Is Nothing
    End Function
    Private Sub CrearTareasOutlook(ByVal param As Object)
        Dim objOL As Outlook.Application
        objOL = New Outlook.Application
        Dim newTask As Outlook.TaskItem
        Dim ruta As New Rutas
        Dim impagados = From e In DbContext.ExtractoCliente Join c In DbContext.Clientes On e.Empresa Equals c.Empresa And e.Número Equals c.Nº_Cliente And e.Contacto Equals c.Contacto Where e.Empresa = empresaActual And e.Asiento = impagadoActual.asiento And Not e.Concepto.StartsWith("Gastos Impagado ")

        Try
            For Each impagado In impagados
                newTask = objOL.CreateItem(Outlook.OlItemType.olTaskItem)
                If Not IsNothing(newTask) Then
                    ruta = (From r In DbContext.Rutas Where r.Empresa = impagado.c.Empresa And r.Número = impagado.c.Ruta).FirstOrDefault
                    newTask.Subject = ruta.Descripción.Trim + " - Llamar al cliente " + impagado.e.Número.Trim + "/" + impagado.e.Contacto.Trim +
                        ". Vendedor: " + impagado.c.Vendedor.Trim + ". " + impagado.c.Nombre.Trim + " en " + impagado.c.Dirección.Trim
                    newTask.Body = "Ha llegado un impagado de este cliente, con fecha " + impagado.e.Fecha.ToShortDateString + " e importe de " + FormatCurrency(impagado.e.Importe) + " (más gastos)." + vbCrLf +
                        "Motivo: " + impagado.e.Concepto + vbCrLf +
                        "Ruta: " + impagado.c.Ruta + vbCrLf +
                        "Empresa: " + impagado.c.Empresas.Nombre.Trim
                    newTask.Assign()
                    'If impagado.c.Ruta.Trim = "00" Or impagado.c.Ruta.Trim = "02" Or impagado.c.Ruta.Trim = "03" Then
                    '    usuarioTareas = "laura@nuevavision.es"
                    'Else
                    '    usuarioTareas = "aidarubio@nuevavision.es"
                    'End If
                    newTask.Recipients.Add(usuarioTareas)
                    newTask.Recipients.ResolveAll()
                    newTask.Send()
                End If
            Next
            mensajeError = "Tareas del asiento " + CStr(impagadoActual.asiento) + " creadas correctamente"
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try
    End Sub

#End Region

#Region "Funciones Auxiliares"
    Private Sub contabilizarImpagados(contenidoFichero As String)
        DbContext.prdContabilizarImpagadosSepa(contenidoFichero)
    End Sub

    Private Function crearFicheroRemesa(remesa As Integer, codigo As String, fechaCobro As Date) As List(Of String)
        Return DbContext.CrearFicheroRemesa(remesa, codigo, fechaCobro).ToList
    End Function
#End Region

End Class

Public Class impagado
    Public Sub New()

    End Sub

    Private Property _asiento As Integer
    Public Property asiento As Integer
        Get
            Return _asiento
        End Get
        Set(value As Integer)
            _asiento = value
        End Set
    End Property

    Private Property _fecha As Date
    Public Property fecha As Date
        Get
            Return _fecha
        End Get
        Set(value As Date)
            _fecha = value
        End Set
    End Property

    Private Property _cuenta As Integer
    Public Property cuenta As Integer
        Get
            Return _cuenta
        End Get
        Set(value As Integer)
            _cuenta = value
        End Set
    End Property

End Class

