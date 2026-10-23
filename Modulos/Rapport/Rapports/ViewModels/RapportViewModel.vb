Imports CommunityToolkit.Mvvm.Input
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO
Imports CommunityToolkit.Mvvm.ComponentModel
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports CommunityToolkit.Mvvm.Messaging
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports ControlesUsuario.SugerenciasProductos

Public Class RapportViewModel
    Inherits ObservableObject
    Implements IReceptorNavegacionPestanaNueva

    Public Property configuracion As IConfiguracion
    Private Const empresaPorDefecto As String = "1"
    Private ReadOnly servicio As IRapportService
    Private ReadOnly dialogService As IServicioDialogos
    Private ReadOnly _messenger As IMessenger
    Private ReadOnly _navegacion As IServicioNavegacion

    Public Sub New(configuracion As IConfiguracion, servicio As IRapportService, dialogService As IServicioDialogos, messenger As IMessenger,
                   servicioSugerenciasProductos As IServicioSugerenciasProductos, navegacion As IServicioNavegacion)
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.dialogService = dialogService
        _messenger = messenger
        _navegacion = navegacion
        ' Nesto#544: la misma cinta «Puede ofrecer» que la plantilla, sin carrito. «✓ Añadir» (corte 2) lo mete en una
        ' plantilla de ese cliente, que es la que apunta «Anadido».
        _cintaSugerencias = New CintaSugerenciasProductosViewModel(
            AddressOf PeticionSugerenciasProductos,
            leerVendedor:=Function() Me.configuracion?.leerParametro(empresaPorDefecto, Parametros.Claves.Vendedor),
            configuracion:=configuracion,
            anadir:=AddressOf AnadirEnPlantillaAsync,
            apuntarAnadido:=False,
            clavePlegadas:=Parametros.Claves.SugerenciasRapportPlegadas) With {
            .Servicio = servicioSugerenciasProductos
        }
        _cintaSugerencias.CargarPreferenciaPlegadas()

        listaTiposRapports = servicio.CargarListaTipos()

        listaTiposCentros = New List(Of idDescripcionTipoCentro)
        listaTiposCentros.Add(New idDescripcionTipoCentro With {
                              .id = TiposCentro.NoSeSabe,
                              .descripcion = "No se sabe"})
        listaTiposCentros.Add(New idDescripcionTipoCentro With {
                              .id = TiposCentro.SoloEstetica,
                              .descripcion = "Sólo Estética"})
        listaTiposCentros.Add(New idDescripcionTipoCentro With {
                              .id = TiposCentro.SoloPeluqueria,
                              .descripcion = "Sólo Peluquería"})
        listaTiposCentros.Add(New idDescripcionTipoCentro With {
                              .id = TiposCentro.EsteticaYPeluqueria,
                              .descripcion = "Estética y Peluquería"})

        listaEstadosRapport = servicio.CargarListaEstados()

        ' Nesto#469: las opciones de la combo de empleados (el valor es el que guarda la API).
        listaEmpleados = New List(Of idByteDescripcion) From {
            New idByteDescripcion(0, "Sin empleados"),
            New idByteDescripcion(1, "1"),
            New idByteDescripcion(2, "2"),
            New idByteDescripcion(3, "3"),
            New idByteDescripcion(4, "4"),
            New idByteDescripcion(5, "5 o más")
        }

        cmdCrearCita = New RelayCommand(AddressOf OnCrearCita, AddressOf CanCrearCita)
        cmdGuardarCambios = New RelayCommand(Of Object)(AddressOf OnGuardarCambios, AddressOf CanGuardarCambios)

    End Sub

