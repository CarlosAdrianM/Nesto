Imports System.Collections.ObjectModel
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Models
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

''' <summary>
''' Nesto#579 (NestoAPI#757, corte 1 de NestoAPI#755): el panel «Nota de entrega» del detalle (tipo Entrega / Recogida,
''' bultos y factura original), la casilla «Nota de entrega» en un pedido nuevo y el botón «Nueva nota de recogida».
''' Las notas de ENTREGA pasan por picking y packing como un pedido (los bultos y el reembolso salen del packing), así que
''' Bultos y Factura original solo se ven y se usan en las de RECOGIDA (Carlos, 30/10/26).
''' </summary>
Partial Public Class DetallePedidoViewModel

    ''' <summary>Los tipos del combo del panel.</summary>
    Public Shared ReadOnly Property TiposNota As IReadOnlyList(Of String) = TiposNotaEntrega.Todos

    ''' <summary>Quién puede crear notas de entrega y de recogida (los mismos grupos que deja la API).</summary>
    Public Shared Function PuedeCrearNotas(configuracion As IConfiguracion) As Boolean
        If configuracion Is Nothing Then
            Return False
        End If
        Return configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) OrElse
            configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) OrElse
            configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
            configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA)
    End Function

    Private _esGrupoQuePuedeCrearNotas As Boolean
    Public Property EsGrupoQuePuedeCrearNotas As Boolean
        Get
            Return _esGrupoQuePuedeCrearNotas
        End Get
        Friend Set(value As Boolean)
            If SetProperty(_esGrupoQuePuedeCrearNotas, value) Then
                NotificarCambioNotaEntrega()
            End If
        End Set
    End Property

    Public Property NuevaNotaRecogidaCommand As AsyncRelayCommand
    Public Property AbrirSelectorFacturaOriginalCommand As RelayCommand
    Public Property CancelarSelectorFacturaOriginalCommand As RelayCommand
    Public Property VincularFacturaOriginalCommand As RelayCommand
    Public Property QuitarFacturaOriginalCommand As RelayCommand
    Public Property ImprimirNotaCommand As AsyncRelayCommand
    Public Property VerNotaCommand As AsyncRelayCommand
    Public Property EtiquetasRecogidaCommand As AsyncRelayCommand

    Private Sub InicializarNotaEntrega()
        EsGrupoQuePuedeCrearNotas = PuedeCrearNotas(configuracion)
        NuevaNotaRecogidaCommand = New AsyncRelayCommand(AddressOf NuevaNotaRecogidaAsync, Function() PuedeCrearNotaRecogida)
        AbrirSelectorFacturaOriginalCommand = New RelayCommand(Sub() MostrarSelectorFacturaOriginal = True, Function() EsNotaRecogida)
        CancelarSelectorFacturaOriginalCommand = New RelayCommand(Sub() MostrarSelectorFacturaOriginal = False)
        VincularFacturaOriginalCommand = New RelayCommand(AddressOf VincularFacturaOriginal, Function() FacturaOriginalEnLista IsNot Nothing)
        QuitarFacturaOriginalCommand = New RelayCommand(AddressOf QuitarFacturaOriginal, Function() Not String.IsNullOrWhiteSpace(FacturaOriginalNota))
        ImprimirNotaCommand = New AsyncRelayCommand(AddressOf ImprimirNotaAsync, Function() PuedeImprimirNota)
        VerNotaCommand = New AsyncRelayCommand(AddressOf VerNotaAsync, Function() PuedeImprimirNota)
        EtiquetasRecogidaCommand = New AsyncRelayCommand(AddressOf ImprimirEtiquetasRecogidaAsync, Function() PuedeImprimirEtiquetasRecogida)
    End Sub

