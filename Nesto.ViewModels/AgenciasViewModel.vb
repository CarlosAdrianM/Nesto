Imports System.Windows.Input
Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.ComponentModel
Imports System.Windows
Imports System.Net
Imports System.IO
Imports System.Windows.Controls
Imports System.Text.RegularExpressions
Imports System.Data.Objects



Public Class AgenciasViewModel
    Inherits ViewModelBase

    Const CARGO_AGENCIA = 26
    Const COD_PAIS As String = "34"
    Const ESTADO_INICIAL_ENVIO = 0
    Const ESTADO_TRAMITADO_ENVIO = 1

    Private Shared DbContext As NestoEntities
    Dim mainModel As New Nesto.Models.MainModel
    Dim empresaDefecto As String = String.Format("{0,-3}", mainModel.leerParametro("1", "EmpresaPorDefecto"))


    Public Structure tipoIdDescripcion
        Public Sub New( _
       ByVal _id As Byte,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As Byte
        Property descripcion As String
    End Structure

    Public Sub New()
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        DbContext = New NestoEntities
        listaEmpresas = New ObservableCollection(Of Empresas)(From c In DbContext.Empresas)
        empresaSeleccionada = (From e In DbContext.Empresas Where e.Número = empresaDefecto).FirstOrDefault
        listaAgencias = New ObservableCollection(Of AgenciasTransporte)(From c In DbContext.AgenciasTransporte Where c.Empresa = empresaSeleccionada.Número)
        agenciaSeleccionada = listaAgencias.FirstOrDefault
        listaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion)
        retornoActual = New tipoIdDescripcion(0, "Sin Retorno")
        listaTiposRetorno.Add(retornoActual)
        listaTiposRetorno.Add(New tipoIdDescripcion(1, "Con Retorno"))
        listaTiposRetorno.Add(New tipoIdDescripcion(2, "Retorno Opcional"))
        bultos = 1
        listaServicios = New ObservableCollection(Of tipoIdDescripcion)
        servicioActual = New tipoIdDescripcion(1, "Courier")
        listaServicios.Add(servicioActual)
        listaServicios.Add(New tipoIdDescripcion(37, "Economy"))
        listaServicios.Add(New tipoIdDescripcion(54, "EuroEstándar"))
        listaHorarios = New ObservableCollection(Of tipoIdDescripcion)
        horarioActual = New tipoIdDescripcion(3, "ASM24")
        listaHorarios.Add(horarioActual)
        listaHorarios.Add(New tipoIdDescripcion(2, "ASM14"))
        listaHorarios.Add(New tipoIdDescripcion(18, "Economy"))
        listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado = ESTADO_INICIAL_ENVIO)
        envioActual = listaEnvios.LastOrDefault
        numeroPedido = mainModel.leerParametro(empresaDefecto, "UltNumPedidoVta")
        fechaFiltro = Today
        listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Fecha = fechaFiltro And e.Estado = ESTADO_TRAMITADO_ENVIO)
    End Sub

