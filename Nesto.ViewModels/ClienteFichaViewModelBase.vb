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
''' Nesto#646 (paso 2): lo común a las dos fichas de cliente que antes servía un solo ClientesViewModel —
''' <see cref="ClienteCuentasViewModel"/> (Ventanas/Clientes.xaml: cuentas bancarias y mandato) y
''' <see cref="ClienteComercialViewModel"/> (Ventanas/ClienteComercial.xaml: la ficha comercial)—: elegir empresa,
''' cliente y contacto, cargar la ficha del cliente (con sus cuentas) y el arranque (cargarDatos). El código se ha
''' MOVIDO tal cual; lo que hace cada ficha al cargar o al cambiar de cliente va en los ganchos Overridable del final.
''' </summary>
Public MustInherit Class ClienteFichaViewModelBase
    Inherits ObservableObject

    ' Nesto#340 (1C.8): VM 100% sin EF — el DbContext se eliminó con el último resto
    ' (listaEmpresas por API + borrado del código muerto de FamiliasVendedor).
    Public Property configuracion As IConfiguracion
    ' Nesto#646: Protected (antes Private) para los dos ViewModels hijos.
    Protected servicioErrores As IServicioRegistroErrores
    Protected Property dialogService As IServicioDialogos
    Protected Property servicio As IClienteComercialService
    Protected Property servicioAutenticacion As IServicioAutenticacion
    ' Nesto#369: factoría que crea HttpClient con AuthTokenHandler (adjunta el JWT → usuario en ELMAH).
    Protected _clienteApiFactory As IClienteApiFactory

    ''' <summary>Nesto#611: para que los tests pongan una API falsa (el constructor de tests no crea la factoría).</summary>
    Friend Property ClienteApiFactory As IClienteApiFactory
        Get
            Return _clienteApiFactory
        End Get
        Set(value As IClienteApiFactory)
            _clienteApiFactory = value
        End Set
    End Property

    ''' <summary>Nesto#611: para que los tests comprueben qué se registra en ELMAH.</summary>
    Friend Property RegistroErrores As IServicioRegistroErrores
        Get
            Return servicioErrores
        End Get
        Set(value As IServicioRegistroErrores)
            servicioErrores = value
        End Set
    End Property

