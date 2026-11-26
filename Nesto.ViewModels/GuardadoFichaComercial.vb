Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#644 (5): lo que la ficha comercial puede cambiar (estado, vendedor, país fiscal y el vendedor y el estado de
''' peluquería), tal como se cargó o tal como está en pantalla. Con lo cargado se sabe qué ha tocado el usuario.
''' </summary>
Friend Class ValoresComercialesFicha
    Public Property Estado As Integer
    Public Property Vendedor As String
    Public Property Pais As String
    Public Property VendedorPeluqueria As String
    Public Property EstadoPeluqueria As Short
    ''' <summary>Si el cliente ya tiene su fila de vendedor por grupo (peluquería) en la base de datos.</summary>
    Public Property TieneFilaPeluqueria As Boolean
    Public Property GrupoProducto As String

    ''' <summary>Lo que trae la ficha de la API, antes de que la pantalla le añada nada (la fila de peluquería vacía).</summary>
    Public Shared Function DeLaFicha(ficha As ClienteJson) As ValoresComercialesFicha
        If ficha Is Nothing Then
            Return Nothing
        End If
        Dim fila As VendedorGrupoProductoDTO = ficha.VendedoresGrupoProducto?.FirstOrDefault()
        Return New ValoresComercialesFicha With {
            .Estado = ficha.estado,
            .Vendedor = ficha.vendedor?.Trim(),
            .Pais = ficha.pais?.Trim(),
            .VendedorPeluqueria = fila?.vendedor?.Trim(),
            .EstadoPeluqueria = If(fila Is Nothing, 0S, fila.estado),
            .TieneFilaPeluqueria = fila IsNot Nothing,
            .GrupoProducto = If(String.IsNullOrWhiteSpace(fila?.grupoProducto), GRUPO_PELUQUERIA, fila.grupoProducto.Trim())
        }
    End Function

    ''' <summary>Lo que ha quedado guardado: desde aquí se mide el siguiente guardado.</summary>
    Public Shared Function Guardados(ficha As ClienteJson, anterior As ValoresComercialesFicha,
                                     vendedorPeluqueria As String, estadoPeluqueria As Short) As ValoresComercialesFicha
        Return New ValoresComercialesFicha With {
            .Estado = ficha.estado,
            .Vendedor = ficha.vendedor?.Trim(),
            .Pais = ficha.pais?.Trim(),
            .VendedorPeluqueria = vendedorPeluqueria?.Trim(),
            .EstadoPeluqueria = estadoPeluqueria,
            .TieneFilaPeluqueria = (anterior IsNot Nothing AndAlso anterior.TieneFilaPeluqueria) OrElse Not String.IsNullOrWhiteSpace(vendedorPeluqueria),
            .GrupoProducto = If(anterior?.GrupoProducto, GRUPO_PELUQUERIA)
        }
    End Function

    ''' <summary>Nesto#644 (5): lo que hay ahora en pantalla (la ficha con lo tecleado y la peluquería elegida).</summary>
    Public Shared Function DeLaPantalla(ficha As ClienteJson, vendedorPeluqueria As String, estadoPeluqueria As Short) As ValoresComercialesFicha
        If ficha Is Nothing Then
            Return Nothing
        End If
        Return New ValoresComercialesFicha With {
            .Estado = ficha.estado,
            .Vendedor = ficha.vendedor?.Trim(),
            .Pais = ficha.pais?.Trim(),
            .VendedorPeluqueria = vendedorPeluqueria?.Trim(),
            .EstadoPeluqueria = estadoPeluqueria
        }
    End Function

    Public Const GRUPO_PELUQUERIA As String = "PEL"
End Class

''' <summary>Nesto#644 (5 y 6): el PUT de Clientes/ClienteComercial que hay que mandar, o por qué no se puede.</summary>
Friend Class PeticionGuardadoFichaComercial
    Public Property Payload As JObject
    Public Property HayCambios As Boolean
    ''' <summary>Nesto#644 (6): el guardado no haría lo que se ve en pantalla; no se manda nada.</summary>
    Public Property MotivoNoSePuede As String
    ''' <summary>
    ''' Nesto#644 (6): el cliente no tenía fila de peluquería y se le crea con un estado distinto del 0; la API la crea siempre
    ''' con el 0 (VISITA_PRESENCIAL), así que hace falta un segundo PUT, ya con la fila, con este <see cref="PayloadEstadoPeluqueria"/>.
    ''' </summary>
    Public Property PayloadEstadoPeluqueria As JObject