#Region "Propiedades"
    ' Carlos 03/09/14
    ' Las propiedades que terminan en "envio" son las que se usan de manera temporal para que el usuario
    ' pueda modificar los datos. Por ejemplo, nombreEnvio se actualiza con el nombre del cliente cada vez que
    ' cambiamos de pedido, pero el usuario puede modificarlas. En el momento de hacer la inserción en la tabla
    ' EnviosAgencia coge el valor que tenga esta propiedad. Así permitimos hacer excepciones y no hay que 
    ' mandarlo siempre con el valor que tiene el campo en la tabla.

    Private Property _resultadoWebservice As String
    Public Property resultadoWebservice As String
        Get
            Return _resultadoWebservice
        End Get
        Set(value As String)
            _resultadoWebservice = value
        End Set
    End Property

    Private Property _listaAgencias As ObservableCollection(Of AgenciasTransporte)
    Public Property listaAgencias As ObservableCollection(Of AgenciasTransporte)
        Get
            Return _listaAgencias
        End Get
        Set(value As ObservableCollection(Of AgenciasTransporte))
            _listaAgencias = value
            OnPropertyChanged("listaAgencias")
        End Set
    End Property

    Private Property _agenciaSeleccionada As AgenciasTransporte
    Public Property agenciaSeleccionada As AgenciasTransporte
        Get
            Return _agenciaSeleccionada
        End Get
        Set(value As AgenciasTransporte)
            _agenciaSeleccionada = value
            OnPropertyChanged("agenciaSeleccionada")
            listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Estado = ESTADO_INICIAL_ENVIO)
        End Set
    End Property

    Private Property _XMLdeSalida As XDocument
    Public Property XMLdeSalida As XDocument
        Get
            Return _XMLdeSalida
        End Get
        Set(value As XDocument)
            _XMLdeSalida = value
        End Set
    End Property

    Private Property _XMLdeEntrada As XDocument
    Public Property XMLdeEntrada As XDocument
        Get
            Return _XMLdeEntrada
        End Get
        Set(value As XDocument)
            _XMLdeEntrada = value
        End Set
    End Property

    Private Property _empresaSeleccionada As Empresas
    Public Property empresaSeleccionada As Empresas
        Get
            Return _empresaSeleccionada
        End Get
        Set(value As Empresas)
            _empresaSeleccionada = value
            OnPropertyChanged("empresaSeleccionada")
            listaAgencias = New ObservableCollection(Of AgenciasTransporte)(From c In DbContext.AgenciasTransporte Where c.Empresa = empresaSeleccionada.Número)
            agenciaSeleccionada = listaAgencias.FirstOrDefault
            listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Estado = ESTADO_INICIAL_ENVIO)
            listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Fecha = fechaFiltro And e.Estado = ESTADO_TRAMITADO_ENVIO)
            'actualizar lista de pedidos o de envíos, dependiendo de la pestaña que esté seleccionada
            'una vez actualizadas, seleccionar el pedido o el envío actual también
        End Set
    End Property

    Private Property _pedidoSeleccionado As CabPedidoVta
    Public Property pedidoSeleccionado As CabPedidoVta
        Get
            Return _pedidoSeleccionado
        End Get
        Set(value As CabPedidoVta)
            _pedidoSeleccionado = value
            OnPropertyChanged("pedidoSeleccionado")
            reembolso = importeReembolso()
            bultos = 1
            nombreEnvio = pedidoSeleccionado.Clientes.Nombre.Trim
            direccionEnvio = pedidoSeleccionado.Clientes.Dirección.Trim
            poblacionEnvio = pedidoSeleccionado.Clientes.Población.Trim
            provinciaEnvio = pedidoSeleccionado.Clientes.Provincia.Trim
            codPostalEnvio = pedidoSeleccionado.Clientes.CodPostal.Trim
            telefonoEnvio = telefonoUnico(pedidoSeleccionado.Clientes.Teléfono.Trim, "F")
            movilEnvio = telefonoUnico(pedidoSeleccionado.Clientes.Teléfono.Trim, "M")
            correoEnvio = correoUnico()
            observacionesEnvio = pedidoSeleccionado.Comentarios
            attEnvio = nombreEnvio
            If IsNothing(empresaSeleccionada.FechaPicking) Then
                fechaEnvio = Today
            Else
                fechaEnvio = empresaSeleccionada.FechaPicking
            End If
            listaEnviosPedido = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Pedido = pedidoSeleccionado.Número)
        End Set
    End Property

    Private Property _listaTiposRetorno As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaTiposRetorno As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaTiposRetorno
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaTiposRetorno = value
            'OnPropertyChanged("listaTiposRemesa")
        End Set
    End Property

    Private Property _retornoActual As tipoIdDescripcion
    Public Property retornoActual As tipoIdDescripcion
        Get
            Return _retornoActual
        End Get
        Set(value As tipoIdDescripcion)
            _retornoActual = value
            OnPropertyChanged("retornoActual")
        End Set
    End Property

    Private Property _reembolso As Decimal
    Public Property reembolso As Decimal
        Get
            Return _reembolso
        End Get
        Set(value As Decimal)
            _reembolso = value
            OnPropertyChanged("reembolso")
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

    Private Property _listaServicios As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaServicios As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaServicios
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaServicios = value
        End Set
    End Property

    Private Property _servicioActual As tipoIdDescripcion
    Public Property servicioActual As tipoIdDescripcion
        Get
            Return _servicioActual
        End Get
        Set(value As tipoIdDescripcion)
            _servicioActual = value
            OnPropertyChanged("servicioActual")
        End Set
    End Property

    Private Property _listaHorarios As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaHorarios As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaHorarios
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaHorarios = value
        End Set
    End Property

    Private Property _horarioActual As tipoIdDescripcion
    Public Property horarioActual As tipoIdDescripcion
        Get
            Return _horarioActual
        End Get
        Set(value As tipoIdDescripcion)
            _horarioActual = value
            OnPropertyChanged("horarioActual")
        End Set
    End Property

    Private Property _nombreEnvio As String
    Public Property nombreEnvio As String
        Get
            Return _nombreEnvio
        End Get
        Set(value As String)
            _nombreEnvio = value
            OnPropertyChanged("nombreEnvio")
        End Set
    End Property

    Private Property _direccionEnvio As String
    Public Property direccionEnvio As String
        Get
            Return _direccionEnvio
        End Get
        Set(value As String)
            _direccionEnvio = value
            OnPropertyChanged("direccionEnvio")
        End Set
    End Property

    Private Property _poblacionEnvio As String
    Public Property poblacionEnvio As String
        Get
            Return _poblacionEnvio
        End Get
        Set(value As String)
            _poblacionEnvio = value
            OnPropertyChanged("poblacionEnvio")
        End Set
    End Property

    Private Property _provinciaEnvio As String
    Public Property provinciaEnvio As String
        Get
            Return _provinciaEnvio
        End Get
        Set(value As String)
            _provinciaEnvio = value
            OnPropertyChanged("provinciaEnvio")
        End Set
    End Property

    Private Property _codPostalEnvio As String
    Public Property codPostalEnvio As String
        Get
            Return _codPostalEnvio
        End Get
        Set(value As String)
            _codPostalEnvio = value
            OnPropertyChanged("codPostalEnvio")
        End Set
    End Property

    Private Property _telefonoEnvio As String
    Public Property telefonoEnvio As String
        Get
            Return _telefonoEnvio
        End Get
        Set(value As String)
            _telefonoEnvio = value
            OnPropertyChanged("telefonoEnvio")
        End Set
    End Property

    Private Property _movilEnvio As String
    Public Property movilEnvio As String
        Get
            Return _movilEnvio
        End Get
        Set(value As String)
            _movilEnvio = value
            OnPropertyChanged("movilEnvio")
        End Set
    End Property

    Private Property _correoEnvio As String
    Public Property correoEnvio As String
        Get
            Return _correoEnvio
        End Get
        Set(value As String)
            _correoEnvio = value
            OnPropertyChanged("correoEnvio")
        End Set
    End Property

    Private Property _observacionesEnvio As String
    Public Property observacionesEnvio As String
        Get
            Return _observacionesEnvio
        End Get
        Set(value As String)
            _observacionesEnvio = value
            OnPropertyChanged("observacionesEnvio")
        End Set
    End Property

    Private Property _attEnvio As String
    Public Property attEnvio As String
        Get
            Return _attEnvio
        End Get
        Set(value As String)
            _attEnvio = value
            OnPropertyChanged("attEnvio")
        End Set
    End Property

    Private Property _fechaEnvio As Date
    Public Property fechaEnvio As Date
        Get
            Return _fechaEnvio
        End Get
        Set(value As Date)
            _fechaEnvio = value
            OnPropertyChanged("fechaEnvio")
        End Set
    End Property

    Private Property _envioActual As EnviosAgencia
    Public Property envioActual As EnviosAgencia
        Get
            Return _envioActual
        End Get
        Set(value As EnviosAgencia)
            _envioActual = value
            OnPropertyChanged("envioActual")
        End Set
    End Property

    Private Property _listaEnvios As ObservableCollection(Of EnviosAgencia)
    Public Property listaEnvios As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaEnvios
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            _listaEnvios = value
            OnPropertyChanged("listaEnvios")
        End Set
    End Property

    Private Property _fechaFiltro As Date
    Public Property fechaFiltro As Date
        Get
            Return _fechaFiltro
        End Get
        Set(value As Date)
            _fechaFiltro = value
            OnPropertyChanged("fechaFiltro")
            'actualizamos listaPedidos
            listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Fecha = fechaFiltro And e.Estado = 1)
        End Set
    End Property

    'Private Property _listaPedidos As ObservableCollection(Of CabPedidoVta)
    'Public Property listaPedidos As ObservableCollection(Of CabPedidoVta)
    '    Get
    '        Return _listaPedidos
    '    End Get
    '    Set(value As ObservableCollection(Of CabPedidoVta))
    '        _listaPedidos = value
    '        OnPropertyChanged("listaPedidos")
    '    End Set
    'End Property

    Private Property _numeroPedido As Integer
    Public Property numeroPedido As Integer
        Get
            Return _numeroPedido
        End Get
        Set(value As Integer)
            _numeroPedido = value
            pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Empresa = empresaSeleccionada.Número And c.Número = numeroPedido).FirstOrDefault
        End Set
    End Property

    Private Property _numeroMultiusuario As Integer
    Public Property numeroMultiusuario As Integer
        Get
            Return _numeroMultiusuario
        End Get
        Set(value As Integer)
            _numeroMultiusuario = value
            multiusuario = (From m In DbContext.MultiUsuarios Where m.Empresa = empresaSeleccionada.Número And m.Número = numeroMultiusuario).FirstOrDefault
        End Set
    End Property

    Private Property _multiusuario As MultiUsuarios
    Public Property multiusuario As MultiUsuarios
        Get
            Return _multiusuario
        End Get
        Set(value As MultiUsuarios)
            _multiusuario = value
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

    Private Property _listaEnviosPedido As ObservableCollection(Of EnviosAgencia)
    Public Property listaEnviosPedido As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaEnviosPedido
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            _listaEnviosPedido = value
            OnPropertyChanged("listaEnviosPedido")
        End Set
    End Property

    Private Property _listaEnviosTramitados As ObservableCollection(Of EnviosAgencia)
    Public Property listaEnviosTramitados As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaEnviosTramitados
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            _listaEnviosTramitados = value
            OnPropertyChanged("listaEnviosTramitados")
        End Set
    End Property

