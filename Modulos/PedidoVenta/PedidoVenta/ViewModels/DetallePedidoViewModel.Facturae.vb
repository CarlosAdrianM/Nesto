Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts

''' <summary>
''' Nesto#652 (NestoAPI#903): «Descargar para FACe (Facturae)…» en el menú de «Imprimir Factura» (clic derecho), junto a «Descargar
''' PDF» y «Enviar por correo…». Abre el diálogo que comprueba la factura de la línea seleccionada, deja escribir los DIR3 y las
''' referencias y guarda el XML para importarlo en B2Brouter. Solo Administración, Dirección e Informática (la API da 403 a los demás).
''' </summary>
Partial Public Class DetallePedidoViewModel

    Public Property DescargarFacturaeCommand As AsyncRelayCommand

    ''' <summary>Los mismos grupos que pueden poner el P.O. en una factura emitida.</summary>
    Public ReadOnly Property PuedeDescargarFacturae As Boolean
        Get
            Return PuedePonerSuPedidoFactura
        End Get
    End Property

    Private Function CanDescargarFacturae() As Boolean
        Return PuedeDescargarFacturae AndAlso CanImprimirFactura()
    End Function

    ''' <summary>Friend para los tests.</summary>
    Friend Async Function DescargarFacturaeAsync() As Task
        If Not CanDescargarFacturae() Then
            Return
        End If
        Dim unused As ResultadoDialogo = Await dialogService.ShowDialogAsync(FacturaeDialogViewModel.NOMBRE, New ParametrosDialogo From {
            {FacturaeDialogViewModel.PARAMETRO_EMPRESA, pedido.empresa?.Trim()},
            {FacturaeDialogViewModel.PARAMETRO_FACTURA, lineaActual.Factura?.Trim()}
        })
    End Function
End Class
