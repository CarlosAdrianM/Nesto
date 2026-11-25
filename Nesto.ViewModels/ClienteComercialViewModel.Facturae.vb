Imports System.Windows.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared

''' <summary>
''' Nesto#652 (NestoAPI#903): «Descargar para FACe…» en la pestaña «Facturas» de la ficha, junto a «Descargar» y «Enviar por
''' correo», para UNA factura marcada. Abre el mismo diálogo que el detalle del pedido. Solo Administración, Dirección e Informática.
''' </summary>
Partial Public Class ClienteComercialViewModel

    Public ReadOnly Property PuedeDescargarFacturae As Boolean
        Get
            Return configuracion IsNot Nothing AndAlso
                (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) OrElse
                 configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
                 configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA))
        End Get
    End Property

    Private _descargarFacturaeCommand As ICommand
    Public ReadOnly Property DescargarFacturaeCommand As ICommand
        Get
            If _descargarFacturaeCommand Is Nothing Then
                _descargarFacturaeCommand = ComandosReevaluables.Crear(AddressOf OnDescargarFacturae, AddressOf CanDescargarFacturae)
            End If
            Return _descargarFacturaeCommand
        End Get
    End Property

    ''' <summary>Una sola factura marcada (el fichero es de una factura) y de los grupos que pueden.</summary>
    Friend Function CanDescargarFacturae(param As Object) As Boolean
        Return PuedeDescargarFacturae AndAlso FacturasSeleccionadas IsNot Nothing AndAlso FacturasSeleccionadas.Count = 1 AndAlso
            Not String.IsNullOrWhiteSpace(FacturasSeleccionadas(0).Documento)
    End Function

    Private Async Sub OnDescargarFacturae(param As Object)
        Await DescargarFacturaeAsync()
    End Sub

    Friend Async Function DescargarFacturaeAsync() As Task
        If Not CanDescargarFacturae(Nothing) Then
            Return
        End If
        Dim factura = FacturasSeleccionadas(0)
        Dim unused As ResultadoDialogo = Await dialogService.ShowDialogAsync(FacturaeDialogViewModel.NOMBRE, New ParametrosDialogo From {
            {FacturaeDialogViewModel.PARAMETRO_EMPRESA, factura.Empresa?.Trim()},
            {FacturaeDialogViewModel.PARAMETRO_FACTURA, factura.Documento?.Trim()}
        })
    End Function
End Class
