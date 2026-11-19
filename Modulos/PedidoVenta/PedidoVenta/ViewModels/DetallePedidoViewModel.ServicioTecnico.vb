Imports System.Collections.ObjectModel
Imports Nesto.Models

''' <summary>
''' NestoAPI#881 (corte 2): el «Servicio técnico» de una nota de entrega o de recogida, en el panel de la nota. Lo marcan Alfredo o Santiago
''' al crearla: un combo editable con los tipos que ya hay (empieza vacío; escribir uno nuevo lo crea la API) y unos minutos opcionales. Vacío =
''' no lleva. En una nota nueva viaja con el POST del pedido; en una grabada, cada cambio va al momento por
''' PUT api/NotasEntrega/{empresa}/{pedido}/ServicioTecnico (como el tipo y los bultos de la nota). Entra en el fin previsto de la ruta.
''' </summary>
Partial Public Class DetallePedidoViewModel

    Public Const MAXIMO_MINUTOS_SERVICIO As Integer = 600
    Public Shared ReadOnly MENSAJE_MINUTOS_SERVICIO As String = $"Los minutos del servicio técnico tienen que ser un número de 1 a {MAXIMO_MINUTOS_SERVICIO} (o vacío: los del tipo)."

    ''' <summary>Los nombres del combo (los tipos activos de la API).</summary>
    Public ReadOnly Property TiposServicioTecnico As New ObservableCollection(Of String)()

    Private _tiposServicioTecnicoCargados As Boolean
    ''' <summary>La lectura de los tipos (para los tests).</summary>
    Friend Property CargaTiposServicioTecnicoEnCurso As Task = Task.CompletedTask
    ''' <summary>La lectura del servicio técnico de la nota en pantalla (para los tests).</summary>
    Friend Property CargaServicioTecnicoEnCurso As Task = Task.CompletedTask
    ''' <summary>El último PUT del servicio técnico (para los tests).</summary>
    Friend Property CambioServicioTecnicoEnCurso As Task = Task.CompletedTask

    ''' <summary>Solo en las notas de entrega o de recogida y para quien puede crear notas.</summary>
    Public ReadOnly Property PuedeIndicarServicioTecnico As Boolean
        Get
            Return EsNotaEntrega AndAlso EsGrupoQuePuedeCrearNotas
        End Get
    End Property

    Private _servicioTecnico As String
    ''' <summary>El tipo de servicio técnico (texto del combo editable). Vacío = la nota no lleva.</summary>
    Public Property ServicioTecnico As String
        Get
            Return _servicioTecnico
        End Get
        Set(value As String)
            Dim nuevo As String = NormalizarServicioTecnico(value)
            If String.Equals(nuevo, _servicioTecnico, StringComparison.CurrentCultureIgnoreCase) Then
                Return
            End If
            Dim anterior = (_servicioTecnico, _minutosServicioTexto)
            _servicioTecnico = nuevo
            If nuevo Is Nothing Then
                _minutosServicioTexto = Nothing ' sin tipo no hay minutos
            End If
            NotificarCambioServicioTecnico()
            GuardarServicioTecnico(anterior)
        End Set
    End Property

    Private _minutosServicioTexto As String
    ''' <summary>Los minutos tecleados (opcional). Vacío = los del tipo (o 60).</summary>
    Public Property MinutosServicioTexto As String
        Get
            Return _minutosServicioTexto
        End Get
        Set(value As String)
            Dim nuevo As String = If(String.IsNullOrWhiteSpace(value), Nothing, value.Trim())
            If nuevo = _minutosServicioTexto Then
                Return
            End If
            If nuevo IsNot Nothing AndAlso Not MinutosServicioValidos(nuevo).HasValue Then
                dialogService?.ShowError(MENSAJE_MINUTOS_SERVICIO)
                OnPropertyChanged(NameOf(MinutosServicioTexto))
                Return
            End If
            Dim anterior = (_servicioTecnico, _minutosServicioTexto)
            _minutosServicioTexto = nuevo
            NotificarCambioServicioTecnico()
            If _servicioTecnico IsNot Nothing Then
                GuardarServicioTecnico(anterior)
            End If
        End Set
    End Property

    ''' <summary>Lo que se cuenta en el fin de ruta, para que se vea (tecleados, del tipo o 60).</summary>
    Public ReadOnly Property TextoMinutosServicio As String
        Get
            If _servicioTecnico Is Nothing Then
                Return Nothing
            End If
            Dim tecleados As Integer? = MinutosServicioValidos(_minutosServicioTexto)
            If tecleados.HasValue Then
                Return Nothing
            End If
            Return If(_minutosEstimadosServicio.HasValue, $"(sin minutos se cuentan {_minutosEstimadosServicio.Value})", "(sin minutos se cuentan los del tipo o 60)")
        End Get
    End Property

    Private _minutosEstimadosServicio As Integer?

    Friend Shared Function NormalizarServicioTecnico(texto As String) As String
        If String.IsNullOrWhiteSpace(texto) Then
            Return Nothing
        End If
        Return System.Text.RegularExpressions.Regex.Replace(texto.Trim(), "\s+", " ")
    End Function

    Friend Shared Function MinutosServicioValidos(texto As String) As Integer?
        Dim minutos As Integer
        If Not String.IsNullOrWhiteSpace(texto) AndAlso Integer.TryParse(texto.Trim(), minutos) AndAlso minutos >= 1 AndAlso minutos <= MAXIMO_MINUTOS_SERVICIO Then
            Return minutos
        End If
        Return Nothing
    End Function

    ''' <summary>Al cargar un pedido (o la nota nueva): vacía lo de antes y, si es una nota grabada, lee su servicio técnico.</summary>
    Private Sub InicializarServicioTecnico()
        _servicioTecnico = Nothing
        _minutosServicioTexto = Nothing
        _minutosEstimadosServicio = Nothing
        CambioServicioTecnicoEnCurso = Task.CompletedTask
        CargaServicioTecnicoEnCurso = Task.CompletedTask
        NotificarCambioServicioTecnico()
        If Not PuedeIndicarServicioTecnico Then
            Return
        End If
        CargarTiposServicioTecnicoSiHaceFalta()
        If pedido IsNot Nothing AndAlso pedido.numero <> 0 Then
            CargaServicioTecnicoEnCurso = CargarServicioTecnicoAsync(pedido.empresa, pedido.numero)
        End If
    End Sub

    Friend Sub CargarTiposServicioTecnicoSiHaceFalta()
        If _tiposServicioTecnicoCargados OrElse servicio Is Nothing Then
            Return
        End If
        _tiposServicioTecnicoCargados = True
        CargaTiposServicioTecnicoEnCurso = CargarTiposServicioTecnicoAsync()
    End Sub

    Private Async Function CargarTiposServicioTecnicoAsync() As Task
        Try
            Dim tipos As List(Of TipoServicioTecnicoDTO) = Await servicio.LeerTiposServicioTecnico().ConfigureAwait(True)
            TiposServicioTecnico.Clear()
            For Each tipo In If(tipos, New List(Of TipoServicioTecnicoDTO)()).Where(Function(t) Not String.IsNullOrWhiteSpace(t?.Nombre))
                TiposServicioTecnico.Add(tipo.Nombre.Trim())
            Next
        Catch ex As Exception
            _tiposServicioTecnicoCargados = False ' que lo vuelva a intentar en la siguiente nota; el combo sigue admitiendo escribir
        End Try
    End Function

    Private Async Function CargarServicioTecnicoAsync(empresa As String, numero As Integer) As Task
        Try
            Dim leido As ServicioTecnicoNotaDTO = Await servicio.LeerServicioTecnicoNota(empresa, numero).ConfigureAwait(True)
            If pedido Is Nothing OrElse pedido.numero <> numero Then
                Return ' el usuario ya está en otro pedido
            End If
            AplicarServicioTecnico(leido)
        Catch ex As Exception
            ' Si no se puede leer se queda vacío (como una nota sin servicio técnico): no se molesta al usuario.
        End Try
    End Function

    Private Sub AplicarServicioTecnico(leido As ServicioTecnicoNotaDTO)
        _servicioTecnico = NormalizarServicioTecnico(leido?.ServicioTecnico)
        _minutosServicioTexto = If(_servicioTecnico IsNot Nothing AndAlso leido?.MinutosServicio.HasValue, leido.MinutosServicio.Value.ToString(), Nothing)
        _minutosEstimadosServicio = If(_servicioTecnico IsNot Nothing, leido?.MinutosEstimados, Nothing)
        If _servicioTecnico IsNot Nothing AndAlso Not TiposServicioTecnico.Any(Function(t) String.Equals(t, _servicioTecnico, StringComparison.CurrentCultureIgnoreCase)) Then
            TiposServicioTecnico.Add(_servicioTecnico)
        End If
        NotificarCambioServicioTecnico()
    End Sub

    ''' <summary>
    ''' Nota nueva: se apunta en el pedido (lo lleva el POST). Nota grabada: PUT al momento; si la API lo rechaza, su mensaje y vuelve atrás.
    ''' </summary>
    Private Sub GuardarServicioTecnico(anterior As (Tipo As String, Minutos As String))
        If pedido Is Nothing OrElse Not EsNotaEntrega Then
            Return
        End If
        If pedido.numero = 0 Then
            CopiarServicioTecnicoAlPedido()
            Return
        End If
        CambioServicioTecnicoEnCurso = GuardarServicioTecnicoAsync(anterior)
    End Sub

    Private Async Function GuardarServicioTecnicoAsync(anterior As (Tipo As String, Minutos As String)) As Task
        Dim empresa As String = pedido.empresa
        Dim numero As Integer = pedido.numero
        Try
            Dim respuesta As ServicioTecnicoNotaDTO = Await servicio.CambiarServicioTecnicoNota(empresa, numero, _servicioTecnico, MinutosServicioValidos(_minutosServicioTexto)).ConfigureAwait(True)
            If pedido Is Nothing OrElse pedido.numero <> numero Then
                Return
            End If
            If respuesta IsNot Nothing Then
                AplicarServicioTecnico(respuesta)
            End If
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            If pedido Is Nothing OrElse pedido.numero <> numero Then
                Return
            End If
            _servicioTecnico = anterior.Tipo
            _minutosServicioTexto = anterior.Minutos
            NotificarCambioServicioTecnico()
        End Try
    End Function

    ''' <summary>Lo que viaja en el POST de la nota nueva (por nombre: si el tipo no existe, la API lo crea).</summary>
    Private Sub CopiarServicioTecnicoAlPedido()
        If pedido?.Model Is Nothing Then
            Return
        End If
        If pedido.notaEntrega AndAlso _servicioTecnico IsNot Nothing Then
            pedido.Model.tipoServicioTecnicoNombre = _servicioTecnico
            pedido.Model.minutosServicio = MinutosServicioValidos(_minutosServicioTexto)
        Else
            pedido.Model.tipoServicioTecnicoNombre = Nothing
            pedido.Model.minutosServicio = Nothing
        End If
        pedido.Model.tipoServicioTecnicoId = Nothing
    End Sub

    Private Sub NotificarCambioServicioTecnico()
        OnPropertyChanged(NameOf(ServicioTecnico))
        OnPropertyChanged(NameOf(MinutosServicioTexto))
        OnPropertyChanged(NameOf(TextoMinutosServicio))
        OnPropertyChanged(NameOf(PuedeIndicarServicioTecnico))
    End Sub

End Class
