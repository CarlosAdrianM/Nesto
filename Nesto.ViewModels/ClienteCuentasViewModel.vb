Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Input
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Models
Imports Microsoft.Win32
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.Rapports
Imports Newtonsoft.Json
Imports CommunityToolkit.Mvvm.Input
Imports CommunityToolkit.Mvvm.ComponentModel

''' <summary>
''' Nesto#646 (paso 2): la ficha de cuentas bancarias y mandato (Ventanas/Clientes.xaml). Antes era la mitad de
''' ClientesViewModel; el código se ha movido tal cual. Lo común (empresa/cliente/contacto y la carga de la ficha) está en
''' <see cref="ClienteFichaViewModelBase"/>.
''' </summary>
Public Class ClienteCuentasViewModel
    Inherits ClienteFichaViewModelBase

    Private ruta As String

    Public Structure tipoIdDescripcion
        Public Sub New(
       ByVal _id As String,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub

        Public Property id As String
        Public Property descripcion As String
    End Structure

    Public Sub New()
        ' Este ViewModel se usa con dos Views y eso es un desastre.
        ' Deberíamos separarlo en dos ViewModels diferentes, uno para Clientes y otro para ClientesComercial
        '***************************
        configuracion = Nesto.Infrastructure.Contracts.Contenedor.Actual.Resolver(GetType(IConfiguracion))
        servicioAutenticacion = Nesto.Infrastructure.Contracts.Contenedor.Actual.Resolver(GetType(IServicioAutenticacion))
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
        ' Nesto#340 (1C.8, slice 4): actualizarCliente carga la ficha por la API en las dos vistas,
        ' así que el servicio también hace falta en este constructor.
        servicio = New ClienteComercialService(configuracion, servicioAutenticacion)

        ' cargarDatos va AL FINAL, y no es un detalle de estilo: antes se llamaba lo PRIMERO,
        ' confiando en que "no usa el servicio hasta después de su primer Await". Eso dejó de ser
        ' cierto al pasar las empresas a la API (#340 1C.8): la primera línea de cargarDatos es
        ' `Await servicio.LeerEmpresas()`, y la LLAMADA se evalúa ANTES de que el Await suspenda.
        ' Con servicio a Nothing, abrir Clientes -> Ficha daba "Object reference not set to an
        ' instance of an object" y el combo de empresas se quedaba vacío (31/08/2026).
        cargarDatos()
    End Sub

    ' Nesto#340 (1C.8, slice 4): constructor para tests. Inyecta fakes, no toca EF ni el contenedor y no dispara
    ' cargarDatos. Nesto#646: misma firma que el de ClienteComercialViewModel para que los tests solo cambien el tipo;
    ' servicioRapports y abridorPedidos no se usan en esta ficha.
    Public Sub New(configuracion As IConfiguracion, dialogService As IServicioDialogos, servicio As IClienteComercialService, servicioRapports As IRapportService, servicioAutenticacion As IServicioAutenticacion, Optional abridorPedidos As IAbridorPedidos = Nothing)
        Me.configuracion = configuracion
        Me.dialogService = dialogService
        Me.servicio = servicio
        Me.servicioAutenticacion = servicioAutenticacion
    End Sub

#Region "Propiedades"
    ''' <summary>Carlos 17/11/26: cuál es la cuenta del cliente (Clientes.CCC, la de los recibos), junto al combo de cuentas.</summary>
    Public ReadOnly Property textoCuentaDelCliente As String
        Get
            If IsNothing(clienteActivo) Then
                Return String.Empty
            End If
            If String.IsNullOrWhiteSpace(clienteActivo.ccc) Then
                Return "El cliente no tiene cuenta asignada"
            End If
            Return "Cuenta del cliente: " + clienteActivo.ccc.Trim()
        End Get
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

    Private _listaSecuencias As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaSecuencias As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaSecuencias
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaSecuencias = value
            OnPropertyChanged("listaSecuencias")
        End Set
    End Property

    Private _listaTipos As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaTipos As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaTipos
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaTipos = value
            OnPropertyChanged("listaTipos")
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 5): los avisos de efectos con otro CCC vienen del PUT Clientes/CCCs
    ' como POCO ligero (FechaVto/Concepto/ImportePdte/CCC, lo único que bindea el grid).
    Private _extractoCCC As ObservableCollection(Of ExtractoCCCModel)
    Public Property extractoCCC() As ObservableCollection(Of ExtractoCCCModel)
        Get
            Return _extractoCCC
        End Get
        Set(ByVal value As ObservableCollection(Of ExtractoCCCModel))
            _extractoCCC = value
            OnPropertyChanged("extractoCCC")
            OnPropertyChanged("estaVisibleExtractoCCC")
        End Set
    End Property

    Private _pedidosCCC As ObservableCollection(Of cabeceraPedidoAgrupada)
    Public Property pedidosCCC() As ObservableCollection(Of cabeceraPedidoAgrupada)
        Get
            Return _pedidosCCC
        End Get
        Set(ByVal value As ObservableCollection(Of cabeceraPedidoAgrupada))
            _pedidosCCC = value
            OnPropertyChanged("pedidosCCC")
            OnPropertyChanged("estaVisiblePedidosCCC")
        End Set
    End Property

    Public ReadOnly Property estaVisibleExtractoCCC As Visibility
        Get
            If Not IsNothing(extractoCCC) AndAlso extractoCCC.Count > 0 Then
                Return Visibility.Visible
            Else
                Return Visibility.Hidden
            End If
        End Get
    End Property

    Public ReadOnly Property estaVisiblePedidosCCC As Visibility
        Get
            If Not IsNothing(pedidosCCC) AndAlso pedidosCCC.Count > 0 Then
                Return Visibility.Visible
            Else
                Return Visibility.Hidden
            End If
        End Get
    End Property

