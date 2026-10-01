''' <summary>
''' El picking no ha sacado nada porque el cliente cierra el día de la entrega (NestoAPI, error
''' PICKING_CLIENTE_CERRADO). En el picking de UN pedido, Nesto pregunta «¿Aún así quieres asignarle picking?»
''' y, si se confirma, lo vuelve a pedir ignorando el cierre. Caso real 01/10/26: Alfredo, cliente 5057.
''' </summary>
Public Class PickingClienteCerradoException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "PICKING_CLIENTE_CERRADO"

    Public Sub New(mensaje As String)
        MyBase.New(mensaje)
    End Sub
End Class
