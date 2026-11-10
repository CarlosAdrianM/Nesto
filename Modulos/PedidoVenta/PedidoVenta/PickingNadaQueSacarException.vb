''' <summary>
''' En un picking de varios pedidos (rutas, nuestra ruta, cliente) no sale ninguno (NestoAPI, error
''' PICKING_NADA_QUE_SACAR, commit 9b791d66): «No hay ningún pedido que pueda salir ahora.» y el resumen por motivo.
''' No es un fallo: Nesto lo enseña como aviso, con el mensaje tal cual (Novedades 1164).
''' </summary>
Public Class PickingNadaQueSacarException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "PICKING_NADA_QUE_SACAR"

    Public Sub New(mensaje As String)
        MyBase.New(mensaje)
    End Sub
End Class