#End Region
#Region "Comandos"
    Private _cmdGuardar As ICommand
    Public ReadOnly Property cmdGuardar() As ICommand
        Get
            If _cmdGuardar Is Nothing Then
                _cmdGuardar = ComandosReevaluables.Crear(AddressOf Guardar, AddressOf CanGuardar)
            End If
            Return _cmdGuardar
        End Get
    End Property
    ' Nesto#340 (1C.8, slice 5): el dirty pasa del ChangeTracker de EF al flag EsModificado de
    ' los POCOs CCCModel (mismo patrón que AlquilerModel en 1C.3).
    ' Regresión ELMAH 30/09/26 (PK_CCC, cliente 41978): mientras se guarda, el botón no se puede volver a
    ' pulsar. Con el servidor tardando, siete clics mandaban siete PUT que se soltaban a la vez.
    Private _guardando As Boolean
    Private Function CanGuardar(ByVal param As Object) As Boolean
        Return Not _guardando AndAlso Not IsNothing(cuentaActiva) AndAlso Not IsNothing(cuentasBanco) AndAlso
            cuentasBanco.Any(Function(c) c.EsModificado) AndAlso
            (File.Exists(rutaMandato) OrElse (cuentaActiva.Estado <> 5 And cuentaActiva.Estado <> 1))
    End Function
    ' Nesto#340 (1C.8, slice 5): guardar pasa por PUT api/Clientes/CCCs. El servidor hace el
    ' upsert de los CCC modificados y devuelve los avisos (efectos pendientes y pedidos abiertos
    ' que apuntan a OTRO CCC) que antes calculaba este VM con dos consultas EF.
    Private Async Sub Guardar(ByVal param As Object)
        Await GuardarCuentasAsync(establecerComoCuentaDelCliente:=False)
    End Sub

    ' Carlos 17/11/26 (cliente 37480/2): elegir otra cuenta en el combo y guardar NO cambia la cuenta del cliente (a
    ' propósito: se puede corregir una cuenta vieja sin querer cambiar la de los recibos). Para eso está este botón, que
    ' manda en la MISMA llamada las cuentas con cambios sin guardar y la orden de usar la elegida (PUT Clientes/CCCs con
    ' establecerComoCuentaDelCliente).
    Private _cmdUsarCuentaParaRecibos As ICommand
    Public ReadOnly Property cmdUsarCuentaParaRecibos() As ICommand
        Get
            If _cmdUsarCuentaParaRecibos Is Nothing Then
                _cmdUsarCuentaParaRecibos = ComandosReevaluables.Crear(AddressOf UsarCuentaParaRecibos, AddressOf CanUsarCuentaParaRecibos)
            End If
            Return _cmdUsarCuentaParaRecibos
        End Get
    End Property

    ' Habilitado con una cuenta elegida que ya está guardada (no recién creada con «Nuevo Mandato»), que no es la del
    ' cliente y que no está anulada. Si hay cuentas con cambios, se guardan a la vez: valen las mismas condiciones del
    ' mandato que en «Guardar Cambios».
    Private Function CanUsarCuentaParaRecibos(ByVal param As Object) As Boolean
        If _guardando OrElse IsNothing(cuentaActiva) OrElse IsNothing(cuentasBanco) OrElse IsNothing(clienteActivo) Then
            Return False
        End If
        If cuentaActiva.EsNueva OrElse String.IsNullOrWhiteSpace(cuentaActiva.Número) OrElse cuentaActiva.Estado = ESTADO_CCC_ANULADA Then
            Return False
        End If
        If cuentaActiva.Número.Trim() = clienteActivo.ccc?.Trim() Then
            Return False
        End If
        If cuentasBanco.Any(Function(c) c.EsModificado) AndAlso
            Not (File.Exists(rutaMandato) OrElse (cuentaActiva.Estado <> 5 And cuentaActiva.Estado <> 1)) Then
            Return False
        End If
        Return True
    End Function

    Private Async Sub UsarCuentaParaRecibos(ByVal param As Object)
        Await GuardarCuentasAsync(establecerComoCuentaDelCliente:=True)
    End Sub

    Private Async Function GuardarCuentasAsync(establecerComoCuentaDelCliente As Boolean) As Task
        ' Fuera del Try: si ya se está guardando, el Finally de esta llamada no debe soltar el guardado de la otra.
        If _guardando Then
            Return
        End If
        Try
            ' Nesto#396: sin cuenta activa no hay nada que guardar. CanGuardar ya lo filtra, pero el comando
            ' puede dispararse en un teardown con cuentaActiva ya a Nothing; sin esta guarda, cuentaActiva.Número
            ' abajo lanza NRE.
            If IsNothing(cuentaActiva) OrElse IsNothing(cuentasBanco) Then
                Return
            End If

            Dim modificados = cuentasBanco.Where(Function(c) c.EsModificado).ToList()
            If Not modificados.Any() AndAlso Not establecerComoCuentaDelCliente Then
                Return
            End If
            _guardando = True
            CommandManager.InvalidateRequerySuggested()

            Dim cuentaElegida As CCCModel = cuentaActiva
            Dim fichaCargada As ClienteJson = clienteActivo
            Dim peticion As New GuardarCCCsRequest With {
                .empresa = empresaActual,
                .cliente = clienteActual,
                .contacto = contactoActual,
                .cccActivo = cuentaElegida.Número,
                .cccs = modificados,
                .establecerComoCuentaDelCliente = establecerComoCuentaDelCliente
            }
            Dim respuesta As GuardarCCCsRespuesta = Await servicio.GuardarCCCs(peticion)

            extractoCCC = New ObservableCollection(Of ExtractoCCCModel)(If(respuesta?.extractoOtroCCC, New List(Of ExtractoCCCModel)))
            pedidosCCC = New ObservableCollection(Of cabeceraPedidoAgrupada)(If(respuesta?.pedidosOtroCCC, New List(Of cabeceraPedidoAgrupada)))

            For Each ccc In modificados
                ccc.EsModificado = False
                ccc.EsNueva = False
            Next
            If establecerComoCuentaDelCliente AndAlso Not IsNothing(fichaCargada) Then
                fichaCargada.ccc = If(respuesta?.cccCliente, cuentaElegida.Número?.Trim())
                OnPropertyChanged(NameOf(textoCuentaDelCliente))
            End If
            mensajeError = ""
        Catch ex As Exception
            ' Nesto#396: ex.InnerException puede ser Nothing (p.ej. un NullReferenceException pelado). Usar
            ' ex.Message en ese caso, para no provocar un SEGUNDO NRE dentro del Catch que escapa como
            ' DispatcherUnhandledException y tumba la aplicación (mismo patrón que VerMandato/NuevoMandato).
            ' Nesto#399: EF anida el error real (SqlException / raiserror de trigger) en
            ' DbUpdateException→UpdateException, cuyos mensajes son genéricos ("An error occurred when
            ' updating the entries…"). Mostramos la excepción MÁS INTERNA (el error real, accionable) y
            ' registramos la excepción completa en ELMAH vía /api/Errores para poder diagnosticarla.
            mensajeError = ExcepcionMasInterna(ex).Message
            Dim unused2 = servicioErrores?.RegistrarErrorAsync(ex, If(establecerComoCuentaDelCliente,
                "ClientesViewModel.UsarCuentaParaRecibos (CCC del cliente)", "ClientesViewModel.Guardar (Nuevo Mandato / CCC)"))
        Finally
            If _guardando Then
                _guardando = False
                CommandManager.InvalidateRequerySuggested()
            End If
        End Try

    End Function

    ' Nesto#399: EF anida el error real varias capas (DbUpdateException→UpdateException→SqlException).
    ' Devuelve la excepción más interna para mostrar/loguear el mensaje real y no el genérico de EF.
    Private Shared Function ExcepcionMasInterna(ByVal ex As Exception) As Exception
        Dim actual As Exception = ex
        While actual.InnerException IsNot Nothing
            actual = actual.InnerException
        End While
        Return actual
    End Function

    Private _cmdVerMandato As ICommand
    Public ReadOnly Property cmdVerMandato() As ICommand
        Get
            If _cmdVerMandato Is Nothing Then
                _cmdVerMandato = ComandosReevaluables.Crear(AddressOf VerMandato, AddressOf CanVerMandato)
            End If
            Return _cmdVerMandato
        End Get
    End Property
    Private Function CanVerMandato(ByVal param As Object) As Boolean
        'Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)
        If IsNothing(cuentaActiva) Then
            Return False
        ElseIf Not File.Exists(rutaMandato) Then
            Return False
        Else
            Return True
        End If
    End Function
    Private Sub VerMandato(ByVal param As Object)
        Try
            Dim fileName As String = rutaMandato()
            If String.IsNullOrEmpty(fileName) Then
                mensajeError = "Esta cuenta no tiene mandato que enseñar."
                Return
            End If
            Dim process As New System.Diagnostics.Process
            process.StartInfo.FileName = fileName
            process.StartInfo.UseShellExecute = True
            Dim unused = process.Start()
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
                _cmdNuevoMandato = ComandosReevaluables.Crear(AddressOf NuevoMandato, AddressOf CanNuevoMandato)
            End If
            Return _cmdNuevoMandato
        End Get
    End Property
    Private Function CanNuevoMandato(ByVal param As Object) As Boolean
        Return True
    End Function
    ' Nesto#340 (1C.8, slice 5): el CCC nuevo es un POCO marcado como modificado; se persiste
    ' al Guardar (PUT api/Clientes/CCCs hace el upsert), igual que antes hacía SaveChanges.
    Private Sub NuevoMandato(ByVal param As Object)
        Try
            Dim siguienteNumero As Integer
            If IsNothing(cuentasBanco) Then
                cuentasBanco = New ObservableCollection(Of CCCModel)
            End If
            If cuentasBanco.Count = 0 Then
                siguienteNumero = 1
            Else
                siguienteNumero = CInt(cuentasBanco.Where(Function(x) x.Cliente.Trim = clienteActual).OrderBy(Function(x) x.Número).LastOrDefault.Número) + 1
            End If
            cuentaActiva = New CCCModel With {
                .Empresa = empresaActual,
                .Cliente = clienteActual,
                .Contacto = contactoActual,
                .Número = siguienteNumero.ToString(),
                .Estado = 0,
                .Secuencia = "FRST",
                .EsModificado = True,
                .EsNueva = True
            }
            cuentasBanco.Add(cuentaActiva)
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
                _cmdAsignarMandato = ComandosReevaluables.Crear(AddressOf AsignarMandato, AddressOf CanAsignarMandato)
            End If
            Return _cmdAsignarMandato
        End Get
    End Property
    Private Function CanAsignarMandato(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub AsignarMandato(ByVal param As Object)
        Dim elegirFichero = New OpenFileDialog With {
            .Filter = "pdf files (*.pdf)|*.pdf|All files (*.*)|*.*",
            .FilterIndex = 1,
            .RestoreDirectory = True
        }

        If elegirFichero.ShowDialog() Then
            Try
                ' Incidente 13/10/26: sin cuenta, contacto o ruta de mandatos no hay dónde copiarlo.
                If String.IsNullOrEmpty(rutaMandato()) Then
                    mensajeError = "Elige primero el cliente, el contacto y la cuenta a la que asignar el mandato."
                    Return
                End If
                selectedPath = elegirFichero.FileName
                FileIO.FileSystem.CopyFile(
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

    Private _imprimirMandatoCommand As ICommand
    Public ReadOnly Property ImprimirMandatoCommand() As ICommand
        Get
            If _imprimirMandatoCommand Is Nothing Then
                _imprimirMandatoCommand = ComandosReevaluables.Crear(AddressOf OnImprimirMandato, AddressOf CanImprimirMandato)
            End If
            Return _imprimirMandatoCommand
        End Get
    End Property
    Private Function CanImprimirMandato(ByVal param As Object) As Boolean
        Return Not String.IsNullOrEmpty(empresaActual) AndAlso Not String.IsNullOrEmpty(clienteActual) AndAlso
            Not String.IsNullOrEmpty(contactoActual) AndAlso Not IsNothing(cuentaActiva) AndAlso Not String.IsNullOrEmpty(cuentaActiva.Número)
    End Function
    Private Async Sub OnImprimirMandato(ByVal param As Object)
        estaOcupado = True
        Try
            ' Nesto#490 / Nesto#340: el mandato lo pinta NestoAPI; con MotorPdfMandatoSepa = QuestPDF pide el
            ' generador nuevo (&motor=QuestPDF) y si no, el RDLC de siempre. Va al cliente → piloto por usuario.
            Dim motorPdf As String = Await LeerMotorPdfMandato()
            Dim urlPdf As String = ConstruirUrlMandatoPdf(configuracion.servidorAPI, empresaActual, clienteActual, contactoActual, cuentaActiva.Número, motorPdf)
            Dim unused = Process.Start(New ProcessStartInfo(urlPdf) With {
                .UseShellExecute = True
            })
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



    ' Nesto#563 (NestoAPI#724, sugerencia 831): el mandato por correo, sin descargarlo (Outlook web no adjunta
    ' ficheros del escritorio remoto). Mismo diálogo que el pedido y la factura.
    Private _envioPorCorreo As ControlesUsuario.Services.IEnvioDocumentoPorCorreo
    ''' <summary>Nesto#563: el diálogo de envío + la llamada a la API. Friend para los tests.</summary>
    Friend Property EnvioPorCorreo As ControlesUsuario.Services.IEnvioDocumentoPorCorreo
        Get
            If _envioPorCorreo Is Nothing Then
                ' Con el constructor sin parámetros (la ficha) no llega el servicio de diálogos: se pide al contenedor
                Dim dialogos As IServicioDialogos = dialogService
                If dialogos Is Nothing Then
                    Try
                        dialogos = CType(Nesto.Infrastructure.Contracts.Contenedor.Actual.Resolver(GetType(IServicioDialogos)), IServicioDialogos)
                    Catch
                        dialogos = Nothing
                    End Try
                End If
                If dialogos IsNot Nothing AndAlso _clienteApiFactory IsNot Nothing Then
                    _envioPorCorreo = New ControlesUsuario.Services.EnvioDocumentoPorCorreo(
                        New ControlesUsuario.Services.ServicioEnvioCorreoDocumentos(_clienteApiFactory), dialogos)
                End If
            End If
            Return _envioPorCorreo
        End Get
        Set(value As ControlesUsuario.Services.IEnvioDocumentoPorCorreo)
            _envioPorCorreo = value
        End Set
    End Property

    Private _enviarMandatoPorCorreoCommand As ICommand
    ''' <summary>Nesto#563: «Enviar por correo…» junto a «Imprimir» del mandato SEPA de la cuenta activa.</summary>
    Public ReadOnly Property EnviarMandatoPorCorreoCommand() As ICommand
        Get
            If _enviarMandatoPorCorreoCommand Is Nothing Then
                _enviarMandatoPorCorreoCommand = ComandosReevaluables.Crear(AddressOf OnEnviarMandatoPorCorreo, AddressOf CanImprimirMandato)
            End If
            Return _enviarMandatoPorCorreoCommand
        End Get
    End Property
    Private Async Sub OnEnviarMandatoPorCorreo(ByVal param As Object)
        Try
            Await EnviarMandatoPorCorreoAsync()
        Catch ex As Exception
            mensajeError = If(ex.InnerException?.Message, ex.Message)
        End Try
    End Sub

    Friend Async Function EnviarMandatoPorCorreoAsync() As Task
        If Not CanImprimirMandato(Nothing) OrElse EnvioPorCorreo Is Nothing Then
            Return
        End If
        ' El mismo motor que al imprimirlo (parámetro MotorPdfMandatoSepa del usuario)
        Dim motorPdf As String = Await LeerMotorPdfMandato()
        Await EnvioPorCorreo.EnviarMandato(empresaActual?.Trim(), clienteActual?.Trim(), contactoActual?.Trim(), cuentaActiva.Número?.Trim(), motorPdf)
    End Function

    ''' <summary>Nesto#490: URL del PDF del mandato SEPA. Solo con motor "QuestPDF" se añade &amp;motor=QuestPDF.</summary>
    Public Shared Function ConstruirUrlMandatoPdf(servidorAPI As String, empresa As String, cliente As String, contacto As String, ccc As String, motorPdf As String) As String
        Dim url As String = $"{servidorAPI}Clientes/MandatoPDF?empresa={empresa?.Trim}&cliente={cliente?.Trim}&contacto={contacto?.Trim}&ccc={ccc?.Trim}"
        If String.Equals(motorPdf?.Trim, "QuestPDF", StringComparison.OrdinalIgnoreCase) Then
            url &= "&motor=QuestPDF"
        End If
        Return url
    End Function

    ''' <summary>Nesto#490: lee MotorPdfMandatoSepa del usuario. Nunca falla: ante cualquier error, RDLC.</summary>
    Public Async Function LeerMotorPdfMandato() As Task(Of String)
        Try
            Return Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.MotorPdfMandatoSepa)
        Catch
            Return "RDLC"
        End Try
    End Function

    Private Async Function CargarMandato(empresa As String, cliente As String, contacto As String, ccc As String) As Task(Of Byte())

        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim response As HttpResponseMessage
            Dim respuesta As Byte()

            Try
                Dim urlConsulta As String = "Clientes"
                urlConsulta += "?empresa=" + empresa.Trim
                urlConsulta += "&cliente=" + cliente.Trim
                urlConsulta += "&contacto=" + contacto.Trim
                urlConsulta += "&ccc=" + ccc.Trim

                response = Await client.GetAsync(urlConsulta).ConfigureAwait(True)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsByteArrayAsync()
                Else
                    respuesta = Nothing
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar el mandato desde el servidor")
            Finally

            End Try

            Return respuesta
        End Using


    End Function

#End Region

#Region "Funciones"
    ' Incidente 13/10/26 (Laura Magán): con la cuenta del cliente anterior aún activa y el contacto a
    ' Nothing (el combo de contactos lo vacía al cambiar de cliente si la lista llega vacía), el .Trim
    ' lanzaba NRE dentro de CanVerMandato/CanGuardar en cada RequerySuggested: 174 errores en 7 minutos
    ' y Nesto imposible de cerrar. Si falta cualquier parte, no hay ruta de mandato (Nothing): los
    ' llamantes lo tratan como «no hay fichero».
    Private Function rutaMandato() As String
        If IsNothing(cuentaActiva) OrElse String.IsNullOrWhiteSpace(ruta) OrElse
            String.IsNullOrWhiteSpace(empresaActual) OrElse String.IsNullOrWhiteSpace(clienteActual) OrElse
            String.IsNullOrWhiteSpace(contactoActual) OrElse String.IsNullOrWhiteSpace(cuentaActiva.Número) Then
            Return Nothing
        End If
        Return ruta + empresaActual.Trim + "_" + clienteActual.Trim + "_" + contactoActual.Trim + "_" + cuentaActiva.Número.Trim + ".pdf"
    End Function

    ' Nesto#646: lo que esta ficha hacía dentro de cargarDatos, ActualizarClienteAsync y el setter de clienteActivo.
    Protected Overrides Async Function CargarDatosAntesDeElegirClienteAsync(mainViewModel As MainViewModel, empresaDefecto As String) As Task
        ruta = Await mainViewModel.leerParametro(empresaActual, "RutaMandatos")
    End Function

    Protected Overrides Sub CargarDatosDespuesDeElegirCliente()
        listaSecuencias = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion("FRST", "Primer adeudo recurrente"),
            New tipoIdDescripcion("RCUR", "Resto de adeudos recurrentes"),
            New tipoIdDescripcion("OOFF", "Operación de un único pago"),
            New tipoIdDescripcion("FNAL", "Último adeudo recurrente")
        }

        listaTipos = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(1, "Consumidor final"),
            New tipoIdDescripcion(2, "Profesional")
        }
    End Sub

    Protected Overrides Sub AlCargarFicha()
        extractoCCC = Nothing
        pedidosCCC = Nothing
    End Sub

    Protected Overrides Sub AlCambiarClienteActivo(value As ClienteJson)
        OnPropertyChanged(NameOf(textoCuentaDelCliente))
    End Sub

    ' Nesto#644 (3): el cliente no se ha podido leer: fuera también los avisos del CCC del anterior
    Protected Overrides Sub AlVaciarFicha()
        extractoCCC = Nothing
        pedidosCCC = Nothing
    End Sub
#End Region
End Class
