''' <summary>
''' Nesto#340 (slice A3): la respuesta de GET api/PedidosVenta/ParaAgencia/SituacionLineas.
'''
''' Sustituye a las dos consultas que AgenciaService hacía a LinPedidoVta con Entity Framework.
''' No trae ni una línea: solo las dos respuestas, calculadas en el servidor.
''' </summary>
Public Class SituacionLineasPedidoModel

    ''' <summary>
    ''' Alguna línea viva (estado entre -1 y 1) tiene picking. Agencias lo usa en negativo: si no
    ''' hay ninguna, pregunta al usuario si quiere insertar el envío de todos modos.
    ''' </summary>
    Public Property TieneAlgunaLineaConPicking As Boolean

    ''' <summary>
    ''' Todas las líneas son de canal externo (WEB, STK, QRU, BLT). Un pedido SIN líneas cuenta
    ''' como todo online, igual que antes: es lo que devolvía el All sobre una lista vacía.
    ''' </summary>
    Public Property EsTodoOnline As Boolean

End Class
