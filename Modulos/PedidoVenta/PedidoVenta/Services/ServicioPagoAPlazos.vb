Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Newtonsoft.Json

Namespace Services
    ''' <summary>Nesto#640 (NestoAPI#864): api/PedidosVenta/{empresa}/{pedido}/PagoAPlazos. Los errores, con el «Message» de la API.</summary>
    Public Class ServicioPagoAPlazos
        Implements IServicioPagoAPlazos

        Private ReadOnly _clienteApiFactory As IClienteApiFactory

        Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
            _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
        End Sub

        Friend Sub New(clienteApiFactory As IClienteApiFactory)
            _clienteApiFactory = clienteApiFactory
        End Sub

        Public Shared Function Ruta(empresa As String, pedido As Integer) As String
            Dim empresaRuta As String = Uri.EscapeDataString(If(String.IsNullOrWhiteSpace(empresa), Constantes.Empresas.EMPRESA_DEFECTO, empresa.Trim()))
            Return $"PedidosVenta/{empresaRuta}/{pedido}/PagoAPlazos"
        End Function

        Public Async Function LeerDisponible(empresa As String, pedido As Integer) As Task(Of DisponibilidadPagoAPlazosModel) Implements IServicioPagoAPlazos.LeerDisponible
            Dim body As String = Await Enviar(HttpMethod.Get, Ruta(empresa, pedido) & "/Disponible", Nothing)
            Return JsonConvert.DeserializeObject(Of DisponibilidadPagoAPlazosModel)(body)
        End Function

        Public Async Function Solicitar(empresa As String, pedido As Integer, canal As String, correo As String) As Task(Of SolicitudPagoAPlazosModel) Implements IServicioPagoAPlazos.Solicitar
            Dim cuerpo = New With {
                .Canal = If(String.IsNullOrWhiteSpace(canal), Nothing, canal.Trim()),
                .Correo = If(String.IsNullOrWhiteSpace(correo), Nothing, correo.Trim())
            }
            Dim body As String = Await Enviar(HttpMethod.Post, Ruta(empresa, pedido), cuerpo)
            Return JsonConvert.DeserializeObject(Of SolicitudPagoAPlazosModel)(body)
        End Function

        Public Async Function LeerEstado(empresa As String, pedido As Integer) As Task(Of EstadoPagoAPlazosModel) Implements IServicioPagoAPlazos.LeerEstado
            Dim body As String = Await Enviar(HttpMethod.Get, Ruta(empresa, pedido) & "/Estado", Nothing)
            Return JsonConvert.DeserializeObject(Of EstadoPagoAPlazosModel)(body)
        End Function

        Public Async Function Cancelar(empresa As String, pedido As Integer, motivo As String) As Task(Of EstadoPagoAPlazosModel) Implements IServicioPagoAPlazos.Cancelar
            Dim url As String = Ruta(empresa, pedido)
            If Not String.IsNullOrWhiteSpace(motivo) Then
                url &= "?motivo=" & Uri.EscapeDataString(motivo.Trim())
            End If
            Dim body As String = Await Enviar(HttpMethod.Delete, url, Nothing)
            Return JsonConvert.DeserializeObject(Of EstadoPagoAPlazosModel)(body)
        End Function

        ' Las claves del interruptor en la API (ConfiguracionPagoAPlazos de NestoAPI#864) y el usuario del que cuelgan
        Friend Const CLAVE_INTERRUPTOR As String = "PagoAPlazosPedidos"
        Friend Const CLAVE_CLIENTES_PRUEBA As String = "PagoAPlazosPedidosClientesPrueba"
        Friend Const USUARIO_DEFECTO As String = "(defecto)"

        Public Async Function LeerInterruptor() As Task(Of InterruptorPagoAPlazosModel) Implements IServicioPagoAPlazos.LeerInterruptor
            ' Se pide la fila de «(defecto)» a propósito: pedida con el usuario, la API le crearía una copia que ya no cambiaría
            Dim interruptor As New InterruptorPagoAPlazosModel With {.Modo = Await LeerParametroDefecto(CLAVE_INTERRUPTOR)}
            If String.Equals(interruptor.Modo, "Pruebas", StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(interruptor.Modo, "Sandbox", StringComparison.OrdinalIgnoreCase) Then
                Dim clientes As String = Nothing
                Try
                    clientes = Await LeerParametroDefecto(CLAVE_CLIENTES_PRUEBA)
                Catch ex As Exception
                    ' sin lista de clientes de prueba, a nadie
                End Try
                interruptor.ClientesPrueba = clientes
            End If
            Return interruptor
        End Function

        Private Async Function LeerParametroDefecto(clave As String) As Task(Of String)
            Dim url As String = $"ParametrosUsuario?empresa={Uri.EscapeDataString(Constantes.Empresas.EMPRESA_DEFECTO)}" &
                $"&usuario={Uri.EscapeDataString(USUARIO_DEFECTO)}&clave={Uri.EscapeDataString(clave)}"
            Dim body As String = Await Enviar(HttpMethod.Get, url, Nothing)
            Return If(JsonConvert.DeserializeObject(Of String)(body), String.Empty).Trim()
        End Function

        Private Async Function Enviar(metodo As HttpMethod, url As String, cuerpo As Object) As Task(Of String)
            Using client As HttpClient = _clienteApiFactory.Crear()
                Using request As New HttpRequestMessage(metodo, url)
                    If cuerpo IsNot Nothing Then
                        request.Content = New StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json")
                    End If
                    Dim response As HttpResponseMessage = Await client.SendAsync(request)
                    Dim body As String = Await response.Content.ReadAsStringAsync()
                    If Not response.IsSuccessStatusCode Then
                        Throw New Exception(ConsultaReembolso.ExtraerMensaje(body, $"Error {CInt(response.StatusCode)} de la API."))
                    End If
                    Return body
                End Using
            End Using
        End Function
    End Class
End Namespace
