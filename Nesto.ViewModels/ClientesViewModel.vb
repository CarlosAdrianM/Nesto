Imports Nesto.Models
Imports System.ComponentModel
Imports System.Windows
Imports System.Collections.ObjectModel
Imports System.Windows.Input
Imports System.IO
Imports Microsoft.Win32
Imports System.Windows.Data
Imports System.Globalization
Imports System.Xml.Linq
'Imports Nesto.Models.Nesto.Models.EF

Public Interface IOService
    Function OpenFileDialog(defaultPath As String) As String

    'Other similar untestable IO operations
    Function OpenFile(path As String) As Stream
End Interface

Public Class ClientesViewModel
    Inherits ViewModelBase
    'Implements IOService

    Private Shared DbContext As NestoEntities

    Dim mainModel As New Nesto.Models.MainModel
    Private ruta As String = mainModel.leerParametro(empresaActual, "RutaMandatos")

    Public Structure tipoIdDescripcion
        Public Sub New( _
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
        DbContext = New NestoEntities
        listaEmpresas = New ObservableCollection(Of Empresas)(From c In DbContext.Empresas)

        Dim mainModel As New Nesto.Models.MainModel
        Dim empresaDefecto As String = mainModel.leerParametro("1", "EmpresaPorDefecto")
        Dim clienteDefecto As String = mainModel.leerParametro(empresaDefecto, "UltNumCliente")
        vendedor = mainModel.leerParametro(empresaDefecto, "Vendedor")

        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        clienteActual = clienteDefecto
        contactoActual = "0" 'esto hay que cambiarlo por el ClientePrincipal

        listaEstadosCCC = New ObservableCollection(Of EstadosCCC)(From c In DbContext.EstadosCCC Where c.Empresa = empresaActual)

        Dim rangosFechas As New List(Of String)
        rangosFechas.Add("Ventas del Último Año")
        rangosFechas.Add("Ventas de Siempre")

        deudaVencida = (Aggregate c In DbContext.ExtractoCliente Where (c.Empresa = "1" Or c.Empresa = "3") And c.Número = clienteActivo.Nº_Cliente And c.Contacto = clienteActivo.Contacto And c.FechaVto < Now And c.ImportePdte <> 0 Into Sum(CType(c.ImportePdte, Decimal?)))

        listaSecuencias = New ObservableCollection(Of tipoIdDescripcion)
        listaSecuencias.Add(New tipoIdDescripcion("FRST", "Primer adeudo recurrente"))
        listaSecuencias.Add(New tipoIdDescripcion("RCUR", "Resto de adeudos recurrentes"))
        listaSecuencias.Add(New tipoIdDescripcion("OOFF", "Operación de un único pago"))
        listaSecuencias.Add(New tipoIdDescripcion("FNAL", "Último adeudo recurrente"))

        listaTipos = New ObservableCollection(Of tipoIdDescripcion)
        listaTipos.Add(New tipoIdDescripcion(1, "Consumidor final"))
        listaTipos.Add(New tipoIdDescripcion(2, "Profesional"))
    End Sub

    Private Property _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            actualizarCliente(_empresaActual, _clienteActual, _contactoActual)
            OnPropertyChanged("empresaActual")
        End Set
    End Property

    Private Property _clienteActual As String
    Public Property clienteActual As String
        Get
            Return _clienteActual
        End Get
        Set(value As String)
            _clienteActual = value
            actualizarCliente(_empresaActual, _clienteActual, _contactoActual)
            OnPropertyChanged("clienteActual")
        End Set
    End Property

    Private Property _contactoActual As String
    Public Property contactoActual As String
        Get
            Return _contactoActual
        End Get
        Set(value As String)
            _contactoActual = value
            actualizarCliente(_empresaActual, _clienteActual, _contactoActual)
            OnPropertyChanged("contactoActual")
        End Set
    End Property

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

    Private Property _nombre As String
    Public Property nombre As String
        Get
            Return _nombre
        End Get
        Set(value As String)
            _nombre = value
            OnPropertyChanged("nombre")
        End Set
    End Property

    Private Property _cuentaActiva As CCC
    Public Property cuentaActiva As CCC
        Get
            Return _cuentaActiva
        End Get
        Set(value As CCC)
            _cuentaActiva = value
            OnPropertyChanged("cuentaActiva")
        End Set
    End Property

    Private Property _cuentasBanco As ObservableCollection(Of CCC)
    Public Property cuentasBanco As ObservableCollection(Of CCC)
        Get
            Return _cuentasBanco
        End Get
        Set(value As ObservableCollection(Of CCC))
            _cuentasBanco = value
            OnPropertyChanged("cuentasBanco")
        End Set
    End Property

    Private Sub actualizarCliente(empresa As String, numCliente As String, contacto As String)
        If Not (IsNothing(empresa) Or IsNothing(numCliente) Or IsNothing(contacto)) Then
            Dim cliente = (From c In DbContext.Clientes Where c.Empresa = empresa And c.Nº_Cliente = numCliente And c.Contacto = contacto).FirstOrDefault
            If IsNothing(cliente) Then 'Si no existe el cliente que tiene en el parámetro
                cliente = (From c In DbContext.Clientes Where c.Empresa = empresa).FirstOrDefault
                numCliente = cliente.Nº_Cliente
                contacto = cliente.Contacto
                clienteActual = numCliente
                contactoActual = contacto
            End If
            nombre = cliente.Nombre
            cuentasBanco = New ObservableCollection(Of CCC)(From c In DbContext.CCC Where c.Empresa = empresa And c.Cliente = numCliente And c.Contacto = contacto)
            If Not IsNothing(cliente.CCC2) Then
                cuentaActiva = cuentasBanco.Where(Function(x) x.Número = cliente.CCC2.Número).FirstOrDefault
            End If
            clienteActivo = cliente
        End If
    End Sub

    Private Property _clienteActivo As Clientes
    Public Property clienteActivo As Clientes
        Get
            Return _clienteActivo
        End Get
        Set(value As Clientes)
            Dim fechaDesde As Date
            _clienteActivo = value
            If Not IsNothing(_listaClientesVendedor) Then
                If Not IsNothing(clienteActivo) Then
                    seguimientosOrdenados = New ObservableCollection(Of SeguimientoCliente)(From c In clienteActivo.SeguimientoCliente Order By c.Fecha Descending Take 20)
                    If rangoFechasVenta = "System.Windows.Controls.ComboBoxItem: Ventas de siempre" Then 'esto está fatal, hay que desacoplarlo de la vista 
                        fechaDesde = DateTime.MinValue
                    Else
                        fechaDesde = DateTime.Now.AddYears(-1)
                    End If
                    listaVentas = New ObservableCollection(Of lineaVentaAgrupada)(From l In DbContext.LinPedidoVta Where (l.Empresa = "1" Or l.Empresa = "3") And l.Nº_Cliente = clienteActivo.Nº_Cliente And l.Contacto = clienteActivo.Contacto And l.Estado >= 2 And l.Fecha_Albarán >= fechaDesde Group By l.Producto, l.Texto, l.SubGruposProducto.Descripción Into Sum(l.Cantidad), Max(l.Fecha_Albarán) Select New lineaVentaAgrupada With {.producto = Producto, .nombre = Texto, .cantidad = Sum, .fechaUltVenta = Max, .subGrupo = Descripción})
                    deudaVencida = (Aggregate c In DbContext.ExtractoCliente Where (c.Empresa = "1" Or c.Empresa = "3") And c.Número = clienteActivo.Nº_Cliente And c.Contacto = clienteActivo.Contacto And c.FechaVto < Now And c.ImportePdte <> 0 Into Sum(CType(c.ImportePdte, Decimal?)))
                Else
                    seguimientosOrdenados = Nothing
                    listaVentas = Nothing
                End If
            End If

            OnPropertyChanged("clienteActivo")
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

    Private _selectedPath As String
    Public Property selectedPath() As String
        Get
            Return _selectedPath
        End Get
        Set(value As String)
            _selectedPath = value
            OnPropertyChanged("selectedPath")
        End Set
    End Property

    Private Property _listaEstadosCCC As ObservableCollection(Of EstadosCCC)
    Public Property listaEstadosCCC As ObservableCollection(Of EstadosCCC)
        Get
            Return _listaEstadosCCC
        End Get
        Set(value As ObservableCollection(Of EstadosCCC))
            _listaEstadosCCC = value
            OnPropertyChanged("listaEstadosCCC")
        End Set
    End Property

    Private Property _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
            OnPropertyChanged("vendedor")
        End Set
    End Property

    Private Property _listaClientesVendedor As ObservableCollection(Of Clientes)
    Public Property listaClientesVendedor As ObservableCollection(Of Clientes)
        Get
            If IsNothing(_listaClientesVendedor) Then
                _listaClientesVendedor = inicializarListaClientesVendedor()
            End If
            Return _listaClientesVendedor
        End Get
        Set(value As ObservableCollection(Of Clientes))
            _listaClientesVendedor = value
            OnPropertyChanged("listaClientesVendedor")
        End Set
    End Property

    Private _seguimientosOrdenados As ObservableCollection(Of SeguimientoCliente)
    Public Property seguimientosOrdenados As ObservableCollection(Of SeguimientoCliente)
        Get
            If IsNothing(_seguimientosOrdenados) Then
                If Not IsNothing(clienteActivo) Then
                    _seguimientosOrdenados = New ObservableCollection(Of SeguimientoCliente)(From c In clienteActivo.SeguimientoCliente Order By c.Fecha Descending Take 20)
                Else
                    _seguimientosOrdenados = Nothing
                End If
            End If
            Return _seguimientosOrdenados
        End Get
        Set(value As ObservableCollection(Of SeguimientoCliente))
            _seguimientosOrdenados = value
            OnPropertyChanged("seguimientosOrdenados")
        End Set
    End Property

    Private Property _listaVentas As ObservableCollection(Of lineaVentaAgrupada)
    Public Property listaVentas As ObservableCollection(Of lineaVentaAgrupada)
        Get
            Return _listaVentas
        End Get
        Set(value As ObservableCollection(Of lineaVentaAgrupada))
            _listaVentas = value
            OnPropertyChanged("listaVentas")
        End Set
    End Property

    Private Property _filtro As String
    Public Property filtro As String
        Get
            Return _filtro
        End Get
        Set(value As String)
            _filtro = value
            If filtro.Trim <> "" Then
                listaClientesVendedor = New ObservableCollection(Of Clientes)(From c In listaClientesVendedor Where (Not IsNothing(c.Dirección) AndAlso c.Dirección.ToLower.Contains(filtro.ToLower)) Or (Not IsNothing(c.Nombre) AndAlso c.Nombre.ToLower.Contains(filtro.ToLower)) Or (Not IsNothing(c.Nº_Cliente) AndAlso c.Nº_Cliente.ToLower.Trim = filtro.ToLower))
            Else
                listaClientesVendedor = inicializarListaClientesVendedor()
            End If

            OnPropertyChanged("filtro")
        End Set
    End Property

    Private Property _rangoFechasVenta As String
    Public Property rangoFechasVenta As String
        Get
            Return _rangoFechasVenta
        End Get
        Set(value As String)
            Dim fechaDesde As Date
            _rangoFechasVenta = value
            If Not IsNothing(clienteActivo) Then
                If rangoFechasVenta = "System.Windows.Controls.ComboBoxItem: Ventas de siempre" Then 'esto hay que cambiarlo, que es una ñapa muy gorda
                    fechaDesde = DateTime.MinValue
                Else
                    fechaDesde = DateTime.Now.AddYears(-1)
                End If
                listaVentas = New ObservableCollection(Of lineaVentaAgrupada)(From l In DbContext.LinPedidoVta Where (l.Empresa = "1" Or l.Empresa = "3") And l.Nº_Cliente = clienteActivo.Nº_Cliente And l.Contacto = clienteActivo.Contacto And l.Estado >= 2 And l.Fecha_Albarán >= fechaDesde Group By l.Producto, l.Texto, l.SubGruposProducto.Descripción Into Sum(l.Cantidad), Max(l.Fecha_Albarán) Select New lineaVentaAgrupada With {.producto = Producto, .nombre = Texto, .cantidad = Sum, .fechaUltVenta = Max, .subGrupo = Descripción})
            End If
            OnPropertyChanged("rangoFechasVenta")
        End Set
    End Property

    Private Property _deudaVencida As Nullable(Of Decimal)
    Public Property deudaVencida As Nullable(Of Decimal)
        Get
            Return _deudaVencida
        End Get
        Set(value As Nullable(Of Decimal))
            _deudaVencida = value
            OnPropertyChanged("deudaVencida")
        End Set
    End Property

    Private Property _listaSecuencias As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaSecuencias As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaSecuencias
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaSecuencias = value
            OnPropertyChanged("listaSecuencias")
        End Set
    End Property

    Private Property _listaTipos As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaTipos As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaTipos
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaTipos = value
            OnPropertyChanged("listaTipos")
        End Set
    End Property

    Private Function inicializarListaClientesVendedor() As ObservableCollection(Of Clientes)
        If vendedor <> "" Then
            Return New ObservableCollection(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = empresaActual And c.Vendedor = vendedor And c.Estado >= 0 Order By c.CodPostal, c.Dirección)
        Else
            Return New ObservableCollection(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = empresaActual And c.Estado >= 0)
        End If

    End Function
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
        Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)
        Return changes.Any
    End Function
    Private Sub Guardar(ByVal param As Object)
        Try
            DbContext.SaveChanges()
            mensajeError = ""
        Catch ex As Exception
            mensajeError = ex.InnerException.Message
        End Try

    End Sub

    Private _cmdVerMandato As ICommand
    Public ReadOnly Property cmdVerMandato() As ICommand
        Get
            If _cmdVerMandato Is Nothing Then
                _cmdVerMandato = New RelayCommand(AddressOf VerMandato, AddressOf CanVerMandato)
            End If
            Return _cmdVerMandato
        End Get
    End Property
    Private Function CanVerMandato(ByVal param As Object) As Boolean
        'Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)
        If IsNothing(cuentaActiva) Then
            Return False
        ElseIf Not My.Computer.FileSystem.FileExists(rutaMandato) Then
            Return False
        Else
            Return True
        End If
    End Function
    Private Sub VerMandato(ByVal param As Object)
        Try
            Dim fileName As String = rutaMandato()
            Dim process As System.Diagnostics.Process = New System.Diagnostics.Process
            process.StartInfo.FileName = fileName
            process.Start()
            process.WaitForExit()
            mensajeError = ""
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try
    End Sub

    Private _cmdNuevoMandato As ICommand
    Public ReadOnly Property cmdNuevoMandato() As ICommand
        Get
            If _cmdNuevoMandato Is Nothing Then
                _cmdNuevoMandato = New RelayCommand(AddressOf NuevoMandato, AddressOf CanNuevoMandato)
            End If
            Return _cmdNuevoMandato
        End Get
    End Property
    Private Function CanNuevoMandato(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub NuevoMandato(ByVal param As Object)
        Try
            Dim siguienteNumero As Integer
            'siguienteNumero = 1
            'If Not IsNothing(cuentaActiva) Then
            If cuentasBanco.Count = 0 Then
                siguienteNumero = 1
            Else
                'siguienteNumero = CInt(cuentasBanco.Where(Function(x) x.Cliente.Trim = clienteActual).LastOrDefault.Número) + 1
                siguienteNumero = CInt(cuentasBanco.Where(Function(x) x.Cliente.Trim = clienteActual).OrderBy(Function(x) x.Número).LastOrDefault.Número) + 1
            End If
            'End If
            cuentaActiva = CCC.CreateCCC(empresaActual, clienteActual, contactoActual, siguienteNumero, "", "", "", "", 0, Nothing, Nothing, "FRST")
            cuentasBanco.Add(cuentaActiva)
            DbContext.AddToCCC(cuentaActiva)
            mensajeError = ""

        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try
    End Sub

    Private _cmdAsignarMandato As ICommand
    Public ReadOnly Property cmdAsignarMandato() As ICommand
        Get
            If _cmdAsignarMandato Is Nothing Then
                _cmdAsignarMandato = New RelayCommand(AddressOf AsignarMandato, AddressOf CanAsignarMandato)
            End If
            Return _cmdAsignarMandato
        End Get
    End Property
    Private Function CanAsignarMandato(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub AsignarMandato(ByVal param As Object)
        Dim elegirFichero = New OpenFileDialog
        elegirFichero.Filter = "pdf files (*.pdf)|*.pdf|All files (*.*)|*.*"
        elegirFichero.FilterIndex = 1
        elegirFichero.RestoreDirectory = True

        If elegirFichero.ShowDialog() Then
            Try
                selectedPath = elegirFichero.FileName
                My.Computer.FileSystem.CopyFile(
                selectedPath,
                rutaMandato,
                Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing)
            Catch ex As Exception
                If IsNothing(ex.InnerException) Then
                    mensajeError = ex.Message
                Else
                    mensajeError = ex.InnerException.Message
                End If
            Finally
                If selectedPath Is Nothing Then
                    selectedPath = String.Empty
                End If
            End Try
        End If
    End Sub

    'Private _cmdCrearFicheroRemesa As ICommand
    'Public ReadOnly Property cmdCrearFicheroRemesa() As ICommand
    '    Get
    '        If _cmdCrearFicheroRemesa Is Nothing Then
    '            _cmdCrearFicheroRemesa = New RelayCommand(AddressOf CrearFicheroRemesa, AddressOf CanCrearFicheroRemesa)
    '        End If
    '        Return _cmdCrearFicheroRemesa
    '    End Get
    'End Property
    'Private Function CanCrearFicheroRemesa(ByVal param As Object) As Boolean
    '    Return True
    'End Function
    'Private Sub CrearFicheroRemesa(ByVal param As Object)
    '    Dim strContenido As String
    '    Dim listaContenido As List(Of String)
    '    Dim codigo As String = "B2B"
    '    Dim nombreFichero As String = mainModel.leerParametro(empresaActual, "PathNorma19") + CStr(numeroRemesa) + ".xml"
    '    'Dim nombreFichero As String = "c:\banco\prueba.xml"
    '    Try
    '        'mensajeError = "Generando fichero..."
    '        listaContenido = DbContext.CrearFicheroRemesa(numeroRemesa, codigo).ToList
    '        strContenido = ""
    '        For Each linea In listaContenido
    '            strContenido = strContenido + linea
    '        Next
    '        contenidoFichero = XDocument.Parse(strContenido)
    '        contenidoFichero.Save(nombreFichero)
    '        mensajeError = "Fichero " + nombreFichero + " creado correctamente"
    '    Catch ex As Exception
    '        If IsNothing(ex.InnerException) Then
    '            mensajeError = ex.Message
    '        Else
    '            mensajeError = ex.InnerException.Message
    '        End If
    '    End Try
    'End Sub

#End Region

    Private Function rutaMandato() As String
        Return ruta + empresaActual.Trim + "_" + clienteActual.Trim + "_" + contactoActual.Trim + "_" + cuentaActiva.Número.Trim + ".pdf"
    End Function


End Class

Public Class StringTrimmingConverter
    Implements IValueConverter

    Public Function ConvertBack(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function

    Public Function Convert(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.Convert
        If IsNothing(value) Then
            Return ""
        Else
            Return value.ToString().Trim
        End If

    End Function

End Class

Public Class datosBancoConverter
    Implements IValueConverter

    Public Function ConvertBack(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function

    Public Function Convert(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.Convert
        If IsNothing(value) Then
            Return "Sin datos de banco"
        Else
            Return "Tiene datos de banco"
        End If
    End Function

End Class

Public Class lineaVentaAgrupada
    Private Property _producto As String
    Public Property producto As String
        Get
            Return _producto
        End Get
        Set(value As String)
            _producto = value
        End Set
    End Property

    Private Property _nombre As String
    Public Property nombre As String
        Get
            Return _nombre
        End Get
        Set(value As String)
            _nombre = value
        End Set
    End Property

    Private Property _cantidad As Integer
    Public Property cantidad As Integer
        Get
            Return _cantidad
        End Get
        Set(value As Integer)
            _cantidad = value
        End Set
    End Property

    Private Property _fechaUltVenta As Date
    Public Property fechaUltVenta As Date
        Get
            Return _fechaUltVenta
        End Get
        Set(value As Date)
            _fechaUltVenta = value
        End Set
    End Property

    Private Property _subGrupo As String
    Public Property subGrupo As String
        Get
            Return _subGrupo
        End Get
        Set(value As String)
            _subGrupo = value
        End Set
    End Property
End Class