#Region "Propiedades de Nesto"
    Private _clienteCompleto As Object
    Public Property ClienteCompleto As Object
        Get
            Return _clienteCompleto
        End Get
        Set(value As Object)
            SetProperty(_clienteCompleto, value)
            VendedorEstetica = _clienteCompleto?.vendedor?.Trim()
            If _clienteCompleto?.VendedoresGrupoProducto IsNot Nothing AndAlso _clienteCompleto?.VendedoresGrupoProducto?.Count > 0 Then
                VendedorPeluqueria = _clienteCompleto?.VendedoresGrupoProducto(0)?.Vendedor?.Trim()
            Else
                VendedorPeluqueria = String.Empty
            End If
            OnPropertyChanged(NameOf(EstaVisibleTipoCentro))
            PrerrellenarEmpleados()
            OnPropertyChanged(NameOf(EstaVisibleEmpleados))
            ActualizarCintaSugerencias() ' Nesto#544
        End Set
    End Property

    ''' <summary>
    ''' Nesto#469: la combo de empleados solo se enseña cuando la API lo pide (hoy, clientes de la
    ''' Comunidad de Madrid). La regla no está aquí: está en NestoAPI (PoliticaEmpleadosCliente).
    ''' </summary>
    Public ReadOnly Property EstaVisibleEmpleados As Boolean
        Get
            Try
                Return _clienteCompleto IsNot Nothing AndAlso CBool(_clienteCompleto.preguntarEmpleados)
            Catch ex As Exception
                ' Un cliente que venga de un modelo sin la propiedad (código viejo): no se pregunta.
                Return False
            End Try
        End Get
    End Property

    Private _listaEmpleados As List(Of idByteDescripcion)
    Public Property listaEmpleados As List(Of idByteDescripcion)
        Get
            Return _listaEmpleados
        End Get
        Set(value As List(Of idByteDescripcion))
            SetProperty(_listaEmpleados, value)
        End Set
    End Property

    ''' <summary>
    ''' Si la ficha ya tiene el dato, la combo sale rellena y el vendedor solo la cambia si algo ha
    ''' cambiado. Solo se rellena si el rapport no trae ya un valor (un rapport que se está editando
    ''' conserva lo que puso el vendedor).
    ''' </summary>
    Private Sub PrerrellenarEmpleados()
        If rapport Is Nothing OrElse rapport.Empleados.HasValue OrElse _clienteCompleto Is Nothing Then
            Return
        End If
        Try
            Dim deLaFicha As Object = _clienteCompleto.empleados
            If deLaFicha IsNot Nothing Then
                rapport.Empleados = CByte(deLaFicha)
            End If
        Catch ex As Exception
            ' Modelo sin la propiedad: se deja vacía.
        End Try
    End Sub

    Public ReadOnly Property EstaVisibleTipoCentro As Boolean
        Get
            Return rapport IsNot Nothing AndAlso rapport.Id = 0 AndAlso VendedorEstetica = VendedorPeluqueria AndAlso VendedorEstetica = VendedorUsuario
        End Get
    End Property

    Private _fechaAviso As DateTime = DateTime.Now.AddDays(1)
    Public Property fechaAviso As DateTime
        Get
            Return _fechaAviso
        End Get
        Set(value As DateTime)
            SetProperty(_fechaAviso, value)
        End Set
    End Property

    Private _listaEstadosRapport As List(Of idShortDescripcion)
    Public Property listaEstadosRapport As List(Of idShortDescripcion)
        Get
            Return _listaEstadosRapport
        End Get
        Set(value As List(Of idShortDescripcion))
            SetProperty(_listaEstadosRapport, value)
        End Set
    End Property

    Private _listaTiposCentros As List(Of idDescripcionTipoCentro)
    Public Property listaTiposCentros As List(Of idDescripcionTipoCentro)
        Get
            Return _listaTiposCentros
        End Get
        Set(value As List(Of idDescripcionTipoCentro))
            SetProperty(_listaTiposCentros, value)
        End Set
    End Property

    Private _listaTiposRapports As List(Of idDescripcion)
    Public Property listaTiposRapports As List(Of idDescripcion)
        Get
            Return _listaTiposRapports
        End Get
        Set(value As List(Of idDescripcion))
            SetProperty(_listaTiposRapports, value)
        End Set
    End Property

    Private _quitarDeMiListado As Boolean
    Public Property QuitarDeMiListado As Boolean
        Get
            Return _quitarDeMiListado
        End Get
        Set(value As Boolean)
            SetProperty(_quitarDeMiListado, value)
        End Set
    End Property

    Private _rapport As SeguimientoClienteDTO
    Public Property rapport As SeguimientoClienteDTO
        Get
            Return _rapport
        End Get
        Set(value As SeguimientoClienteDTO)
            Dim anterior As SeguimientoClienteDTO = _rapport
            If SetProperty(_rapport, value) Then
                _clienteContactoGuardado = ClaveClienteContacto(value)
                ' Nesto#544: el cliente y el contacto se cambian en el propio rapport (SelectorCliente). Evento débil: el
                ' rapport sigue en la lista cuando se elige otro y no debe retener esta vista.
                If anterior IsNot Nothing Then
                    System.ComponentModel.PropertyChangedEventManager.RemoveHandler(anterior, AddressOf Rapport_PropertyChanged, String.Empty)
                End If
                If value IsNot Nothing Then
                    System.ComponentModel.PropertyChangedEventManager.AddHandler(value, AddressOf Rapport_PropertyChanged, String.Empty)
                End If
            End If
            cmdCrearCita.NotifyCanExecuteChanged()
            cmdGuardarCambios.NotifyCanExecuteChanged()
            PrerrellenarEmpleados()
            OnPropertyChanged(NameOf(EstaVisibleEmpleados))
            ActualizarCintaSugerencias() ' Nesto#544
        End Set
    End Property

    Private _sePuedeCrearRapport As Boolean = True
    Public Property SePuedeCrearRapport As Boolean
        Get
            Return _sePuedeCrearRapport
        End Get
        Set(value As Boolean)
            SetProperty(_sePuedeCrearRapport, value)
        End Set
    End Property

    Private _vendedorEstetica As String
    Public Property VendedorEstetica As String
        Get
            Return _vendedorEstetica
        End Get
        Set(value As String)
            SetProperty(_vendedorEstetica, value)
        End Set
    End Property

    Private _vendedorPeluqueria As String
    Public Property VendedorPeluqueria As String
        Get
            Return _vendedorPeluqueria
        End Get
        Set(value As String)
            SetProperty(_vendedorPeluqueria, value)
        End Set
    End Property


    Private _vendedorUsuario As String
    Public Property VendedorUsuario As String
        Get
            Return _vendedorUsuario
        End Get
        Set(value As String)
            SetProperty(_vendedorUsuario, value)
        End Set
    End Property

#End Region


#Region "Comandos"
    Private _cmdCrearCita As RelayCommand
    Public Property cmdCrearCita As RelayCommand
        Get
            Return _cmdCrearCita
        End Get
        Private Set(value As RelayCommand)
            SetProperty(_cmdCrearCita, value)
        End Set
    End Property
    Private Function CanCrearCita() As Boolean
        Return Not IsNothing(rapport)
    End Function
    Private Async Sub OnCrearCita()
        Dim p As New ParametrosDialogo
        Dim continuar As Boolean = False
        p.Add("message", "Se va a crear la tarea. ¿Desea continuar?")
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ResultadoBoton.OK Then
                                                                  continuar = True
                                                              End If
                                                          End Sub)
        If Not continuar Then
            Return
        End If

        SePuedeCrearRapport = False
        Try
            Await servicio.CrearCita(rapport, fechaAviso)
            SePuedeCrearRapport = True
            dialogService.ShowNotification("Cita", "Se ha creado la cita correctamente")
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            SePuedeCrearRapport = True
        End Try
    End Sub


    Private _cmdGuardarCambios As RelayCommand(Of Object)
    Public Property cmdGuardarCambios As RelayCommand(Of Object)
        Get
            Return _cmdGuardarCambios
        End Get
        Private Set(value As RelayCommand(Of Object))
            SetProperty(_cmdGuardarCambios, value)
        End Set
    End Property
    ' NestoAPI#294: guard de reentrada. Con el guardado en vuelo (servidor lento) el botón seguía
    ' activo y cada clic extra disparaba otro POST: dos peticiones concurrentes pasaban ambas el
    ' check del servidor y se creaban rapports duplicados. Mismo patrón que SePuedeCrearRapport.
    Private _guardandoRapport As Boolean = False
    Private Function CanGuardarCambios(arg As Object) As Boolean
        Return Not _guardandoRapport AndAlso Not IsNothing(rapport) AndAlso (rapport.Usuario.ToLower = configuracion.usuario.ToLower OrElse configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION))
    End Function
    ''' <summary>
    ''' Nesto#469 (NestoAPI#464, Carlos 16/09/26): desde el deploy nadie rellenaba los empleados (368
    ''' rapports de Madrid, ninguno con el dato): la combo está, pero el vendedor no se fija. Si se le
    ''' pregunta (EstaVisibleEmpleados) y la deja vacía, se le dice y tiene que confirmar que no lo
    ''' rellena porque no lo sabe; si no confirma, no se guarda y vuelve al formulario. Con valor
    ''' (incluido «Sin empleados» = 0) o sin combo, no se pregunta. Devuelve True si se puede guardar.
    ''' </summary>
    Public Function ConfirmarEmpleadosSinRellenar() As Boolean
        If rapport Is Nothing OrElse Not EstaVisibleEmpleados OrElse rapport.Empleados.HasValue Then
            Return True
        End If
        Dim continuar As Boolean = False
        Dim p As New ParametrosDialogo From {
            {"message", "No has indicado los empleados del centro." & vbCrLf & "¿Confirmas que no lo rellenas porque no lo sabes?"}
        }
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ResultadoBoton.OK Then
                                                                  continuar = True
                                                              End If
                                                          End Sub)
        Return continuar
    End Function

    ' Nesto#548 (sugerencia 736): cliente|contacto con que llegó el rapport ya guardado (Id <> 0). Nothing en uno nuevo.
    Private _clienteContactoGuardado As String

    Private Shared Function ClaveClienteContacto(r As SeguimientoClienteDTO) As String
        If r Is Nothing OrElse r.Id = 0 Then
            Return Nothing
        End If
        Return $"{r.Cliente?.Trim()}|{r.Contacto?.Trim()}"
    End Function

    ''' <summary>
    ''' Nesto#548 (sugerencia 736): muchos vendedores registran el seguimiento en un contacto equivocado del cliente. Si el
    ''' contacto elegido está en estado 7 (Constantes.Clientes.ESTADO_NO_VISITABLE), se pregunta antes de guardar, con «No»
    ''' por defecto. Solo en un rapport nuevo o cuando se le cambia el cliente o el contacto; los ya guardados no se
    ''' preguntan otra vez. El estado sale de ClienteCompleto (el contacto que tiene el selector), siempre que sea el del
    ''' rapport. Devuelve True si se puede guardar.
    ''' </summary>
    Public Function ConfirmarContactoNoVisitable() As Boolean
        If rapport Is Nothing Then
            Return True
        End If
        If rapport.Id <> 0 AndAlso ClaveClienteContacto(rapport) = _clienteContactoGuardado Then
            Return True
        End If
        If EstadoContactoDelRapport() <> Constantes.Clientes.ESTADO_NO_VISITABLE Then
            Return True
        End If
        Return dialogService.ShowConfirmationAnswer("Contacto en estado 7",
            "Este contacto está en estado 7. ¿Seguro que quiere registrar aquí el seguimiento?", porDefectoNo:=True)
    End Function

    ''' <summary>El estado del contacto del rapport según ClienteCompleto; Nothing si no es ese contacto o no se sabe.</summary>
    Private Function EstadoContactoDelRapport() As Integer?
        If _clienteCompleto Is Nothing Then
            Return Nothing
        End If
        Try
            Dim cliente As String = CStr(_clienteCompleto.cliente)?.Trim()
            Dim contacto As String = CStr(_clienteCompleto.contacto)?.Trim()
            If Not String.Equals(cliente, rapport.Cliente?.Trim(), StringComparison.OrdinalIgnoreCase) OrElse
                Not String.Equals(contacto, rapport.Contacto?.Trim(), StringComparison.OrdinalIgnoreCase) Then
                Return Nothing
            End If
            Return CInt(_clienteCompleto.estado)
        Catch ex As Exception
            Return Nothing ' un cliente de un modelo sin esas propiedades: no se pregunta
        End Try
    End Function

    Private Async Sub OnGuardarCambios(arg As Object)
        If _guardandoRapport Then
            Return
        End If
        If Not ConfirmarContactoNoVisitable() Then
            Return
        End If
        If Not ConfirmarEmpleadosSinRellenar() Then
            Return
        End If
        If Not EstaVisibleTipoCentro Then
            rapport.TipoCentro = TiposCentro.NoSeSabe
        End If
        _guardandoRapport = True
        cmdGuardarCambios.NotifyCanExecuteChanged()
        Dim texto As String
        Try
            texto = Await servicio.crearRapport(rapport)
            If rapport.Estado = Constantes.Rapports.Estados.GESTION_ADMINISTRATIVA Then
                texto += vbCrLf + Await servicio.CrearTareaPlanner(rapport)
            End If
            If QuitarDeMiListado Then
                Await servicio.QuitarDeMiListado(rapport, VendedorEstetica, VendedorPeluqueria)
                QuitarDeMiListado = False
            End If
            _clienteContactoGuardado = ClaveClienteContacto(rapport)
            _messenger.Send(New RapportGuardadoMensaje(0))
            dialogService.ShowNotification("Rapport", texto)
        Catch ex As Exception
            ' Nesto#206: la lista ya lo tenía como fila; que sepa que no se ha guardado.
            _messenger.Send(New RapportNoGuardadoMensaje(rapport))
            dialogService.ShowError(ex.Message)
        Finally
            _guardandoRapport = False
            cmdGuardarCambios.NotifyCanExecuteChanged()
        End Try
    End Sub
