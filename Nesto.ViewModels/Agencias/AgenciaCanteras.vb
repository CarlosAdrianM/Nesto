Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.Text
Imports System.Windows
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

' Nesto#359: Canteras es la agencia que hace los envíos a Canarias. No tiene integración
' (ni web service, ni impresión de etiquetas): el flujo es 100% manual — se les manda un
' correo con los datos del envío y la factura adjunta (la necesitan para el DUA), y nos
' contestan con el nº de envío, que el usuario tiene que pegar en el campo CodigoBarras
' desde la pestaña de envíos. La validación de mínimos (400€ pedido o 100€ portes) y la
' prohibición de contra reembolso se hacen en NestoAPI al crear la etiqueta (NestoAPI#204).
Public Class AgenciaCanteras
    Implements IAgencia

    ' Hardcoded a propósito: es responsabilidad del cliente saber a qué buzón notificar.
    ' Si Canteras lo cambia, se toca aquí y se republica. NO debe filtrarse a NestoAPI:
    ' el endpoint de correo es genérico y no conoce "Canteras".
    Public Const CORREO_CANTERAS As String = "recogidas.mad@canteras.com"

    Public Sub New()
        ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "NO")
        }
        ' AgenciasViewModel.agenciaSeleccionada hace listaServicios.Single(s => s.ServicioId = ServicioDefecto)
        ' al seleccionar la agencia desde las pestañas PEDIDOS / EN_CURSO / ETIQUETAS. Si la lista
        ' está vacía la excepción la traga el Catch genérico mostrando un "No se encuentra la
        ' implementación..." engañoso. Exponemos una tarifa placeholder con ServicioId=0 (el
        ' ServicioDefecto de Canteras) y CosteEnvio vacío: CalcularCostoEnvio devuelve MaxValue
        ' para listas vacías, así que esta tarifa nunca gana la auto-tarificación.
        ListaServicios = New ObservableCollection(Of ITarifaAgencia) From {
            New TarifaCanterasPlaceholder()
        }
        ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "")
        }
        ListaPaises = rellenarPaises()
    End Sub

    Public ReadOnly Property NumeroCliente As String Implements IAgencia.NumeroCliente
        Get
            Return String.Empty
        End Get
    End Property

    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        Throw New Exception("Canteras no permite integración. Consulta el estado por correo con Canteras o usa el nº de envío que indicaron.")
    End Function

    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Return If(IsNothing(envio), Nothing, New estadoEnvio)
    End Function

    Public Function calcularCodigoBarras(envio As EnviosAgencia, agencia As AgenciasTransporte) As String Implements IAgencia.calcularCodigoBarras
        ' Canteras devuelve el nº de envío por correo. El usuario lo pega manualmente; el
        ' cálculo automático no aplica.
        Return String.Empty
    End Function

    Public Sub calcularPlaza(ByVal codPostal As String, codPais As Integer, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        nemonico = "CN"
        nombrePlaza = "Canteras"
        telefonoPlaza = String.Empty
        emailPlaza = String.Empty
    End Sub

    Public Async Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgencia.LlamadaWebService
        ' Tramitar = mandar el correo a Canteras con la factura adjunta. La respuesta del
        ' servicio determina Exito; AgenciasViewModel ya muestra TextoRespuestaError al
        ' usuario (vía dialogService) cuando Exito=False, así que basta con propagarlo.
        If Not envio.Pedido.HasValue Then
            Return New RespuestaAgencia With {
                .Agencia = "Canteras",
                .Fecha = Date.Now,
                .UrlLlamada = String.Empty,
                .Exito = False,
                .CuerpoLlamada = String.Empty,
                .CuerpoRespuesta = String.Empty,
                .TextoRespuestaError = "El envío no tiene pedido asociado."
            }
        End If

        Dim asunto As String = $"Solicitud de recogida — Pedido {envio.Pedido.Value} ({envio.Nombre?.Trim()})"
        Dim cuerpo As String = ComponerCuerpoCorreo(envio)

        Dim respuestaCorreo = Await servicio.EnviarCorreoConFacturaDelPedido(envio.Empresa, envio.Pedido.Value, CORREO_CANTERAS, asunto, cuerpo).ConfigureAwait(False)

        Return New RespuestaAgencia With {
            .Agencia = "Canteras",
            .Fecha = Date.Now,
            .UrlLlamada = String.Empty,
            .Exito = respuestaCorreo.Exito,
            .CuerpoLlamada = cuerpo,
            .CuerpoRespuesta = String.Empty,
            .TextoRespuestaError = respuestaCorreo.Mensaje
        }
    End Function

    Public Shared Function ComponerCuerpoCorreo(envio As EnviosAgencia) As String
        ' Réplica del correo manual que se mandaba antes de automatizar (Nesto#359):
        ' identificador cliente, nombre, dirección, CP+población+provincia, teléfono,
        ' bultos, peso y medidas (Nesto#367: el usuario las indica al tramitar y se guardan en
        ' Observaciones), y mención del adjunto.
        Dim sb As New StringBuilder()
        sb.AppendLine("Buenas tardes,")
        sb.AppendLine()
        sb.AppendLine("Necesitamos recogida de un envío a:")

        Dim identificador As String = $"{envio.Cliente?.Trim()}/{envio.Contacto?.Trim()}".Trim("/"c)
        If Not String.IsNullOrWhiteSpace(identificador) Then
            sb.AppendLine(identificador)
        End If
        If Not String.IsNullOrWhiteSpace(envio.Nombre) Then sb.AppendLine(envio.Nombre.Trim())
        If Not String.IsNullOrWhiteSpace(envio.Direccion) Then sb.AppendLine(envio.Direccion.Trim())

        Dim ubicacion As String = String.Join(" ", New String() {envio.CodPostal?.Trim(), envio.Poblacion?.Trim()}.Where(Function(s) Not String.IsNullOrWhiteSpace(s)))
        If Not String.IsNullOrWhiteSpace(envio.Provincia) Then
            ubicacion = $"{ubicacion} ({envio.Provincia.Trim()})"
        End If
        If Not String.IsNullOrWhiteSpace(ubicacion) Then sb.AppendLine(ubicacion.Trim())

        Dim telefono As String = If(Not String.IsNullOrWhiteSpace(envio.Telefono), envio.Telefono?.Trim(), envio.Movil?.Trim())
        If Not String.IsNullOrWhiteSpace(telefono) Then sb.AppendLine(telefono)

        sb.AppendLine()
        Dim bultos As Integer = envio.Bultos
        Dim pesoTexto As String = envio.Peso.ToString("0.##", CultureInfo.InvariantCulture)
        If bultos = 1 Then
            sb.AppendLine($"Es un bulto y unos {pesoTexto} kgs.")
        Else
            sb.AppendLine($"Son {bultos} bultos y unos {pesoTexto} kgs en total.")
        End If

        ' Nesto#367: las medidas de los bultos las indica el usuario al tramitar y quedan en Observaciones.
        If Not String.IsNullOrWhiteSpace(envio.Observaciones) Then
            sb.AppendLine($"Medidas de los bultos: {envio.Observaciones.Trim()}")
        End If

        sb.AppendLine()
        sb.AppendLine("Adjuntamos factura.")
        sb.AppendLine()
        sb.AppendLine("Gracias.")
        Return sb.ToString()
    End Function

    Public Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        ' Canteras no imprime etiquetas en local — el aviso viaja con el correo a Canteras al
        ' tramitar. No-op a propósito (Nesto#367): al "imprimir etiqueta" el operario solo indica
        ' las dimensiones de los bultos (lo gestiona AgenciasViewModel antes de llamar aquí); no hay
        ' nada que imprimir, pero tampoco debe lanzar error ni cortar el flujo.
    End Sub

    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Collapsed
        End Get
    End Property

    Public ReadOnly Property retornoSoloCobros As Byte Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' NO (Canteras no admite contra reembolso)
        End Get
    End Property

    Public ReadOnly Property servicioSoloCobros As Byte Implements IAgencia.servicioSoloCobros
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property horarioSoloCobros As Byte Implements IAgencia.horarioSoloCobros
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property retornoSinRetorno As Byte Implements IAgencia.retornoSinRetorno
        Get
            Return 0 ' NO
        End Get
    End Property

    Public ReadOnly Property retornoObligatorio As Byte Implements IAgencia.retornoObligatorio
        Get
            Return 0 ' NO
        End Get
    End Property

    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 34 ' España (Canarias)
        End Get
    End Property

    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA")
        }
    End Function

    Private Function IAgencia_EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        ' Canteras no tiene web de tracking; el usuario consulta el estado por correo.
        Return String.Empty
    End Function

    Public ReadOnly Property LoggingDetallado As Boolean Implements IAgencia.LoggingDetallado
        Get
            Return False
        End Get
    End Property

    Public Function RespuestaYaTramitada(respuesta As String) As Boolean Implements IAgencia.RespuestaYaTramitada
        Return False
    End Function

    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of ITarifaAgencia) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios

    Public ReadOnly Property ServicioDefecto As Byte Implements IAgencia.ServicioDefecto
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Byte Implements IAgencia.HorarioDefecto
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property ServicioAuxiliar As Byte Implements IAgencia.ServicioAuxiliar
        Get
            Return Byte.MaxValue
        End Get
    End Property

    Public ReadOnly Property ServicioCreaEtiquetaRetorno As Byte Implements IAgencia.ServicioCreaEtiquetaRetorno
        Get
            Return Byte.MaxValue
        End Get
    End Property

    Public ReadOnly Property PermiteEditarCodigoBarras As Boolean Implements IAgencia.PermiteEditarCodigoBarras
        Get
            ' Canteras devuelve el nº de envío por correo; el usuario lo pega a mano.
            Return True
        End Get
    End Property

    Public ReadOnly Property FlujoTramitacion As TipoFlujoTramitacion Implements IAgencia.FlujoTramitacion
        Get
            Return TipoFlujoTramitacion.TramitarAlCerrar
        End Get
    End Property

    Public ReadOnly Property DimensionesBultosObligatorias As Boolean Implements IAgencia.DimensionesBultosObligatorias
        Get
            ' Canteras necesita las medidas de los bultos en el aviso de recogida (Nesto#367).
            Return True
        End Get
    End Property

    Private Class TarifaCanterasPlaceholder
        Implements ITarifaAgencia

        Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
            Get
                Return 11
            End Get
        End Property

        Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
            Get
                Return 0
            End Get
        End Property

        Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
            Get
                Return "Canteras"
            End Get
        End Property

        Public ReadOnly Property HorarioDefectoId As Byte Implements ITarifaAgencia.HorarioDefectoId
            Get
                Return 0
            End Get
        End Property

        ' CosteEnvio vacío → CalcularCostoEnvio devuelve Decimal.MaxValue y Canteras nunca
        ' gana la auto-tarificación (la entrega a Canarias se decide manualmente).
        Public ReadOnly Property CosteEnvio As List(Of (Decimal, ZonasEnvioAgencia, Decimal)) Implements ITarifaAgencia.CosteEnvio
            Get
                Return New List(Of (Decimal, ZonasEnvioAgencia, Decimal))
            End Get
        End Property

        Public Function CosteKiloAdicional(zona As ZonasEnvioAgencia) As Decimal Implements ITarifaAgencia.CosteKiloAdicional
            Return 0D
        End Function

        Public Function CosteReembolso(reembolso As Decimal) As Decimal Implements ITarifaAgencia.CosteReembolso
            Return 0D
        End Function
    End Class

End Class
