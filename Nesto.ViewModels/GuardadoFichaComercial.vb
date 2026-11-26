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
''' Lo que NO se puede sin la API: rechazar el guardado si lo que se cambia ya lo había cambiado otro (haría falta una versión
''' o el valor original, como NestoAPI#873 en los pedidos), ni cambiar solo el vendedor o solo el estado de peluquería (la API
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
                peticion.PayloadEstadoPeluqueria = segundo
            End If
        Else
            payload("VendedoresGrupoProducto") = JValue.CreateNull()
        End If

        peticion.HayCambios = cambiaEstado OrElse cambiaVendedor OrElse cambiaPais OrElse cambiaPeluqueria
        Return peticion
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