#Region "Propiedades"
    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            Dim unused = SetProperty(_titulo, value)
        End Set
    End Property

    Private _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            CargarListaContactos()
            If IsNothing(contactoActual) Then
                Dim unused = CargarClienteActualEmpresa()
            End If
            ActualizarClienteAsync(_empresaActual, clienteActual, contactoActual)
            OnPropertyChanged("empresaActual")
        End Set
    End Property

    Private Async Function CargarClienteActualEmpresa() As Task
        Dim mainViewModel = New MainViewModel()
        clienteActual = Await mainViewModel.leerParametro(empresaActual, "UltNumCliente")
    End Function

    ' Nesto#340 (1C.7): la lista de contactos se lee de la API (PlantillaVentas/DireccionesEntrega,
    ' mismo filtro Estado >= 0) en vez de consultar Clientes con EF. Se mantiene la colección de
    ' Clientes como contenedor porque el ComboBox solo usa el campo Contacto y así los bindings
    ' quedan intactos. Al asignar clienteActual el setter relanza la carga, así que no hace falta
    ' recargar tras CargarClienteActualEmpresa.
    Private _servicioDireccionesEntrega As ControlesUsuario.Services.IServicioDireccionesEntrega
    Private ReadOnly Property ServicioDireccionesEntrega As ControlesUsuario.Services.IServicioDireccionesEntrega
        Get
            If _servicioDireccionesEntrega Is Nothing Then
                _servicioDireccionesEntrega = Nesto.Infrastructure.Contracts.Contenedor.Actual.Resolver(GetType(ControlesUsuario.Services.IServicioDireccionesEntrega))
            End If
            Return _servicioDireccionesEntrega
        End Get
    End Property

    Private Async Sub CargarListaContactos()
        Try
            If String.IsNullOrWhiteSpace(_empresaActual) OrElse String.IsNullOrWhiteSpace(_clienteActual) Then
                listaContactos = New ObservableCollection(Of Clientes)()
                Return
            End If
            Dim empresaPedida As String = _empresaActual
            Dim clientePedido As String = _clienteActual
            Dim direcciones = Await ServicioDireccionesEntrega.ObtenerDireccionesEntrega(_empresaActual.Trim(), _clienteActual.Trim())
            ' Nesto#644 (2): si mientras tanto se ha elegido otro cliente (o empresa), estos contactos ya no son suyos
            If empresaPedida <> _empresaActual OrElse clientePedido <> _clienteActual Then
                Return
            End If
            listaContactos = New ObservableCollection(Of Clientes)(
                direcciones.Select(Function(d) New Clientes With {
                    .Empresa = _empresaActual,
                    .Nº_Cliente = _clienteActual,
                    .Contacto = d.contacto
                }))
        Catch ex As Exception
            listaContactos = New ObservableCollection(Of Clientes)()
        End Try
    End Sub

    Private _clienteActual As String
    Public Property clienteActual As String
        Get
            Return _clienteActual
        End Get
        Set(value As String)
            _clienteActual = value
            CargarListaContactos()
            ActualizarClienteAsync(_empresaActual, _clienteActual, _contactoActual)
            OnPropertyChanged(NameOf(clienteActual))
            ' Nesto#340 (1C.8, slice 4): el Titulo se pone en actualizarCliente al terminar la
            ' carga async (aquí clienteActivo aún sería el cliente anterior).
        End Set
    End Property

    Private _contactoActual As String
    Public Property contactoActual As String
        Get
            Return _contactoActual
        End Get
        Set(value As String)
            _contactoActual = value
            ActualizarClienteAsync(_empresaActual, _clienteActual, _contactoActual)
            OnPropertyChanged("contactoActual")
        End Set
    End Property

    ' Nesto#340 (1C.8, último resto EF): POCO del API en vez de la entidad EF Empresas.
    Private _listaEmpresas As ObservableCollection(Of EmpresaModel)
    Public Property listaEmpresas As ObservableCollection(Of EmpresaModel)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of EmpresaModel))
            _listaEmpresas = value
            OnPropertyChanged("listaEmpresas")
        End Set
    End Property

    Private _nombre As String
    Public Property nombre As String
        Get
            Return _nombre
        End Get
        Set(value As String)
            _nombre = value
            OnPropertyChanged("nombre")
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 5): la cuenta activa deja de ser la entidad EF y pasa al POCO
    ' CCCModel (mismos nombres de propiedad que bindea la vista, con dirty flag propio).
    Private _cuentaActiva As CCCModel
    Public Property cuentaActiva As CCCModel
        Get
            Return _cuentaActiva
        End Get
        Set(value As CCCModel)
            _cuentaActiva = value
            OnPropertyChanged("cuentaActiva")
            OnPropertyChanged(NameOf(descripcionEstadoCCC))
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 4): la vista mostraba clienteActivo.CCC2.EstadosCCC.Descripción (dos
    ' nav properties de EF). Con el POCO, la descripción se calcula con la cuenta activa y la
    ' lista de estados que ya está cargada.
    Public ReadOnly Property descripcionEstadoCCC As String
        Get
            If IsNothing(cuentaActiva) OrElse IsNothing(listaEstadosCCC) Then
                Return String.Empty
            End If
            Return listaEstadosCCC.FirstOrDefault(Function(e) e.Número = cuentaActiva.Estado)?.Descripción
        End Get
    End Property

    Private _cuentasBanco As ObservableCollection(Of CCCModel)
    Public Property cuentasBanco As ObservableCollection(Of CCCModel)
        Get
            Return _cuentasBanco
        End Get
        Set(value As ObservableCollection(Of CCCModel))
            _cuentasBanco = value
            OnPropertyChanged("cuentasBanco")
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 4): la ficha del cliente se carga de la API (GET Clientes) en vez de
    ' DbContext.Clientes, y clienteActivo pasa a ser el DTO. La cuenta activa se localiza por el
    ' campo escalar ccc (antes nav property CCC2). Slice 5: cuentasBanco también viene de la API
    ' (GET Clientes/CCCs) como POCOs con dirty flag; ya no queda EF en la carga de la ficha.
    ' El Titulo se pone aquí (antes en el setter de clienteActual) porque la carga es async.
    ' Es Function As Task (no Sub) para que los tests puedan await; los setters la invocan como
    ' sentencia (fire-and-forget) y la excepción queda capturada aquí dentro.
    Public Async Function ActualizarClienteAsync(empresa As String, numCliente As String, contacto As String) As Task
        If IsNothing(empresa) OrElse IsNothing(numCliente) OrElse IsNothing(contacto) Then
            Return
        End If
        ' Nesto#644 (2): cada carga lleva su testigo; si cuando contesta la API ya se ha pedido otra (otro cliente, empresa o
        ' contacto), lo que llega se descarta y no pisa la ficha nueva.
        Dim testigo As Integer = NuevoTestigoFicha()
        Dim errorLectura As Exception = Nothing
        Try
            Dim cliente As ClienteJson = Await servicio.LeerCliente(empresa, numCliente, contacto)
            If Not EsFichaActual(testigo) Then
                Return
            End If
            If IsNothing(cliente) Then
                ' Nesto#644 (3): antes se quedaba entera la ficha del cliente anterior
                VaciarFicha(numCliente, contacto, Nothing)
                Return
            End If
            If Not IsNothing(cliente) Then
                nombre = cliente.nombre
                Dim cccs As List(Of CCCModel) = Await servicio.LeerCCCs(empresa, numCliente, contacto)
                If Not EsFichaActual(testigo) Then
                    Return
                End If
                If mensajeError = _mensajeFichaNoLeida Then
                    mensajeError = Nothing ' el aviso de que no se pudo leer era de la carga anterior
                End If
                cuentasBanco = New ObservableCollection(Of CCCModel)(If(cccs, New List(Of CCCModel)))
                ' Carlos 17/11/26: antes, con un cliente sin ccc (o con un ccc que no está en sus cuentas) cuentaActiva NO se
                ' tocaba y se quedaba la cuenta del cliente ANTERIOR (y Guardar mandaba su número). Ahora siempre se elige
                ' entre las cuentas de ESTE cliente.
                cuentaActiva = CuentaParaMostrar(cuentasBanco, cliente.ccc)
                clienteActivo = cliente
                If Not IsNothing(cliente.cliente) Then
                    Titulo = String.Format("Cliente {0}", cliente.cliente.Trim)
                End If
                AlCargarFicha() ' Nesto#646: la ficha de cuentas vacía aquí sus avisos de otro CCC (extractoCCC, pedidosCCC)
            End If
        Catch ex As Exception
            errorLectura = ex
        End Try
        ' Nesto#644 (3): si la lectura falla, tampoco se queda la ficha del cliente anterior
        If errorLectura IsNot Nothing AndAlso EsFichaActual(testigo) Then
            VaciarFicha(numCliente, contacto, errorLectura.Message)
        End If
    End Function

    ' ---------------- Nesto#644 (2 y 3): el cliente que está en pantalla ----------------

    Private _testigoFicha As Integer
    Private _mensajeFichaNoLeida As String

    ''' <summary>
    ''' Nesto#644 (2): el testigo de la ficha en pantalla. Cambia al pedir una carga de cliente (ActualizarClienteAsync) y al
    ''' cambiar clienteActivo. Cada carga async lo apunta al empezar (<see cref="TestigoFicha"/>) y, cuando le contesta la API,
    ''' solo asigna si <see cref="EsFichaActual"/>: un resultado que llega cuando la ficha ya es de otro cliente (o empresa o
    ''' contacto) se descarta.
    ''' </summary>
    Protected Function TestigoFicha() As Integer
        Return System.Threading.Volatile.Read(_testigoFicha)
    End Function

    Protected Function EsFichaActual(testigo As Integer) As Boolean
        Return testigo = System.Threading.Volatile.Read(_testigoFicha)
    End Function

    Private Function NuevoTestigoFicha() As Integer
        Return System.Threading.Interlocked.Increment(_testigoFicha)
    End Function

    ''' <summary>
    ''' Nesto#644 (3): el cliente pedido no se ha podido leer: se vacía todo lo que depende del cliente (nombre, cuentas,
    ''' clienteActivo, que vacía lo de cada ficha por <see cref="AlCambiarClienteActivo"/>, y <see cref="AlVaciarFicha"/>) y se
    ''' dice en mensajeError y en la cabecera de la pestaña.
    ''' </summary>
    Private Sub VaciarFicha(numCliente As String, contacto As String, motivo As String)
        nombre = Nothing
        cuentasBanco = New ObservableCollection(Of CCCModel)()
        cuentaActiva = Nothing
        clienteActivo = Nothing
        AlVaciarFicha()
        _mensajeFichaNoLeida = MensajeFichaNoLeida(numCliente, contacto, motivo)
        mensajeError = _mensajeFichaNoLeida
        Titulo = String.Format("Cliente {0}: no se ha podido leer", numCliente?.Trim())
    End Sub

    Friend Shared Function MensajeFichaNoLeida(numCliente As String, contacto As String, motivo As String) As String
        Return String.Format("No se ha podido leer el cliente {0} (contacto {1}){2}. Vuelva a elegirlo.",
                             numCliente?.Trim(), contacto?.Trim(), If(String.IsNullOrWhiteSpace(motivo), String.Empty, ": " & motivo.Trim()))
    End Function

    ''' <summary>
    ''' Carlos 17/11/26: la cuenta que se enseña al cargar la ficha. La del cliente si la tiene y está entre sus cuentas;
    ''' si no, la primera que no está anulada (para poder verla y, con «Usar esta cuenta para los recibos», asignarla:
    ''' el texto «El cliente no tiene cuenta asignada» deja claro que no es la suya); si todas están anuladas, la primera;
    ''' sin cuentas, ninguna. Nunca una cuenta de otro cliente.
    ''' </summary>
    Friend Shared Function CuentaParaMostrar(cuentas As IEnumerable(Of CCCModel), cccCliente As String) As CCCModel
        If IsNothing(cuentas) Then
            Return Nothing
        End If
        If Not String.IsNullOrWhiteSpace(cccCliente) Then
            Dim suya As CCCModel = cuentas.FirstOrDefault(Function(x) x.Número?.Trim() = cccCliente.Trim())
            If Not IsNothing(suya) Then
                Return suya
            End If
        End If
        Return If(cuentas.FirstOrDefault(Function(x) x.Estado <> ESTADO_CCC_ANULADA), cuentas.FirstOrDefault())
    End Function

    ' Estado de la cuenta «Nulo» (anulada) en EstadosCCC: no vale para los recibos.
    Friend Const ESTADO_CCC_ANULADA As Short = -1

    ' Nesto#340 (1C.8, slice 4): POCO de la API en vez de la entidad EF Clientes. Es la misma
    ' ficha que clienteServidor (se cargan de una sola llamada en actualizarCliente).
    Private _clienteActivo As ClienteJson
    Public Property clienteActivo As ClienteJson
        Get
            Return _clienteActivo
        End Get
        Set(value As ClienteJson)
            _clienteActivo = value
            ' Nesto#644 (2): lo que estuviera cargando para el cliente activo anterior ya no vale
            Dim unusedTestigo = NuevoTestigoFicha()
            ' Nesto#646: aquí la comercial apunta el estado cargado y lanza sus cargas; la de cuentas avisa de textoCuentaDelCliente
            AlCambiarClienteActivo(value)

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

    ' Nesto#340 (1C.8, slice 5): estados de CCC desde la API (POCO con Número/Descripción, los
    ' nombres que bindea la vista). Como la carga ahora es async, se notifica también la
    ' descripción del estado por si la ficha se cargó antes que el catálogo.
    Private Property _listaEstadosCCC As ObservableCollection(Of EstadoCCCModel)
    Public Property listaEstadosCCC As ObservableCollection(Of EstadoCCCModel)
        Get
            Return _listaEstadosCCC
        End Get
        Set(value As ObservableCollection(Of EstadoCCCModel))
            _listaEstadosCCC = value
            OnPropertyChanged("listaEstadosCCC")
            OnPropertyChanged(NameOf(descripcionEstadoCCC))
        End Set
    End Property

    Private _listaContactos As ObservableCollection(Of Clientes)
    Public Property listaContactos() As ObservableCollection(Of Clientes)
        Get
            Return _listaContactos
        End Get
        Set(ByVal value As ObservableCollection(Of Clientes))
            _listaContactos = value
            OnPropertyChanged("listaContactos")
            If Not IsNothing(value.FirstOrDefault) Then
                contactoActual = value.FirstOrDefault.Contacto
                OnPropertyChanged("contactoActual")
            End If
        End Set
    End Property

    Private _estaOcupado As Boolean
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(value As Boolean)
            _estaOcupado = value
            OnPropertyChanged("estaOcupado")
        End Set
    End Property
#End Region

#Region "Funciones"
    ' Nesto#646: Protected (antes Private) para que la llame el constructor de cada ficha, en el mismo punto que antes.
    Protected Async Sub cargarDatos()
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        ' Nesto#340 (1C.8, último resto EF): empresas por API; si falla, mensaje y seguimos
        ' (el resto de la ficha no depende del combo).
        Dim errorEmpresas As Exception = Nothing
        Try
            listaEmpresas = New ObservableCollection(Of EmpresaModel)(Await servicio.LeerEmpresas())
        Catch ex As Exception
            listaEmpresas = New ObservableCollection(Of EmpresaModel)
            mensajeError = $"No se han podido cargar las empresas: {ex.Message}"
            ' (VB no permite Await dentro de un Catch, por eso se registra fuera del Try.)
            errorEmpresas = ex
        End Try

        ' Este fallo se quedaba SOLO en el mensaje de pantalla y no llegaba a ELMAH, asi que
        ' llevaba tiempo ocurriendo sin que nadie se enterase. Un error que el usuario ve y
        ' nosotros no es peor que uno que revienta.
        If errorEmpresas IsNot Nothing Then
            Try
                ' En el constructor sin parametros -que es justo el camino que fallaba- no llega
                ' el registro de errores por el constructor: se pide al contenedor.
                Dim registroErrores As IServicioRegistroErrores = servicioErrores
                If registroErrores Is Nothing Then
                    registroErrores = CType(Nesto.Infrastructure.Contracts.Contenedor.Actual.Resolver(GetType(IServicioRegistroErrores)), IServicioRegistroErrores)
                End If
                If registroErrores IsNot Nothing Then
                    Await registroErrores.RegistrarErrorAsync(errorEmpresas, "ClientesViewModel.cargarDatos (empresas)")
                End If
            Catch
                ' El registro de errores nunca debe empeorar lo que ya ha fallado.
            End Try
        End If

        Dim mainViewModel As New MainViewModel
        Dim empresaDefecto As String = Await mainViewModel.leerParametro("1", "EmpresaPorDefecto")
        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        Dim clienteDefecto As String = Await mainViewModel.leerParametro(empresaActual, "UltNumCliente")
        ' Nesto#646: la de cuentas lee aquí la ruta de los mandatos (antes, justo antes de UltNumCliente) y la comercial
        ' el vendedor, los permisos, su equipo y el motor de pagos
        Await CargarDatosAntesDeElegirClienteAsync(mainViewModel, empresaDefecto)

        clienteActual = clienteDefecto
        'contactoActual = "0  " 'esto hay que cambiarlo por el ClientePrincipal

        ' Nesto#340 (1C.8, slice 5): estados de CCC desde la API (la tabla no se toca desde EF).
        Try
            listaEstadosCCC = New ObservableCollection(Of EstadoCCCModel)(Await servicio.LeerEstadosCCC(empresaActual))
        Catch ex As Exception
            mensajeError = ex.Message
        End Try

        Dim rangosFechas As New List(Of String) From {
            "Ventas del Último Año",
            "Ventas de Siempre"
        }

        ' Nesto#646: la comercial carga aquí la deuda vencida y la de cuentas sus listas de secuencias y tipos de mandato
        CargarDatosDespuesDeElegirCliente()

        Titulo = "Clientes"
    End Sub

    ' Nesto#646: lo que cada ficha añade al arranque, al cargar la ficha del cliente y al cambiar de cliente activo.
    ' Van en el mismo punto en el que estaba ese código dentro de cargarDatos, ActualizarClienteAsync y el setter de
    ' clienteActivo.

    ''' <summary>En cargarDatos, tras leer el último cliente (UltNumCliente) y antes de ponerlo en clienteActual.</summary>
    Protected Overridable Function CargarDatosAntesDeElegirClienteAsync(mainViewModel As MainViewModel, empresaDefecto As String) As Task
        Return Task.CompletedTask
    End Function

    ''' <summary>En cargarDatos, tras leer los estados de las cuentas y antes de poner el título.</summary>
    Protected Overridable Sub CargarDatosDespuesDeElegirCliente()
    End Sub

    ''' <summary>En ActualizarClienteAsync, con la ficha ya cargada (clienteActivo y Titulo puestos).</summary>
    Protected Overridable Sub AlCargarFicha()
    End Sub

    ''' <summary>En el setter de clienteActivo, con el valor ya guardado y antes de avisar del cambio.</summary>
    Protected Overridable Sub AlCambiarClienteActivo(value As ClienteJson)
    End Sub

    ''' <summary>Nesto#644 (3): en ActualizarClienteAsync, cuando el cliente no se ha podido leer y se vacía la ficha.</summary>
    Protected Overridable Sub AlVaciarFicha()
    End Sub
#End Region
End Class