#End Region

#Region "Commandos"
    Private _cmdTramitar As ICommand
    Public ReadOnly Property cmdTramitar() As ICommand
        Get
            If _cmdTramitar Is Nothing Then
                _cmdTramitar = New RelayCommand(AddressOf Tramitar, AddressOf CanTramitar)
            End If
            Return _cmdTramitar
        End Get
    End Property
    Private Function CanTramitar(ByVal param As Object) As Boolean
        Return Not IsNothing(envioActual)
    End Function
    Private Sub Tramitar(ByVal param As Object)
        llamadaWebService()
        If envioActual.Reembolso > 0 Then
            contabilizarReembolso(envioActual)
        End If
    End Sub

    Private _cmdTramitarTodos As ICommand
    Public ReadOnly Property cmdTramitarTodos() As ICommand
        Get
            If _cmdTramitarTodos Is Nothing Then
                _cmdTramitarTodos = New RelayCommand(AddressOf TramitarTodos, AddressOf CanTramitarTodos)
            End If
            Return _cmdTramitarTodos
        End Get
    End Property
    Private Function CanTramitarTodos(ByVal param As Object) As Boolean
        Return Not IsNothing(listaEnvios) AndAlso listaEnvios.Count > 0
    End Function
    Private Sub TramitarTodos(ByVal param As Object)
        For Each envio In listaEnvios
            envioActual = envio
            cmdTramitar.Execute(Nothing)
        Next
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
        Return Not IsNothing(envioActual)
    End Function
    Private Sub ImprimirEtiquetaPedido(ByVal param As Object)

        Dim puerto As String = mainModel.leerParametro(envioActual.Empresa, "ImpresoraBolsas")

        Dim objFSO
        Dim objStream
        objFSO = CreateObject("Scripting.FileSystemObject")
        objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  
        Dim i As Integer



        Try
            For i = 1 To bultos
                objStream.Writeline("I8,A,034")
                objStream.Writeline("N")
                objStream.Writeline("A40,10,0,4,1,1,N,""" + envioActual.Nombre + """")
                objStream.Writeline("A40,50,0,4,1,1,N,""" + envioActual.Direccion + """")
                objStream.Writeline("A40,90,0,4,1,1,N,""" + envioActual.CodPostal + " " + envioActual.Poblacion + """")
                objStream.Writeline("A40,130,0,4,1,1,N,""" + envioActual.Provincia + """")
                objStream.Writeline("A40,170,0,4,1,1,N,""Bulto: " + i.ToString + "/" + bultos.ToString _
                                    + ". Cliente: " + envioActual.Cliente.Trim + ". Fecha: " + envioActual.Fecha + """")
                objStream.Writeline("B40,210,0,2C,4,8,200,B,""" + envioActual.CodigoBarras + i.ToString("D3") + """")
                objStream.Writeline("A40,450,0,4,1,2,N,""" + envioActual.Nemonico + " " + envioActual.NombrePlaza + """")
                objStream.Writeline("A40,510,0,4,1,2,N,""" + listaHorarios.Where(Function(x) x.id = envioActual.Horario).FirstOrDefault.descripcion + """")
                objStream.Writeline("A590,265,0,5,2,2,N,""" + envioActual.Nemonico + """")
                objStream.Writeline("P1")
                objStream.Writeline("")
            Next

            '' Insertamos la etiqueta en la tabla
            'Dim etiqueta As New EtiquetasPicking With { _
            '.Empresa = envioActual.Empresa,
            '.Número = envioActual.Pedido,
            '.Picking = envioActual.Empresas.MaxPickingListado, 'esto está mal
            '.NºBultos = envioActual.Bultos,
            '.UsuarioQuePrepara = multiusuario.Número,
            '.NºCliente = envioActual.Cliente,
            '.Contacto = envioActual.Contacto}
            'DbContext.AddToEtiquetasPicking(etiqueta)
            'DbContext.SaveChanges()


        Catch ex As Exception
            mensajeError = ex.InnerException.Message
        Finally
            objStream.Close()
            objFSO = Nothing
            objStream = Nothing
        End Try

    End Sub

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
        Return envioActual IsNot Nothing
    End Function
    Private Sub Borrar(ByVal param As Object)
        DbContext.EnviosAgencia.DeleteObject(envioActual)
        listaEnvios.Remove(envioActual)
        envioActual = listaEnvios.LastOrDefault
    End Sub

    Private _cmdInsertar As ICommand
    Public ReadOnly Property cmdInsertar() As ICommand
        Get
            If _cmdInsertar Is Nothing Then
                _cmdInsertar = New RelayCommand(AddressOf Insertar, AddressOf CanInsertar)
            End If
            Return _cmdInsertar
        End Get
    End Property
    Private Function CanInsertar(ByVal param As Object) As Boolean
        Return Not IsNothing(pedidoSeleccionado)
    End Function
    Private Sub Insertar(ByVal param As Object)
        insertarRegistro()
    End Sub

    Private _cmdImprimirEInsertar As ICommand
    Public ReadOnly Property cmdImprimirEInsertar() As ICommand
        Get
            If _cmdImprimirEInsertar Is Nothing Then
                _cmdImprimirEInsertar = New RelayCommand(AddressOf ImprimirEInsertar, AddressOf CanImprimirEInsertar)
            End If
            Return _cmdImprimirEInsertar
        End Get
    End Property
    Private Function CanImprimirEInsertar(ByVal param As Object) As Boolean
        Return CanInsertar(Nothing)
    End Function
    Private Sub ImprimirEInsertar(ByVal param As Object)
        cmdInsertar.Execute(Nothing)
        cmdImprimirEtiquetaPedido.Execute(Nothing)
    End Sub


    'Private _cmdImprimirManifiesto As ICommand
    'Public ReadOnly Property cmdImprimirManifiesto() As ICommand
    '    Get
    '        If _cmdImprimirManifiesto Is Nothing Then
    '            _cmdImprimirManifiesto = New RelayCommand(AddressOf ImprimirManifiesto, AddressOf CanImprimirManifiesto)
    '        End If
    '        Return _cmdImprimirManifiesto
    '    End Get
    'End Property
    'Private Function CanImprimirManifiesto(ByVal param As Object) As Boolean
    '    Return True
    'End Function
    'Private Sub ImprimirManifiesto(ByVal param As Object)
    '
    'End Sub



#End Region

#Region "Funciones de Ayuda"
    Public Sub llamadaWebService()
        XMLdeSalida = construirXMLdeSalida()
        'If IsNothing(envioActual.Agencia) Then
        '    resultadoWebservice = "No se pudo llamar al webservice (no hay ninguna agencia seleccionada)."
        'Else
        '    resultadoWebservice = "0"
        'End If

        'If resultadoWebservice <> "0" Then
        '    Return
        'End If

        'Comenzamos la llamada
        Dim soap As String = "<?xml version=""1.0"" encoding=""utf-8""?>" & _
             "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " & _
              "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " & _
              "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" & _
              "<soap:Body>" & _
                    "<GrabaServicios xmlns=""http://www.asmred.com/"">" & _
                        "<docIn>" & XMLdeSalida.ToString & "</docIn>" & _
                    "</GrabaServicios>" & _
                "</soap:Body>" & _
             "</soap:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("http://www.asmred.com/WebSrvs/b2b.asmx?op=GrabaServicios")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GrabaServicios""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Using stm As Stream = req.GetRequestStream()
            Using stmw As StreamWriter = New StreamWriter(stm)
                stmw.Write(soap)
            End Using
        End Using

        Dim response As WebResponse = req.GetResponse()
        Dim responseStream As New StreamReader(response.GetResponseStream())
        soap = responseStream.ReadToEnd
        XMLdeEntrada = XDocument.Parse(soap)


        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://www.asmred.com/")
        elementoXML = XMLdeEntrada.Descendants(Xns + "GrabaServiciosResult").First().FirstNode
        XMLdeEntrada = New XDocument
        XMLdeEntrada.AddFirst(elementoXML)

        If elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value <> "0" Then
            If elementoXML.Element("Envio").Element("Errores").HasElements Then
                mensajeError = elementoXML.Element("Envio").Element("Errores").Element("Error").Value
            Else
                mensajeError = calcularMensajeError(elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value)
            End If

        Else
            'lo que tenemos que hacer es cambiar el estado del envío: cambiarEstadoEnvio(1)
            envioActual.Estado = 1 'Enviado
            DbContext.SaveChanges()
            mensajeError = "Envío del pedido " + envioActual.Pedido.ToString + " tramitado correctamente."
            listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado = ESTADO_INICIAL_ENVIO)
            envioActual = listaEnvios.LastOrDefault
        End If


        'Debug.Print(XMLdeEntrada.ToString)

    End Sub

    Public Function construirXMLdeSalida() As XDocument
        Dim xml As New XDocument
        'xml = XDocument.Load("C:\Users\Carlos.NUEVAVISION\Desktop\ASM\webservice\XML-IN-B.xml")
        'xml.Descendants("Servicios").FirstOrDefault().Add(New XElement("Servicios", New XAttribute("uidcliente", ""), New XAttribute("xmlns", "http://www.asmred.com/")))

        ' Si no hay envioActual devolvemos el xml vacío
        If IsNothing(envioActual) Then
            Return xml
        End If

        'Añadimos el nodo raíz (Servicios)
        xml.AddFirst(
            <Servicios uidcliente=<%= envioActual.AgenciasTransporte.Identificador %> xmlns="http://www.asmred.com/">
                <Envio codbarras=<%= envioActual.CodigoBarras %>>
                    <Fecha><%= envioActual.Fecha.ToShortDateString %></Fecha>
                    <Portes>P</Portes>
                    <Servicio><%= envioActual.Servicio %></Servicio>
                    <Horario><%= envioActual.Horario %></Horario>
                    <Bultos><%= envioActual.Bultos %></Bultos>
                    <Peso>1</Peso>
                    <Retorno><%= envioActual.Retorno %></Retorno>
                    <Pod>N</Pod>
                    <Remite>
                        <Plaza></Plaza>
                        <Nombre><%= envioActual.Empresas.Nombre.Trim %></Nombre>
                        <Direccion><%= envioActual.Empresas.Dirección.Trim %></Direccion>
                        <Poblacion><%= envioActual.Empresas.Población.Trim %></Poblacion>
                        <Provincia><%= envioActual.Empresas.Provincia.Trim %></Provincia>
                        <Pais>34</Pais>
                        <CP><%= envioActual.Empresas.CodPostal.Trim %></CP>
                        <Telefono><%= envioActual.Empresas.Teléfono.Trim %></Telefono>
                        <Movil></Movil>
                        <Email><%= envioActual.Empresas.Email.Trim %></Email>
                        <Observaciones></Observaciones>
                    </Remite>
                    <Destinatario>
                        <Codigo></Codigo>
                        <Plaza></Plaza>
                        <Nombre><%= envioActual.Nombre %></Nombre>
                        <Direccion><%= envioActual.Direccion %></Direccion>
                        <Poblacion><%= envioActual.Poblacion %></Poblacion>
                        <Provincia><%= envioActual.Provincia %></Provincia>
                        <Pais><%= COD_PAIS %></Pais>
                        <CP><%= envioActual.CodPostal %></CP>
                        <Telefono><%= envioActual.Telefono %></Telefono>
                        <Movil><%= envioActual.Movil %></Movil>
                        <Email><%= envioActual.Email %></Email>
                        <Observaciones><%= envioActual.Observaciones %></Observaciones>
                        <ATT><%= envioActual.Atencion %></ATT>
                    </Destinatario>
                    <Referencias><!-- cualquier numero, siempre distinto a cada prueba-->
                        <Referencia tipo="C"><%= envioActual.Cliente.Trim %>/<%= envioActual.Pedido %></Referencia>
                    </Referencias>
                    <Importes>
                        <Debidos>0</Debidos>
                        <Reembolso><%= envioActual.Reembolso %></Reembolso>
                    </Importes>
                    <Seguro tipo="">
                        <Descripcion></Descripcion>
                        <Importe></Importe>
                    </Seguro>
                    <DevuelveAdicionales>
                        <PlazaDestino/>
                    </DevuelveAdicionales>
                </Envio>
            </Servicios>
        )

        'xml.Root.Attribute("xmlns").Value = "http://www.asmred.com/"
        'Debug.Print(xml.ToString)
        Return xml
    End Function

    Public Function telefonoUnico(listaTelefonos As String, Optional tipo As String = "F") As String
        ' tipo = F -> teléfono fijo
        ' tipo = M -> teléfono móvil

        Dim telefonos() As String
        Dim stringSeparators() As String = {"/"}

        telefonos = listaTelefonos.Split(stringSeparators, StringSplitOptions.None)
        For Each t As String In telefonos
            If (t.Length = 9) And (
                (tipo = "F" And t.Substring(0, 1) = "9") Or
                (tipo = "M" And t.Substring(0, 1) = "6") Or
                (tipo = "M" And t.Substring(0, 1) = "7") Or
                (tipo = "M" And t.Substring(0, 1) = "8")
                ) Then
                Return t
            End If
        Next
        Return ""

    End Function

    Public Function correoUnico(Optional listaPersonas As List(Of PersonasContactoCliente) = Nothing) As String
        Dim correo As String
        Dim personaAgencia As PersonasContactoCliente

        If IsNothing(listaPersonas) Then
            If IsNothing(pedidoSeleccionado.Clientes.PersonasContactoCliente.FirstOrDefault) Then
                Return ""
            Else
                listaPersonas = pedidoSeleccionado.Clientes.PersonasContactoCliente.ToList
            End If
        End If

        If IsNothing(listaPersonas.FirstOrDefault) Then
            Return ""
        End If
        personaAgencia = (From c In listaPersonas Where c.Cargo = CARGO_AGENCIA And c.CorreoElectrónico <> "").FirstOrDefault
        If Not IsNothing(personaAgencia) AndAlso Not IsNothing(personaAgencia.CorreoElectrónico) Then
            correo = personaAgencia.CorreoElectrónico.Trim
            If correo <> "" Then
                Return correo
            End If
        End If

        personaAgencia = (From c In listaPersonas Where c.CorreoElectrónico <> "").FirstOrDefault
        If Not IsNothing(personaAgencia) AndAlso Not IsNothing(personaAgencia.CorreoElectrónico) Then
            correo = personaAgencia.CorreoElectrónico.Trim
            If correo <> "" Then
                Return correo
            End If
        End If

        If IsNothing(listaPersonas.FirstOrDefault.CorreoElectrónico) Then
            Return ""
        Else
            Return listaPersonas.FirstOrDefault.CorreoElectrónico.Trim
        End If



    End Function

    Public Function importeReembolso() As Decimal
        If IsNothing(pedidoSeleccionado) Then
            Return 0
        End If
        If pedidoSeleccionado.CCC IsNot Nothing Then
            Return 0
        End If
        Dim lineas As ObjectQuery(Of LinPedidoVta)
        lineas = (From l In DbContext.LinPedidoVta Where l.Número = pedidoSeleccionado.Número And l.Picking <> 0)
        If Not lineas.Any Then
            Return 0
        End If

        Return Math.Round(
            (Aggregate l In lineas _
            Select l.Total Into Sum()) _
            , 2)
        'Return Math.Round(
        '    (Aggregate l In DbContext.LinPedidoVta _
        '    Where l.Número = pedidoSeleccionado.Número And l.Picking <> 0 _
        '    Select l.Total Into Sum()) _
        '    , 2)
    End Function

    Public Function calcularDigitoControl(ByVal number As String) As Integer

        If (number.Length <> 17) Then
            number = ""
            Throw New System.ArgumentException
        Else
            Dim ch As Char
            For Each ch In number
                If (Not Char.IsNumber(ch)) Then
                    number = ""
                    Throw New System.ArgumentException
                End If
            Next
        End If

        Dim x, digito, sumaCod As Integer

        ' Extraigo el valor del dígito, y voy
        ' sumando los valores resultantes.
        For x = 1 To number.Length
            digito = CInt(number.Substring(x - 1, 1))
            If (x Mod 2) <> 0 Then
                ' Las posiciones impares se multiplican por 3
                sumaCod += (digito * 3)
            Else
                ' Las posiciones pares se multiplican por 1
                sumaCod += (digito * 1)
            End If
        Next

        ' Calculo la decena superior
        digito = (sumaCod Mod 10)

        ' Calculo el dígito de control
        If digito <> 0 Then
            digito = 10 - digito
        End If
        ' Devuelvo el dígito de control
        Return digito

    End Function

    Public Function calcularCodigoBarras() As String
        Return agenciaSeleccionada.PrefijoCodigoBarras.ToString + envioActual.Numero.ToString("D7")
    End Function

    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String)
        'Comenzamos la llamada
        Dim soap As String = "<?xml version=""1.0"" encoding=""utf-8""?>" & _
             "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " & _
              "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " & _
              "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" & _
              "<soap:Body>" & _
                    "<GetPlazaXCP xmlns=""http://www.asmred.com/"">" & _
                        "<codPais>" + COD_PAIS + "</codPais>" & _
                        "<cp>" + codPostal + "</cp>" & _
                    "</GetPlazaXCP>" & _
                "</soap:Body>" & _
             "</soap:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("http://www.asmred.com/WebSrvs/b2b.asmx?op=GetPlazaXCP")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GetPlazaXCP""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Using stm As Stream = req.GetRequestStream()
            Using stmw As StreamWriter = New StreamWriter(stm)
                stmw.Write(soap)
            End Using
        End Using

        Dim response As WebResponse = req.GetResponse()
        Dim responseStream As New StreamReader(response.GetResponseStream())
        soap = responseStream.ReadToEnd

        Dim respuestaXML As XDocument
        respuestaXML = XDocument.Parse(soap)

        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://www.asmred.com/")
        elementoXML = respuestaXML.Descendants(Xns + "GetPlazaXCPResult").First().FirstNode
        respuestaXML = New XDocument
        respuestaXML.AddFirst(elementoXML)

        'Debug.Print(respuestaXML.ToString)
        nemonico = elementoXML.Element("Nemonico").Value
        nombrePlaza = elementoXML.Element("Nombre").Value
        telefonoPlaza = elementoXML.Element("Telefono").Value
        telefonoPlaza = Regex.Replace(telefonoPlaza, "([^0-9])", "")
        'telefonoPlaza = elementoXML.Element("Telefono").Value.Replace(" "c, String.Empty)
        emailPlaza = elementoXML.Element("Mail").Value
    End Sub

    Public Sub insertarRegistro()
        envioActual = New EnviosAgencia
        With envioActual
            .Empresa = pedidoSeleccionado.Empresa
            .Agencia = agenciaSeleccionada.Numero
            .Cliente = pedidoSeleccionado.Nº_Cliente
            .Contacto = pedidoSeleccionado.Contacto
            .Pedido = pedidoSeleccionado.Número
            .Fecha = fechaEnvio
            .Servicio = servicioActual.id
            .Horario = horarioActual.id
            .Bultos = bultos
            .Retorno = retornoActual.id
            .Nombre = nombreEnvio
            .Direccion = direccionEnvio
            .CodPostal = codPostalEnvio
            .Poblacion = poblacionEnvio
            .Provincia = provinciaEnvio
            .Telefono = telefonoEnvio
            .Movil = movilEnvio
            .Email = correoEnvio
            .Observaciones = Left(observacionesEnvio, 80)
            .Atencion = attEnvio
            .Reembolso = reembolso
            '.CodigoBarras = calcularCodigoBarras()
            .Vendedor = pedidoSeleccionado.Vendedor
            calcularPlaza(codPostalEnvio, .Nemonico, .NombrePlaza, .TelefonoPlaza, .EmailPlaza)
        End With

        DbContext.AddToEnviosAgencia(envioActual)
        listaEnvios.Add(envioActual)
        listaEnviosPedido.Add(envioActual)
        DbContext.SaveChanges()
        envioActual.CodigoBarras = calcularCodigoBarras()
    End Sub

    Public Function calcularMensajeError(numeroError As Integer) As String
        Select Case numeroError
            Case -33
                Return "Ya existe el código de barras de la expedición"
            Case -69
                Return "No se pudo canalizar el envío"
            Case -70
                Return "Ya existe se ha enviado este pedido para esta fecha y cliente"
            Case -108
                Return "El nombre del remitente debe tener al menos tres caracteres"
            Case -109
                Return "La dirección del remitente debe tener al menos tres caracteres"
            Case -110
                Return "La población del remitente debe tener al menos tres caracteres"
            Case -111
                Return "El código postal del remitente debe tener al menos cuatro caracteres"
            Case -111
                Return "La referencia del cliente está duplicada"
            Case -119
                Return "Error no controlado por el webservice de la agencia"
            Case -128
                Return "El nombre del destinatario debe tener al menos tres caracteres"
            Case -129
                Return "La dirección del destinatario debe tener al menos tres caracteres"
            Case -130
                Return "La población del destinatario debe tener al menos tres caracteres"
            Case -131
                Return "El código postal del destinatario debe tener al menos cuatro caracteres"
            Case Else
                Return "El código de error " + numeroError + " no está controlado por Nesto"
        End Select
    End Function

    Public Function contabilizarReembolso(envio As EnviosAgencia)

        Const diarioReembolsos As String = "_Reembolso"

        If IsNothing(envio.AgenciasTransporte.CuentaReembolsos) Then
            mensajeError = "Esta agencia no tiene establecida una cuenta de reembolsos. No se puede contabilizar."
            Return -1
        End If

        Dim lineaInsertar As New PreContabilidad
        Dim asiento As Integer

        With lineaInsertar
            .Empresa = envio.Empresa.Trim
            .Diario = diarioReembolsos.Trim
            .TipoApunte = "3" 'Pago
            .TipoCuenta = "2" 'Cliente
            .Nº_Cuenta = envio.Cliente.Trim
            .Contacto = envio.Contacto.Trim
            .Fecha = envio.Fecha
            .FechaVto = envio.Fecha
            .Haber = envio.Reembolso
            .Concepto = Left("S/Pago pedido " + envio.Pedido.ToString + " a " + envio.AgenciasTransporte.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
            .Contrapartida = envio.AgenciasTransporte.CuentaReembolsos.Trim
            .Nº_Documento = envio.Pedido
            .Asiento_Automático = False
            .Delegación = envio.Empresas.DelegaciónVarios
            .FormaVenta = envio.Empresas.FormaVentaVarios
            .FormaPago = envio.Empresas.FormaPagoEfectivo
        End With
        Try
            DbContext.AddToPreContabilidad(lineaInsertar)
            DbContext.SaveChanges()
            asiento = DbContext.prdContabilizar(envio.Empresa, diarioReembolsos)
        Catch e As Exception
            DbContext.DeleteObject(lineaInsertar) 'Lo suyo sería hacer una transacción con todo
            DbContext.SaveChanges()
            Return -1
        End Try

        Return asiento

    End Function

#End Region



End Class


