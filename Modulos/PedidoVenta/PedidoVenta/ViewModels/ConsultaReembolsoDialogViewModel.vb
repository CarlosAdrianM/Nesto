Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared

''' <summary>
''' Nesto#568 (NestoAPI#734): Administración contesta si un pedido retenido por el almacén sale con o sin reembolso. Se abre
''' desde el aviso de la campana (Tipo «ConsultaReembolso», accion «consulta») con «empresa» y «pedido». Enseña quién
''' preguntó, el cliente y los enlaces de pago pendientes, y ofrece:
''' <list type="bullet">
''' <item>«Sale con reembolso», con la casilla «Anular el enlace pendiente» (nunca automático: el enlace puede ser de otro pedido).</item>
''' <item>«Sale sin reembolso», con la forma de pago y los plazos pactados con el cliente. Lo que no vale (tarjeta o prepago
''' sin estar pagado, una forma que sigue siendo contra reembolso…) lo dice la API y se enseña tal cual.</item>
''' </list>
''' La API cambia el pedido, lo libera y avisa a quien preguntó.
''' </summary>
Public Class ConsultaReembolsoDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "ConsultaReembolsoDialog"
    Public Const PARAMETRO_EMPRESA As String = "empresa"
    Public Const PARAMETRO_PEDIDO As String = "pedido"

    Private ReadOnly _servicio As IConsultaReembolsoService

    Public Sub New(servicio As IConsultaReembolsoService, configuracion As IConfiguracion)
        _servicio = servicio
        Me.Configuracion = configuracion
        Title = "¿Sale con o sin reembolso?"
        ResponderCommand = New AsyncRelayCommand(AddressOf ResponderAsync, AddressOf PuedeResponder)
        CerrarCommand = New RelayCommand(AddressOf Cerrar)
    End Sub

    ''' <summary>Para los selectores de forma de pago y plazos.</summary>
    Public ReadOnly Property Configuracion As IConfiguracion

    Public ReadOnly Property ResponderCommand As AsyncRelayCommand
    Public ReadOnly Property CerrarCommand As RelayCommand

    Private _empresa As String = Constantes.Empresas.EMPRESA_DEFECTO
    Public Property Empresa As String
        Get
            Return _empresa
        End Get
        Set(value As String)
            Dim unused = SetProperty(_empresa, value)
        End Set
    End Property

    Private _pedido As Integer
    Public Property Pedido As Integer
        Get
            Return _pedido
        End Get
        Set(value As Integer)
            Dim unused = SetProperty(_pedido, value)
        End Set
    End Property

    Private _consulta As ConsultaReembolsoListadoModel
    ''' <summary>La consulta abierta del pedido (Nothing hasta cargarla o si no hay ninguna abierta).</summary>
    Public Property Consulta As ConsultaReembolsoListadoModel
        Get
            Return _consulta
        End Get
        Private Set(value As ConsultaReembolsoListadoModel)
            If SetProperty(_consulta, value) Then
                OnPropertyChanged(NameOf(Cliente))
                OnPropertyChanged(NameOf(TextoConsulta))
                OnPropertyChanged(NameOf(Enlaces))
                OnPropertyChanged(NameOf(HayConsultaAbierta))
                ResponderCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Public ReadOnly Property Cliente As String
        Get
            Return Consulta?.Cliente?.Trim()
        End Get
    End Property

    Public ReadOnly Property HayConsultaAbierta As Boolean
        Get
            Return Consulta IsNot Nothing AndAlso Not Contestada
        End Get
    End Property

    ''' <summary>«Alfredo retuvo el pedido 929300 del cliente 40026/0 (CLIENTA) el 29/10/2026 a las 10:15 y pregunta…».</summary>
    Public ReadOnly Property TextoConsulta As String
        Get
            Return TextoDe(Consulta)
        End Get
    End Property

    Public Shared Function TextoDe(consulta As ConsultaReembolsoListadoModel) As String
        If consulta Is Nothing Then
            Return String.Empty
        End If
        Dim cliente As String = consulta.Cliente?.Trim() &
            If(String.IsNullOrWhiteSpace(consulta.Contacto), String.Empty, "/" & consulta.Contacto.Trim()) &
            If(String.IsNullOrWhiteSpace(consulta.NombreCliente), String.Empty, $" ({consulta.NombreCliente.Trim()})")
        Dim texto As String = $"{consulta.Usuario?.Trim()} retuvo el pedido {consulta.Pedido} del cliente {cliente} el {consulta.Fecha:dd/MM/yyyy} a las {consulta.Fecha:HH:mm} " &
            $"y pregunta si sale con o sin reembolso: tiene enlaces de pago pendientes por {consulta.ImporteEnlaces:C}."
        If Not String.IsNullOrWhiteSpace(consulta.Comentario) Then
            texto &= $" Comentario: «{consulta.Comentario.Trim()}»."
        End If
        Return texto
    End Function

    Public ReadOnly Property Enlaces As List(Of EnlacePagoPendienteModel)
        Get
            Return If(Consulta?.EnlacesPendientes, New List(Of EnlacePagoPendienteModel))
        End Get
    End Property

    Private _saleConReembolso As Boolean = True
    Public Property SaleConReembolso As Boolean
        Get
            Return _saleConReembolso
        End Get
        Set(value As Boolean)
            If SetProperty(_saleConReembolso, value) Then
                OnPropertyChanged(NameOf(SaleSinReembolso))
                ResponderCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Public Property SaleSinReembolso As Boolean
        Get
            Return Not SaleConReembolso
        End Get
        Set(value As Boolean)
            SaleConReembolso = Not value
        End Set
    End Property

    Private _anularEnlaces As Boolean
    ''' <summary>Solo con «Sale con reembolso»: anular los enlaces pendientes del pedido.</summary>
    Public Property AnularEnlaces As Boolean
        Get
            Return _anularEnlaces
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_anularEnlaces, value)
        End Set
    End Property

    Private _formaPago As String
    ''' <summary>Solo con «Sale sin reembolso»: la forma de pago pactada (RCB, TRN…).</summary>
    Public Property FormaPago As String
        Get
            Return _formaPago
        End Get
        Set(value As String)
            If SetProperty(_formaPago, value) Then
                ResponderCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _plazosPago As String
    ''' <summary>Solo con «Sale sin reembolso»: vacío = los que tiene el pedido.</summary>
    Public Property PlazosPago As String
        Get
            Return _plazosPago
        End Get
        Set(value As String)
            Dim unused = SetProperty(_plazosPago, value)
        End Set
    End Property

    Private _comentario As String
    ''' <summary>Opcional: se le dice a quien preguntó.</summary>
    Public Property Comentario As String
        Get
            Return _comentario
        End Get
        Set(value As String)
            Dim unused = SetProperty(_comentario, value)
        End Set
    End Property

    Private _mensaje As String
    ''' <summary>El error (el de la API, tal cual) o por qué no se puede contestar.</summary>
    Public Property Mensaje As String
        Get
            Return _mensaje
        End Get
        Private Set(value As String)
            Dim unused = SetProperty(_mensaje, value)
        End Set
    End Property

    Private _textoResultado As String
    ''' <summary>Lo que ha pasado al contestar (o la respuesta que ya se dio).</summary>
    Public Property TextoResultado As String
        Get
            Return _textoResultado
        End Get
        Private Set(value As String)
            If SetProperty(_textoResultado, value) Then
                OnPropertyChanged(NameOf(HayResultado))
            End If
        End Set
    End Property

    Public ReadOnly Property HayResultado As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(TextoResultado)
        End Get
    End Property

    Private _contestada As Boolean
    Public Property Contestada As Boolean
        Get
            Return _contestada
        End Get
        Private Set(value As Boolean)
            If SetProperty(_contestada, value) Then
                OnPropertyChanged(NameOf(HayConsultaAbierta))
                ResponderCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _estaOcupado As Boolean
    Public Property EstaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Private Set(value As Boolean)
            If SetProperty(_estaOcupado, value) Then
                ResponderCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        If parameters IsNot Nothing Then
            If parameters.ContainsKey(PARAMETRO_EMPRESA) AndAlso Not String.IsNullOrWhiteSpace(parameters.GetValue(Of String)(PARAMETRO_EMPRESA)) Then
                Empresa = parameters.GetValue(Of String)(PARAMETRO_EMPRESA).Trim()
            End If
            If parameters.ContainsKey(PARAMETRO_PEDIDO) Then
                Dim valor As Object = parameters.GetValue(Of Object)(PARAMETRO_PEDIDO)
                Dim numero As Integer
                If valor IsNot Nothing AndAlso Integer.TryParse(valor.ToString(), numero) Then
                    Pedido = numero
                End If
            End If
        End If
        Title = $"Pedido {Pedido}: ¿sale con o sin reembolso?"
        Dim unused = CargarAsync()
    End Sub

    ''' <summary>La consulta abierta del pedido (de la lista de abiertas, que trae el cliente y los enlaces) o, si ya no está abierta, la respuesta que se dio.</summary>
    Public Async Function CargarAsync() As Task
        Try
            EstaOcupado = True
            Mensaje = Nothing
            Dim abiertas As List(Of ConsultaReembolsoListadoModel) = Await _servicio.LeerConsultas(Empresa, True)
            Dim abierta = abiertas?.FirstOrDefault(Function(c) c IsNot Nothing AndAlso c.Pedido = Pedido)
            If abierta IsNot Nothing Then
                Consulta = abierta
                Return
            End If
            Dim ultima As ConsultaReembolsoModel = Await _servicio.LeerConsulta(Empresa, Pedido)
            If ultima Is Nothing Then
                Mensaje = $"El pedido {Pedido} no tiene ninguna consulta de reembolso."
            ElseIf ultima.Respondida.HasValue Then
                Contestada = True
                TextoResultado = $"Ya está contestada. {ultima.TextoRespuesta}"
            Else
                Mensaje = $"No se ha podido leer la consulta abierta del pedido {Pedido}."
            End If
        Catch ex As Exception
            Mensaje = ex.Message
        Finally
            EstaOcupado = False
        End Try
    End Function

    Private Function PuedeResponder() As Boolean
        Return Not EstaOcupado AndAlso HayConsultaAbierta
    End Function

    Public Async Function ResponderAsync() As Task
        If Not PuedeResponder() Then
            Return
        End If
        If SaleSinReembolso AndAlso String.IsNullOrWhiteSpace(FormaPago) Then
            Mensaje = "Para que salga sin reembolso, indique la forma de pago pactada con el cliente (recibo bancario, transferencia…)."
            Return
        End If
        Dim respuesta As New ResponderConsultaReembolsoModel With {
            .ConReembolso = SaleConReembolso,
            .AnularEnlaces = If(SaleConReembolso AndAlso AnularEnlaces, CType(True, Boolean?), Nothing),
            .FormaPago = If(SaleSinReembolso, FormaPago.Trim(), Nothing),
            .PlazosPago = If(SaleSinReembolso AndAlso Not String.IsNullOrWhiteSpace(PlazosPago), PlazosPago.Trim(), Nothing),
            .Comentario = If(String.IsNullOrWhiteSpace(Comentario), Nothing, Comentario.Trim())
        }
        Try
            EstaOcupado = True
            Mensaje = Nothing
            Dim resultado As RespuestaConsultaReembolsoModel = Await _servicio.Responder(Empresa, Pedido, respuesta)
            Contestada = True
            TextoResultado = TextoContestado(resultado, Pedido, SaleConReembolso)
        Catch ex As Exception
            Mensaje = ex.Message ' el 400/409 de la API, tal cual
        Finally
            EstaOcupado = False
        End Try
    End Function

    ''' <summary>«Contestado: el pedido 929300 sale sin reembolso (RCB, 1/30). Ya no está retenido.» (+ avisos de la API).</summary>
    Public Shared Function TextoContestado(resultado As RespuestaConsultaReembolsoModel, pedido As Integer, conReembolso As Boolean) As String
        Dim consulta = resultado?.Consulta
        Dim decision As String = If(conReembolso, "sale con reembolso",
            "sale sin reembolso" & If(consulta Is Nothing OrElse String.IsNullOrWhiteSpace(consulta.FormaPago), String.Empty,
                $" ({consulta.FormaPago.Trim()}{If(String.IsNullOrWhiteSpace(consulta.PlazosPago), String.Empty, ", " & consulta.PlazosPago.Trim())})"))
        Dim texto As String = $"Contestado: el pedido {pedido} {decision}."
        If resultado?.EnlacesAnulados IsNot Nothing AndAlso resultado.EnlacesAnulados.Any() Then
            texto &= If(resultado.EnlacesAnulados.Count = 1, " Se ha anulado el enlace de pago pendiente.", $" Se han anulado {resultado.EnlacesAnulados.Count} enlaces de pago pendientes.")
        End If
        If resultado?.Liberado = True Then
            texto &= " Ya no está retenido: el almacén puede hacer el envío."
        End If
        texto &= " Se le ha avisado a quien preguntó."
        Return ConsultaReembolso.AnadirAvisos(texto, resultado?.Avisos)
    End Function

    Private Sub Cerrar()
        RaiseRequestClose(If(Contestada, ResultadoBoton.OK, ResultadoBoton.Cancel))
    End Sub
End Class