#Region "Casilla «Nota de entrega»"
    ''' <summary>La casilla solo se puede marcar en un pedido nuevo y para Almacén, Administración, Dirección e Informática.</summary>
    Public ReadOnly Property PuedeMarcarNotaEntrega As Boolean
        Get
            Return EstaCreandoPedido AndAlso EsGrupoQuePuedeCrearNotas
        End Get
    End Property

    ''' <summary>La casilla «Nota de entrega»: al marcarla nace como Entrega (bultos 1) y sale el panel.</summary>
    Public Property NotaEntregaMarcada As Boolean
        Get
            Return EsNotaEntrega
        End Get
        Set(value As Boolean)
            If pedido Is Nothing OrElse value = pedido.notaEntrega OrElse Not PuedeMarcarNotaEntrega Then
                OnPropertyChanged(NameOf(NotaEntregaMarcada))
                Return
            End If
            pedido.notaEntrega = value
            _tipoNota = TiposNotaEntrega.ENTREGA
            _bultosNota = 1
            _facturaOriginalNota = Nothing
            CopiarCabeceraNotaAlPedido()
            NotificarCambioNotaEntrega()
        End Set
    End Property
#End Region

#Region "Panel «Nota de entrega»"
    Public ReadOnly Property MostrarPanelNotaEntrega As Boolean
        Get
            Return EsNotaEntrega
        End Get
    End Property

    Private _tipoNota As String = TiposNotaEntrega.ENTREGA
    Public Property TipoNota As String
        Get
            Return _tipoNota
        End Get
        Set(value As String)
            Dim nuevo As String = TiposNotaEntrega.Normalizar(value)
            If nuevo = _tipoNota Then
                Return
            End If
            Dim anterior As CabeceraNota = CabeceraActual()
            _tipoNota = nuevo
            If nuevo = TiposNotaEntrega.RECOGIDA AndAlso _bultosNota < 1 Then
                _bultosNota = 1 ' una recogida lleva al menos un bulto
            End If
            NotificarCambioCabeceraNota()
            GuardarCabeceraNota(anterior)
        End Set
    End Property

    Private _bultosNota As Integer = 1
    Public Property BultosNota As Integer
        Get
            Return _bultosNota
        End Get
        Set(value As Integer)
            If value = _bultosNota Then
                Return
            End If
            Dim anterior As CabeceraNota = CabeceraActual()
            _bultosNota = value
            NotificarCambioCabeceraNota()
            GuardarCabeceraNota(anterior)
        End Set
    End Property

    Private _facturaOriginalNota As String
    ''' <summary>La factura original que se devuelve (solo recogidas). Se elige con el selector de facturas del cliente.</summary>
    Public ReadOnly Property FacturaOriginalNota As String
        Get
            Return _facturaOriginalNota
        End Get
    End Property

    Public ReadOnly Property TextoFacturaOriginal As String
        Get
            Return If(String.IsNullOrWhiteSpace(_facturaOriginalNota), "(sin vincular)", _facturaOriginalNota)
        End Get
    End Property

    ''' <summary>Bultos y factura original solo cuentan en las recogidas: las entregas pasan por el packing.</summary>
    Public ReadOnly Property EsNotaRecogida As Boolean
        Get
            Return EsNotaEntrega AndAlso _tipoNota = TiposNotaEntrega.RECOGIDA
        End Get
    End Property

    Public ReadOnly Property TextoCabeceraNota As String
        Get
            Dim texto As String = If(EsNotaRecogida, "Nota de recogida", "Nota de entrega")
            If pedido IsNot Nothing AndAlso pedido.numero <> 0 Then
                texto &= If(EsNotaRecogida, " NR", " NE") & pedido.numero.ToString()
            End If
            Return texto
        End Get
    End Property

    Private _mostrarSelectorFacturaOriginal As Boolean
    Public Property MostrarSelectorFacturaOriginal As Boolean
        Get
            Return _mostrarSelectorFacturaOriginal
        End Get
        Set(value As Boolean)
            If SetProperty(_mostrarSelectorFacturaOriginal, value) AndAlso Not value Then
                FacturaOriginalEnLista = Nothing
            End If
        End Set
    End Property

    Private _facturaOriginalEnLista As FacturaClienteDTO
    ''' <summary>La factura marcada en el selector (aún sin vincular).</summary>
    Public Property FacturaOriginalEnLista As FacturaClienteDTO
        Get
            Return _facturaOriginalEnLista
        End Get
        Set(value As FacturaClienteDTO)
            If SetProperty(_facturaOriginalEnLista, value) Then
                VincularFacturaOriginalCommand?.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Friend Sub VincularFacturaOriginal()
        Dim documento As String = FacturaOriginalEnLista?.Documento?.Trim()
        If String.IsNullOrWhiteSpace(documento) Then
            Return
        End If
        CambiarFacturaOriginal(documento)
        MostrarSelectorFacturaOriginal = False
    End Sub

    Friend Sub QuitarFacturaOriginal()
        CambiarFacturaOriginal(Nothing)
    End Sub

    Private Sub CambiarFacturaOriginal(factura As String)
        If String.Equals(If(factura, String.Empty), If(_facturaOriginalNota, String.Empty), StringComparison.OrdinalIgnoreCase) Then
            Return
        End If
        Dim anterior As CabeceraNota = CabeceraActual()
        _facturaOriginalNota = factura
        NotificarCambioCabeceraNota()
        GuardarCabeceraNota(anterior)
    End Sub
#End Region

#Region "Guardar al momento"
    Private Structure CabeceraNota
        Public Tipo As String
        Public Bultos As Integer
        Public FacturaOriginal As String
    End Structure

    Private Function CabeceraActual() As CabeceraNota
        Return New CabeceraNota With {.Tipo = _tipoNota, .Bultos = _bultosNota, .FacturaOriginal = _facturaOriginalNota}
    End Function

    ''' <summary>Las llamadas al PUT de la cabecera (para los tests).</summary>
    Friend Property CambioCabeceraNotaEnCurso As Task = Task.CompletedTask

    ''' <summary>
    ''' En una nota grabada, cada cambio va al momento por PUT api/NotasEntrega/{empresa}/{pedido}/Cabecera (como la casilla
    ''' «Servir lo pendiente» de la nota). En un pedido nuevo solo se apunta en el pedido, que lo lleva el POST.
    ''' </summary>
    Private Sub GuardarCabeceraNota(anterior As CabeceraNota)
        If pedido Is Nothing OrElse Not EsNotaEntrega Then
            Return
        End If
        If pedido.numero = 0 Then
            CopiarCabeceraNotaAlPedido()
            Return
        End If
        CambioCabeceraNotaEnCurso = GuardarCabeceraNotaAsync(anterior)
    End Sub

    Private Async Function GuardarCabeceraNotaAsync(anterior As CabeceraNota) As Task
        Dim empresa As String = pedido.empresa
        Dim numero As Integer = pedido.numero
        Try
            Dim respuesta As NotaEntregaCabeceraDTO = Await servicio.CambiarCabeceraNotaEntrega(empresa, numero, _tipoNota, _bultosNota, _facturaOriginalNota).ConfigureAwait(True)
            If pedido Is Nothing OrElse pedido.numero <> numero Then
                Return ' el usuario ya está en otro pedido
            End If
            If respuesta IsNot Nothing Then
                _tipoNota = TiposNotaEntrega.Normalizar(respuesta.Tipo)
                _bultosNota = respuesta.Bultos
                _facturaOriginalNota = If(String.IsNullOrWhiteSpace(respuesta.FacturaOriginal), Nothing, respuesta.FacturaOriginal.Trim())
            End If
            CopiarCabeceraNotaAlPedido()
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            If pedido Is Nothing OrElse pedido.numero <> numero Then
                Return
            End If
            _tipoNota = anterior.Tipo
            _bultosNota = anterior.Bultos
            _facturaOriginalNota = anterior.FacturaOriginal
        End Try
        NotificarCambioCabeceraNota()
    End Function

    ''' <summary>Lo que viaja en el POST de la nota: tipo siempre; bultos solo en la recogida (en la entrega los da el packing).</summary>
    Private Sub CopiarCabeceraNotaAlPedido()
        If pedido?.Model Is Nothing Then
            Return
        End If
        If pedido.notaEntrega Then
            pedido.Model.tipoNotaEntrega = _tipoNota
            pedido.Model.bultosNota = If(_tipoNota = TiposNotaEntrega.RECOGIDA, _bultosNota, CType(Nothing, Integer?))
            pedido.Model.facturaOriginalNota = If(_tipoNota = TiposNotaEntrega.RECOGIDA, _facturaOriginalNota, Nothing)
        Else
            pedido.Model.tipoNotaEntrega = Nothing
            pedido.Model.bultosNota = Nothing
            pedido.Model.facturaOriginalNota = Nothing
        End If
    End Sub

    ''' <summary>
    ''' El POST lleva tipo y bultos, pero la factura original va por el PUT de la cabecera: si se eligió en la nota nueva,
    ''' se vincula nada más crearla. Si falla, la nota ya está creada y se dice.
    ''' </summary>
    Friend Async Function VincularFacturaOriginalTrasCrearAsync() As Task
        If pedido Is Nothing OrElse pedido.numero = 0 OrElse Not EsNotaRecogida OrElse String.IsNullOrWhiteSpace(_facturaOriginalNota) Then
            Return
        End If
        Try
            Dim unused = Await servicio.CambiarCabeceraNotaEntrega(pedido.empresa, pedido.numero, _tipoNota, _bultosNota, _facturaOriginalNota).ConfigureAwait(True)
        Catch ex As Exception
            dialogService.ShowError($"La nota se ha creado, pero no se ha podido vincular la factura original: {ex.Message}")
            _facturaOriginalNota = Nothing
            CopiarCabeceraNotaAlPedido()
            NotificarCambioCabeceraNota()
        End Try
    End Function

    ''' <summary>Al cargar un pedido: lo que trae el GET (sin fila en BD = Entrega, 1 bulto).</summary>
    Private Sub InicializarCabeceraNota()
        Dim modelo As PedidoVentaDTO = pedido?.Model
        _tipoNota = TiposNotaEntrega.Normalizar(modelo?.tipoNotaEntrega)
        _bultosNota = If(modelo?.bultosNota, 1)
        _facturaOriginalNota = If(String.IsNullOrWhiteSpace(modelo?.facturaOriginalNota), Nothing, modelo.facturaOriginalNota.Trim())
        CambioCabeceraNotaEnCurso = Task.CompletedTask
        _mostrarSelectorFacturaOriginal = False
        _facturaOriginalEnLista = Nothing
        OnPropertyChanged(NameOf(MostrarSelectorFacturaOriginal))
        OnPropertyChanged(NameOf(FacturaOriginalEnLista))
        NotificarCambioNotaEntrega()
    End Sub

    Private Sub NotificarCambioCabeceraNota()
        OnPropertyChanged(NameOf(TipoNota))
        OnPropertyChanged(NameOf(BultosNota))
        OnPropertyChanged(NameOf(FacturaOriginalNota))
        OnPropertyChanged(NameOf(TextoFacturaOriginal))
        OnPropertyChanged(NameOf(EsNotaRecogida))
        OnPropertyChanged(NameOf(TextoCabeceraNota))
        AbrirSelectorFacturaOriginalCommand?.NotifyCanExecuteChanged()
        QuitarFacturaOriginalCommand?.NotifyCanExecuteChanged()
        OnPropertyChanged(NameOf(PuedeImprimirNota))
        OnPropertyChanged(NameOf(PuedeImprimirEtiquetasRecogida))
        ImprimirNotaCommand?.NotifyCanExecuteChanged()
        VerNotaCommand?.NotifyCanExecuteChanged()
        EtiquetasRecogidaCommand?.NotifyCanExecuteChanged()
        If Not EsNotaRecogida AndAlso _mostrarSelectorFacturaOriginal Then
            MostrarSelectorFacturaOriginal = False
        End If
    End Sub

    ''' <summary>Todo lo que depende de si el pedido es nota, es nuevo o de quién lo mira.</summary>
    Friend Sub NotificarCambioNotaEntrega()
        OnPropertyChanged(NameOf(NotaEntregaMarcada))
        OnPropertyChanged(NameOf(PuedeMarcarNotaEntrega))
        OnPropertyChanged(NameOf(MostrarPanelNotaEntrega))
        OnPropertyChanged(NameOf(PuedeElegirModoFacturacion))
        NotificarCambioCabeceraNota()
        NuevaNotaRecogidaCommand?.NotifyCanExecuteChanged()
    End Sub
#End Region

#Region "Imprimir nota y etiquetas de recogida (Nesto#580)"
    Private _servicioImpresion As IServicioImpresionDocumentos
    ''' <summary>Nesto#580: inyectable desde los tests (en producción, la impresora por defecto con PdfiumViewer).</summary>
    Friend Property ServicioImpresion As IServicioImpresionDocumentos
        Get
            If _servicioImpresion Is Nothing Then
                _servicioImpresion = New ServicioImpresionDocumentos()
            End If
            Return _servicioImpresion
        End Get
        Set(value As IServicioImpresionDocumentos)
            _servicioImpresion = value
        End Set
    End Property

    ''' <summary>
    ''' «Imprimir nota» / «Ver nota»: cualquier nota grabada, de entrega o de recogida, también las de Nesto viejo y antes de
    ''' facturar la ruta (el PDF con el código de barras NE/NR lo hace la API y no crea la parada).
    ''' </summary>
    Public ReadOnly Property PuedeImprimirNota As Boolean
        Get
            Return EsNotaEntrega AndAlso pedido IsNot Nothing AndAlso pedido.numero <> 0
        End Get
    End Property

    ''' <summary>
    ''' «Etiquetas de recogida»: solo las notas de RECOGIDA grabadas. Las de entrega pasan por el packing y llevan su
    ''' etiqueta normal (Carlos, 30/10/26).
    ''' </summary>
    Public ReadOnly Property PuedeImprimirEtiquetasRecogida As Boolean
        Get
            Return EsNotaRecogida AndAlso pedido IsNot Nothing AndAlso pedido.numero <> 0
        End Get
    End Property

    Private ReadOnly Property NombreNota As String
        Get
            Return If(EsNotaRecogida, "nota de recogida", "nota de entrega")
        End Get
    End Property

    ''' <summary>Imprime 2 copias en la bandeja Middle (como las notas de Facturar rutas).</summary>
    Friend Async Function ImprimirNotaAsync() As Task
        If Not PuedeImprimirNota Then
            Return
        End If
        Dim empresa As String = pedido.empresa
        Dim numero As Integer = pedido.numero
        If Not dialogService.ShowConfirmationAnswer("Imprimir nota", $"¿Desea imprimir la {NombreNota} {numero} directamente en la impresora (2 copias)?") Then
            Return
        End If
        Try
            Dim bytesPdf As Byte() = Await servicio.CargarDocumentoNotaEntrega(empresa, numero).ConfigureAwait(True)
            If bytesPdf Is Nothing OrElse bytesPdf.Length = 0 Then
                dialogService.ShowError($"No se ha podido obtener el documento de la nota {numero}.")
                Return
            End If
            Dim nota As New NotaEntregaCreadaDTO With {
                .Empresa = empresa,
                .NumeroPedido = numero,
                .Tipo = _tipoNota,
                .DatosImpresion = New DocumentoParaImprimir With {
                    .BytesPDF = bytesPdf,
                    .NumeroCopias = 2,
                    .TipoBandeja = TipoBandejaImpresion.Middle
                }
            }
            Dim resultado As ResultadoImpresion = Await ServicioImpresion.ImprimirNotasEntrega({nota}).ConfigureAwait(True)
            If resultado IsNot Nothing AndAlso resultado.TieneErrores Then
                dialogService.ShowError($"No se ha podido imprimir la nota: {resultado.Errores.First().MensajeError}")
            Else
                dialogService.ShowNotification($"Nota {numero} enviada a la impresora")
            End If
        Catch ex As Exception
            Dim mensajeError As String = If(ex.InnerException Is Nothing, ex.Message, ex.InnerException.Message)
            dialogService.ShowError($"No se ha podido imprimir la nota: {mensajeError}")
        End Try
    End Function

    ''' <summary>Abre el PDF de la nota en pantalla (como los demás PDF del detalle).</summary>
    Friend Async Function VerNotaAsync() As Task
        If Not PuedeImprimirNota Then
            Return
        End If
        Try
            Dim ruta As String = Await servicio.DescargarDocumentoNotaEntrega(pedido.empresa, pedido.numero, pedido.cliente).ConfigureAwait(True)
            Dim unused = Process.Start(New ProcessStartInfo(ruta) With {.UseShellExecute = True})
        Catch ex As Exception
            Dim mensajeError As String = If(ex.InnerException Is Nothing, ex.Message, ex.InnerException.Message)
            dialogService.ShowError($"No se ha podido abrir la nota: {mensajeError}")
        End Try
    End Function

    ''' <summary>
    ''' Sin bultos guardados (vacío o 0) hay que preguntarlos: la API imprime una etiqueta NR{Pedido}-{N} por bulto.
    ''' </summary>
    Friend ReadOnly Property FaltanBultosNotaRecogida As Boolean
        Get
            Dim bultosGuardados As Integer? = pedido?.Model?.bultosNota
            Return Not bultosGuardados.HasValue OrElse bultosGuardados.Value < 1 OrElse _bultosNota < 1
        End Get
    End Property

    ''' <summary>
    ''' «Etiquetas de recogida»: si la nota no tiene bultos, los pregunta y los guarda por el PUT de la cabecera; luego la API
    ''' las imprime por la Zebra (POST api/NotasEntrega/{empresa}/{pedido}/EtiquetasRecogida). En Nesto no se genera ZPL.
    ''' </summary>
    Friend Async Function ImprimirEtiquetasRecogidaAsync() As Task
        If Not PuedeImprimirEtiquetasRecogida Then
            Return
        End If
        Await CambioCabeceraNotaEnCurso.ConfigureAwait(True)
        If Not PuedeImprimirEtiquetasRecogida Then
            Return
        End If
        Dim empresa As String = pedido.empresa
        Dim numero As Integer = pedido.numero

        If FaltanBultosNotaRecogida Then
            Dim texto As String = dialogService.GetText("Etiquetas de recogida", "¿Cuántos bultos se recogen? Se imprime una etiqueta por bulto.", Math.Max(_bultosNota, 1).ToString())
            If texto Is Nothing Then
                Return
            End If
            Dim bultos As Integer
            If Not Integer.TryParse(texto.Trim(), bultos) OrElse bultos < 1 Then
                dialogService.ShowError("Escriba un número de bultos mayor que cero.")
                Return
            End If
            Try
                Dim respuesta As NotaEntregaCabeceraDTO = Await servicio.CambiarCabeceraNotaEntrega(empresa, numero, TiposNotaEntrega.RECOGIDA, bultos, _facturaOriginalNota).ConfigureAwait(True)
                If pedido Is Nothing OrElse pedido.numero <> numero Then
                    Return ' el usuario ya está en otro pedido
                End If
                _bultosNota = If(respuesta IsNot Nothing AndAlso respuesta.Bultos > 0, respuesta.Bultos, bultos)
                CopiarCabeceraNotaAlPedido()
                NotificarCambioCabeceraNota()
            Catch ex As Exception
                dialogService.ShowError($"No se han podido guardar los bultos de la nota: {ex.Message}")
                Return
            End Try
        End If

        Try
            Dim resultado As EtiquetasRecogidaRespuestaDTO = Await servicio.ImprimirEtiquetasRecogida(empresa, numero, Nothing).ConfigureAwait(True)
            Dim impresas As Integer = If(resultado?.Impresas, 0)
            dialogService.ShowNotification($"Impresas {impresas} etiqueta{If(impresas = 1, "", "s")} por la Zebra")
        Catch ex As Exception
            dialogService.ShowError($"No se han podido imprimir las etiquetas de recogida: {ex.Message}")
        End Try
    End Function
#End Region

#Region "Nueva nota de recogida"
    Public ReadOnly Property PuedeCrearNotaRecogida As Boolean
        Get
            Return EsGrupoQuePuedeCrearNotas AndAlso pedido IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(pedido.cliente)
        End Get
    End Property

    ''' <summary>La línea de texto de la nota nueva, para que la vista ponga ahí el foco.</summary>
    Public Event FocoEnTextoDeLineaSolicitado As EventHandler(Of LineaPedidoVentaWrapper)

    ''' <summary>
    ''' «Nueva nota de recogida»: con el cliente (y contacto) del pedido en pantalla monta un pedido nuevo con la casilla
    ''' «Nota de entrega» marcada, tipo Recogida, 1 bulto y una línea de texto con el foco para escribir «Recoger …».
    ''' Al grabar va al POST con notaEntrega = true.
    ''' </summary>
    Friend Async Function NuevaNotaRecogidaAsync() As Task
        If Not PuedeCrearNotaRecogida Then
            Return
        End If
        If TieneCambiosSinGuardar AndAlso
            Not Await dialogService.ShowConfirmationAsync("Cambios sin guardar",
                "El pedido que tiene en pantalla tiene cambios sin guardar y se perderán." & vbCrLf & vbCrLf & "¿Quiere crear la nota de recogida igualmente?") Then
            Return
        End If

        Dim empresa As String = If(String.IsNullOrWhiteSpace(pedido.empresa), EMPRESA_POR_DEFECTO, pedido.empresa)
        Dim cliente As String = pedido.cliente
        Dim contacto As String = pedido.contacto
        Try
            Dim nueva As PedidoVentaWrapper = Await CrearPedidoVentaWrapperNuevo(empresa)
            nueva.Model.cliente = cliente
            nueva.Model.contacto = contacto
            nueva.Model.notaEntrega = True
            nueva.Model.tipoNotaEntrega = TiposNotaEntrega.RECOGIDA
            nueva.Model.bultosNota = 1

            Titulo = "Nueva nota de recogida"
            _snapshotPedidoGuardado = Nothing
            pedido = nueva
            fechaEntrega = Date.Today
            ListaEnlacesSeguimiento = New List(Of EnvioAgenciaDTO)()
            RecogerProducto = False
            ivaOriginal = IVA_POR_DEFECTO
            CobroTarjetaImporte = 0

            Dim linea As New LineaPedidoVentaWrapper() With {
                .tipoLinea = CByte(0),
                .estado = CShort(Constantes.LineasPedido.ESTADO_LINEA_EN_CURSO)
            }
            pedido.Lineas.Add(linea)
            CompletarDatosLineaNueva(linea)
            lineaActual = linea
            RaiseEvent FocoEnTextoDeLineaSolicitado(Me, linea)
        Catch ex As Exception
            dialogService.ShowError($"No se ha podido preparar la nota de recogida: {ex.Message}")
        End Try
    End Function
#End Region

End Class
