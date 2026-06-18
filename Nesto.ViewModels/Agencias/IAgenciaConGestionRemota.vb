Imports System.Threading.Tasks
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

''' <summary>
''' Tipo de flujo de tramitación de una agencia. Lo declara CADA agencia (propiedad
''' <c>FlujoTramitacion</c> de <see cref="IAgencia"/>): las actuales = <see cref="TramitarAlCerrar"/>,
''' Innovatrans = <see cref="RegistrarAlImprimir"/>.
''' </summary>
Public Enum TipoFlujoTramitacion
    ''' <summary>
    ''' Flujo clásico (CEX, ASM, Sending, GLS, Canteras...): nosotros somos dueños de la
    ''' numeración, montamos e imprimimos la etiqueta en local, el envío vive en "En curso"
    ''' (estado 0) sin estar en la agencia, y "Tramitar todos" es lo que lo envía a la agencia.
    ''' Reimprimir, modificar bultos/reembolso o borrar antes de tramitar es SOLO en nuestra BD.
    ''' </summary>
    TramitarAlCerrar = 0

    ''' <summary>
    ''' Flujo "registrar al imprimir" (Innovatrans / DataTrans DTX): la agencia asigna el
    ''' albarán y devuelve la etiqueta (ZPL). No podemos auto-numerar ni montar la etiqueta.
    ''' Por tanto, poner el envío en "En curso" (estado 0) ES insertarlo en la agencia e
    ''' imprimirlo a la vez. A partir de ahí, cualquier modificación/anulación tiene que ir
    ''' TAMBIÉN a la agencia (no basta con la BD), y "Tramitar todos" pasa a ser "cerrar el
    ''' día / pedir recogida" (ya no envía datos, solo bloquea la edición).
    ''' </summary>
    RegistrarAlImprimir = 1
End Enum

''' <summary>
''' Operaciones extra de las agencias cuyo <see cref="IAgencia.FlujoTramitacion"/> es
''' <see cref="TipoFlujoTramitacion.RegistrarAlImprimir"/> (la plataforma asigna el albarán y
''' sirve la etiqueta). Las agencias clásicas NO implementan esta interfaz: el ViewModel decide
''' el flujo con <c>agencia.FlujoTramitacion</c> y, cuando toca llamar a una de estas operaciones,
''' hace TryCast(agencia, IAgenciaConGestionRemota).
'''
''' Reglas de consistencia (nuestra BD y la API no deben divergir):
'''  - Idempotencia: si el envío YA tiene albarán (CodigoBarras de la respuesta), "imprimir"
'''    NO reinserta -> usa <see cref="Reimprimir"/>. Insertar dos veces = envío fantasma + cobro doble.
'''  - El insert puede ser RECHAZADO por la agencia (CP, tipoServ, campos): si falla, el envío se
'''    queda editable (no a medias) y NO pasa a "En curso".
'''  - Modificar/Anular: primero la API, luego la BD. Si la API falla, NO tocar la BD (marcar
'''    desincronizado y avisar), nunca dejar BD y agencia divergentes en silencio.
'''  - Cambiar el nº de bultos obliga a REIMPRIMIR TODAS (vienen numeradas 1/n, 2/n... y el total
'''    cambia), no solo el bulto nuevo.
''' </summary>
Public Interface IAgenciaConGestionRemota
    Inherits IAgencia

    ''' <summary>
    ''' Inserta el envío en la agencia, guarda el albarán devuelto en el envío (CodigoBarras) e
    ''' imprime la etiqueta (ZPL). Es lo que ocurre al pasar el envío a "En curso". Devuelve
    ''' Exito=False con el motivo si la agencia rechaza el insert.
    ''' </summary>
    Function InsertarYEtiquetar(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia)

    ''' <summary>
    ''' Reimprime la etiqueta de un envío YA insertado, sin volver a insertarlo (mismo albarán).
    ''' bultoDesde/bultoHasta opcionales (0 = todos los bultos). Para errores físicos de impresión.
    ''' </summary>
    Function Reimprimir(envio As EnviosAgencia, servicio As IAgenciaService, bultoDesde As Integer, bultoHasta As Integer) As Task(Of RespuestaAgencia)

    ''' <summary>
    ''' Aplica en la agencia un cambio de datos (bultos, reembolso, dirección...) de un envío ya
    ''' insertado. El llamante debe persistir en BD solo si Exito=True.
    ''' </summary>
    Function ModificarEnAgencia(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia)

    ''' <summary>
    ''' Anula/borra el envío en la agencia. El llamante borra de la BD solo si Exito=True.
    ''' </summary>
    Function AnularEnAgencia(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia)

End Interface