End Class

''' <summary>
''' Nesto#644 (5 y 6): LA REGLA de qué se manda al guardar la ficha comercial. La API (AplicarCambiosClienteComercial y
''' GestorComisiones.ActualizarVendedorClienteGrupoProducto) solo toca lo que llega: estado, vendedor o país a null y los
''' vendedores por grupo sin ninguna fila = no tocar. Antes se mandaba todo tal como se cargó y, si otro usuario había cambiado
''' algo entre medias, se le pisaba. Ahora va solo lo que el usuario ha cambiado respecto a lo cargado.
''' Además va el «original» (lo cargado): si lo que se cambia ya lo había cambiado otro, la API contesta 409 CLIENTE_CAMBIADO
''' y la pantalla ofrece recargar. Lo que sigue sin poderse: cambiar solo el vendedor o solo el estado de peluquería (la API
''' escribe los dos).
''' </summary>
Friend Module GuardadoFichaComercial

    Public Const MOTIVO_ESTADO_PELUQUERIA_SIN_VENDEDOR As String =
        "Para guardar el estado de peluquería, elija también el vendedor de peluquería."
    Public Const MOTIVO_VENDEDOR_PELUQUERIA_EN_BLANCO As String =
        "El vendedor de peluquería no puede quedar en blanco: elija uno."
    ''' <summary>Constantes.Clientes.Estados.VISITA_PRESENCIAL de la API: el estado con que crea la fila de peluquería.</summary>
    Public Const ESTADO_PELUQUERIA_AL_CREAR As Short = 0S

    ''' <param name="ficha">clienteServidor, con el estado de la pantalla ya puesto.</param>
    ''' <param name="cargado">Lo que se cargó (Nothing = no se sabe: se manda todo, como antes).</param>
    Public Function Preparar(ficha As ClienteJson, cargado As ValoresComercialesFicha,
                             vendedorPeluqueria As String, estadoPeluqueria As Short, usuario As String) As PeticionGuardadoFichaComercial
        Dim payload As JObject = JObject.FromObject(ficha)
        ' Nesto#340 (1C.8, slice 4): el PUT solo gestiona vendedores; sin PersonasContacto para que el [EmailAddress] del DTO
        ' del servidor no devuelva 400 por correos mal grabados en fichas antiguas.
        Dim unused = payload.Remove("PersonasContacto")
        Dim peticion As New PeticionGuardadoFichaComercial With {.Payload = payload, .HayCambios = True}
        If cargado Is Nothing Then
            Return peticion
        End If

        Dim cambiaEstado As Boolean = ficha.estado <> cargado.Estado
        Dim cambiaVendedor As Boolean = Not MismoTexto(ficha.vendedor, cargado.Vendedor)
        Dim cambiaPais As Boolean = Not MismoTexto(ficha.pais, cargado.Pais)
        Dim cambiaPeluqueria As Boolean = Not MismoTexto(vendedorPeluqueria, cargado.VendedorPeluqueria) OrElse
            estadoPeluqueria <> cargado.EstadoPeluqueria

        ' Nesto#644 (5, parte de Nesto): lo que había al abrir la ficha. Si otro usuario ha cambiado entre medias lo que este
        ' guardado cambia, la API contesta 409 CLIENTE_CAMBIADO sin escribir nada (una API anterior lo ignora).
        payload("original") = Original(cargado)

        If Not cambiaEstado Then
            payload("estado") = JValue.CreateNull()
        End If
        If Not cambiaVendedor Then
            payload("vendedor") = JValue.CreateNull()
        End If
        If Not cambiaPais Then
            payload("pais") = JValue.CreateNull()
        End If

        If cambiaPeluqueria Then
            ' Nesto#644 (6): sin vendedor la API no crea la fila (o la deja sin vendedor) y decía «guardado correctamente»
            If String.IsNullOrWhiteSpace(vendedorPeluqueria) Then
                peticion.MotivoNoSePuede = If(cargado.TieneFilaPeluqueria, MOTIVO_VENDEDOR_PELUQUERIA_EN_BLANCO, MOTIVO_ESTADO_PELUQUERIA_SIN_VENDEDOR)
            End If
            ' Nesto#644 (6): lo que hay en pantalla, aunque la ficha no trajera la lista (antes el estado se perdía)
            payload("VendedoresGrupoProducto") = FilaPeluqueria(cargado.GrupoProducto, vendedorPeluqueria, estadoPeluqueria, usuario)
            If Not cargado.TieneFilaPeluqueria AndAlso estadoPeluqueria <> ESTADO_PELUQUERIA_AL_CREAR Then
                Dim segundo As JObject = CType(payload.DeepClone(), JObject)
                segundo("estado") = JValue.CreateNull()
                segundo("vendedor") = JValue.CreateNull()
                segundo("pais") = JValue.CreateNull()
                segundo("motivoCambioEstado") = JValue.CreateNull()
                segundo("comentarioCambioEstado") = JValue.CreateNull()
                ' La fila la acaba de crear el primer PUT: su «original» ya no vale (sería un 409 contra uno mismo). Con la API
                ' nueva (NestoAPI-901) este segundo PUT sobra, porque crea la fila con el estado que llega, pero es inofensivo y
                ' hace falta mientras quede publicada una API que la crea siempre con el 0.
                Dim unusedOriginal = segundo.Remove("original")
                peticion.PayloadEstadoPeluqueria = segundo
            End If
        Else
            payload("VendedoresGrupoProducto") = JValue.CreateNull()
        End If

        peticion.HayCambios = cambiaEstado OrElse cambiaVendedor OrElse cambiaPais OrElse cambiaPeluqueria
        Return peticion
    End Function

    ''' <summary>Nesto#644 (5): el «original» del PUT (OriginalClienteComercialDTO de la API) con lo que se cargó.</summary>
    Public Function Original(cargado As ValoresComercialesFicha) As JObject
        Return New JObject(
            New JProperty("estado", CShort(cargado.Estado)),
            New JProperty("vendedor", cargado.Vendedor),
            New JProperty("pais", cargado.Pais),
            New JProperty("vendedorPeluqueria", If(cargado.TieneFilaPeluqueria, cargado.VendedorPeluqueria, Nothing)),
            New JProperty("estadoPeluqueria", cargado.EstadoPeluqueria))
    End Function

    ''' <summary>
    ''' Nesto#644 (5): qué ha cambiado el usuario respecto a lo cargado, con los nombres de campo de la API (estado, vendedor,
    ''' pais, vendedorPeluqueria, estadoPeluqueria). Mismo criterio que <see cref="Preparar"/>.
    ''' </summary>
    Public Function CamposCambiados(cargado As ValoresComercialesFicha, pantalla As ValoresComercialesFicha) As List(Of String)
        Dim campos As New List(Of String)
        If cargado Is Nothing OrElse pantalla Is Nothing Then
            Return campos
        End If
        If pantalla.Estado <> cargado.Estado Then campos.Add(CAMPO_ESTADO)
        If Not MismoTexto(pantalla.Vendedor, cargado.Vendedor) Then campos.Add(CAMPO_VENDEDOR)
        If Not MismoTexto(pantalla.Pais, cargado.Pais) Then campos.Add(CAMPO_PAIS)
        If Not MismoTexto(pantalla.VendedorPeluqueria, cargado.VendedorPeluqueria) Then campos.Add(CAMPO_VENDEDOR_PELUQUERIA)
        If pantalla.EstadoPeluqueria <> cargado.EstadoPeluqueria Then campos.Add(CAMPO_ESTADO_PELUQUERIA)
        Return campos
    End Function

    ''' <summary>
    ''' Nesto#644 (5): al recargar la ficha tras el 409, lo que el usuario había cambiado se vuelve a poner encima de lo nuevo
    ''' SOLO en los campos que no chocan; en los que chocan se queda lo que guardó el otro y decide el usuario si lo vuelve a
    ''' cambiar. La peluquería va junta (la API escribe los dos): si choca el vendedor o el estado, no se reaplica ninguno.
    ''' </summary>
    Public Function CamposAReaplicar(cambiados As IEnumerable(Of String), enConflicto As IEnumerable(Of String)) As List(Of String)
        Dim conflicto As New HashSet(Of String)(If(enConflicto, Enumerable.Empty(Of String)()), StringComparer.OrdinalIgnoreCase)
        Dim peluqueriaChoca As Boolean = conflicto.Contains(CAMPO_VENDEDOR_PELUQUERIA) OrElse conflicto.Contains(CAMPO_ESTADO_PELUQUERIA)
        Return If(cambiados, Enumerable.Empty(Of String)()).
            Where(Function(c) Not conflicto.Contains(c)).
            Where(Function(c) Not (peluqueriaChoca AndAlso (c = CAMPO_VENDEDOR_PELUQUERIA OrElse c = CAMPO_ESTADO_PELUQUERIA))).
            ToList()
    End Function

    ''' <summary>Nesto#644 (5): cómo se llama cada campo en los mensajes (como la API).</summary>
    Public Function NombreCampo(campo As String) As String
        Select Case campo
            Case CAMPO_PAIS : Return "país"
            Case CAMPO_VENDEDOR_PELUQUERIA : Return "vendedor de peluquería"
            Case CAMPO_ESTADO_PELUQUERIA : Return "estado de peluquería"
            Case Else : Return campo
        End Select
    End Function

    ''' <summary>Nesto#644 (5): el aviso tras recargar: qué se ha conservado y qué hay que volver a hacer.</summary>
    Public Function MensajeTrasRecargar(reaplicados As IEnumerable(Of String), perdidos As IEnumerable(Of String)) As String
        Dim siguen As List(Of String) = If(reaplicados, Enumerable.Empty(Of String)()).Select(Function(c) NombreCampo(c)).ToList()
        Dim fuera As List(Of String) = If(perdidos, Enumerable.Empty(Of String)()).Select(Function(c) NombreCampo(c)).ToList()
        Dim texto As String = "Se ha recargado la ficha con lo que hay guardado ahora."
        If siguen.Any() Then
            texto &= $" Se mantienen sus cambios en: {String.Join(", ", siguen)}."
        End If
        If fuera.Any() Then
            texto &= $" En {String.Join(", ", fuera)} se ha quedado lo que guardó el otro usuario: si quiere cambiarlo, vuelva a hacerlo."
        End If
        If siguen.Any() OrElse fuera.Any() Then
            texto &= " Revise la ficha y pulse Guardar."
        End If
        Return texto
    End Function

    Public Const CAMPO_ESTADO As String = "estado"
    Public Const CAMPO_VENDEDOR As String = "vendedor"
    Public Const CAMPO_PAIS As String = "pais"
    Public Const CAMPO_VENDEDOR_PELUQUERIA As String = "vendedorPeluqueria"
    Public Const CAMPO_ESTADO_PELUQUERIA As String = "estadoPeluqueria"
    ''' <summary>Nesto#644 (5): el Codigo del 409 de la API (ReglaClienteComercialCambiado.CODIGO).</summary>
    Public Const CODIGO_CLIENTE_CAMBIADO As String = "CLIENTE_CAMBIADO"

    ''' <summary>
    ''' Nesto#644 (5): si la respuesta es el 409 CLIENTE_CAMBIADO de la API, los campos que chocan (details.campos; lista vacía
    ''' si no los trae). Nothing si es cualquier otra cosa.
    ''' </summary>
    Public Function CamposEnConflicto(status As Net.HttpStatusCode, cuerpo As String) As List(Of String)
        If status <> Net.HttpStatusCode.Conflict OrElse String.IsNullOrWhiteSpace(cuerpo) Then
            Return Nothing
        End If
        Try
            Dim json As JObject = JObject.Parse(cuerpo)
            If Not String.Equals(json("Codigo")?.ToString(), CODIGO_CLIENTE_CAMBIADO, StringComparison.OrdinalIgnoreCase) Then
                Return Nothing
            End If
            Dim campos As JArray = TryCast(json("details")?("campos"), JArray)
            Return If(campos Is Nothing, New List(Of String), campos.Select(Function(c) c.ToString()).ToList())
        Catch
            Return Nothing
        End Try
    End Function

    Private Function FilaPeluqueria(grupo As String, vendedor As String, estado As Short, usuario As String) As JArray
        Return New JArray(New JObject(
            New JProperty("grupoProducto", grupo),
            New JProperty("vendedor", vendedor?.Trim()),
            New JProperty("estado", estado),
            New JProperty("usuario", usuario)))
    End Function

    Private Function MismoTexto(a As String, b As String) As Boolean
        Return String.Equals(If(a, String.Empty).Trim(), If(b, String.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
    End Function
End Module