#End Region


    ' Nesto#490 (4C.4): cada navegación abre una vista de rapport nueva (antes IsNavigationTarget = False). Misma
    ' clave que con Prism; GetValue(Of Object) devuelve Nothing si no viene, como su indexador.
    Public Async Sub AlLlegar(parametros As ParametrosNavegacion) Implements IReceptorNavegacion.AlLlegar
        rapport = parametros?.GetValue(Of Object)("rapportParameter")
        If VendedorUsuario Is Nothing Then
            VendedorUsuario = Await configuracion.leerParametro("1", "Vendedor")
        End If
    End Sub


#Region "Nesto#544: productos que puede ofrecer"

    ' La misma cinta «Puede ofrecer» que la plantilla (CintaSugerenciasProductosViewModel y CintaSugerenciasProductosView, en
    ' ControlesUsuario): «✗ No le interesa» y «🚫 Quitar» funcionan igual, con la sesión del cliente y Aplicacion = Nesto. Aquí
    ' no hay carrito. Solo en un rapport nuevo y con cliente. Si la API falla o no hay nada que ofrecer, la cinta no se ve y
    ' no hay errores. «✓ Añadir» (corte 2) lo mete en una plantilla del cliente: la abierta si la hay, si no una nueva.

    Private ReadOnly _cintaSugerencias As CintaSugerenciasProductosViewModel

    ''' <summary>La cinta «Puede ofrecer» (su DataContext en la vista).</summary>
    Public ReadOnly Property CintaSugerencias As CintaSugerenciasProductosViewModel
        Get
            Return _cintaSugerencias
        End Get
    End Property

    ''' <summary>Lo que se manda a Calcular: el cliente y el contacto del rapport, sin carrito. Nothing si no toca preguntar.</summary>
    Public Function PeticionSugerenciasProductos() As CalcularSugerenciasProductosDTO
        If rapport Is Nothing OrElse rapport.Id <> 0 OrElse String.IsNullOrWhiteSpace(rapport.Cliente) Then
            Return Nothing
        End If
        Return New CalcularSugerenciasProductosDTO With {
            .Empresa = If(String.IsNullOrWhiteSpace(rapport.Empresa), empresaPorDefecto, rapport.Empresa).Trim(),
            .Cliente = rapport.Cliente.Trim(),
            .Contacto = rapport.Contacto?.Trim(),
            .Vendedor = VendedorUsuario?.Trim()
        }
    End Function

    ''' <summary>
    ''' Con otro cliente, sesión nueva y cinta vacía; con el mismo, nada se reinicia. En los dos casos se pregunta tras el
    ''' respiro (si no ha cambiado nada de lo que se manda, no sale ninguna petición).
    ''' </summary>
    Private Sub ActualizarCintaSugerencias()
        If _cintaSugerencias Is Nothing Then
            Return ' durante la construcción
        End If
        Dim peticion As CalcularSugerenciasProductosDTO = PeticionSugerenciasProductos()
        _cintaSugerencias.ReiniciarSiCambiaElCliente(If(peticion Is Nothing, Nothing, $"{peticion.Empresa}|{peticion.Cliente}"))
        _cintaSugerencias.Programar()
    End Sub

    ''' <summary>
    ''' «✓ Añadir» (corte 2): pregunta por el mensajero si hay una plantilla abierta de este cliente; si contesta, ella mete
    ''' el producto y se trae al frente (True si ha entrado). Si no hay ninguna, abre una plantilla nueva con el cliente, el
    ''' contacto, el producto, la sugerencia y la sesión (True: la ficha sale de aquí; lo demás lo cuenta la plantilla).
    ''' En los dos casos «Anadido» lo apunta la plantilla, contra esta sugerencia y esta sesión.
    ''' </summary>
    Friend Async Function AnadirEnPlantillaAsync(sugerencia As SugerenciaProductoDTO) As Task(Of Boolean)
        Dim peticion As CalcularSugerenciasProductosDTO = PeticionSugerenciasProductos()
        If sugerencia Is Nothing OrElse peticion Is Nothing OrElse String.IsNullOrWhiteSpace(sugerencia.Producto) Then
            Return False
        End If
        Dim sugerido As New ProductoSugeridoParaPlantilla With {
            .Empresa = peticion.Empresa,
            .Cliente = peticion.Cliente,
            .Contacto = peticion.Contacto,
            .Producto = sugerencia.Producto.Trim(),
            .IdSugerencia = sugerencia.IdSugerencia,
            .IdSesion = _cintaSugerencias.IdSesion
        }
        Dim mensaje As New AnadirProductoSugeridoMensaje(sugerido)
        _messenger?.Send(mensaje)
        If mensaje.HasReceivedResponse Then
            Return Await mensaje.Response.ConfigureAwait(True)
        End If
        If _navegacion Is Nothing Then
            Return False
        End If
        Dim parametros As New ParametrosNavegacion From {
            {ProductoSugeridoParaPlantilla.PARAMETRO_EMPRESA, sugerido.Empresa},
            {ProductoSugeridoParaPlantilla.PARAMETRO_CLIENTE, sugerido.Cliente},
            {ProductoSugeridoParaPlantilla.PARAMETRO_CONTACTO, sugerido.Contacto},
            {ProductoSugeridoParaPlantilla.PARAMETRO_PRODUCTO, sugerido.Producto},
            {ProductoSugeridoParaPlantilla.PARAMETRO_ID_SUGERENCIA, sugerido.IdSugerencia}
        }
        If sugerido.IdSesion.HasValue Then
            parametros.Add(ProductoSugeridoParaPlantilla.PARAMETRO_ID_SESION, sugerido.IdSesion.Value)
        End If
        _navegacion.RequestNavigate("MainRegion", "PlantillaVentaView", parametros)
        Return True
    End Function

    Private Sub Rapport_PropertyChanged(sender As Object, e As System.ComponentModel.PropertyChangedEventArgs)
        If e Is Nothing OrElse sender IsNot _rapport Then
            Return ' un rapport que ya no es el de la vista (o la vista se está cerrando)
        End If
        If e.PropertyName = NameOf(SeguimientoClienteDTO.Cliente) OrElse e.PropertyName = NameOf(SeguimientoClienteDTO.Contacto) OrElse
            e.PropertyName = NameOf(SeguimientoClienteDTO.Empresa) Then
            ActualizarCintaSugerencias()
        End If
    End Sub

#End Region

    Public Structure idByteDescripcion
        Public Sub New(_id As Byte, _descripcion As String)
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As Byte
        Property descripcion As String
    End Structure

    Public Structure idDescripcionTipoCentro
        Public Sub New(
       ByVal _id As TiposCentro,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As TiposCentro
        Property descripcion As String
    End Structure

End Class
