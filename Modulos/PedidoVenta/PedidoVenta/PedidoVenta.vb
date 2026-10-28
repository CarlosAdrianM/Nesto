Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Services
Imports Nesto.Infrastructure.Contracts

Public Class PedidoVenta
    Implements IModuloNesto, IPedidoVenta

    Public Sub Registrar(registro As IRegistroNesto) Implements IModuloNesto.Registrar
        registro.Vista(Of PedidoVentaView)("PedidoVentaView")
        registro.Vista(Of DetallePedidoView)("DetallePedidoView")
        registro.RegistrarDialogo(Of PickingPopupView, PickingPopupViewModel)
        ' Sugerencia 575: pedidos que podrían ir por nuestra ruta, antes de sacar el picking
        registro.RegistrarDialogo(Of SugerenciasRutaPickingPopup, SugerenciasRutaPickingPopupViewModel)(SugerenciasRutaPickingPopupViewModel.NOMBRE)
        registro.RegistrarDialogo(Of FacturarRutasPopup, FacturarRutasPopupViewModel)
        registro.RegistrarDialogo(Of ErroresFacturacionRutasPopup, ErroresFacturacionRutasPopupViewModel)
        ' NestoAPI#727: facturar el fin de mes por la API (sustituye al Nesto viejo)
        registro.RegistrarDialogo(Of FacturarFinDeMesDialog, FacturarFinDeMesDialogViewModel)(FacturarFinDeMesDialogViewModel.NOMBRE)
        registro.RegistrarDialogo(Of CopiarFacturaView, CopiarFacturaViewModel)
        ' NestoAPI#352: pregunta el grupo de comisión de las líneas de inmovilizado al guardar
        registro.RegistrarDialogo(Of SelectorGrupoComisionDialog, SelectorGrupoComisionDialogViewModel)
        ' NestoAPI#698: motivo y fecha al retener un pedido hasta nuevo aviso
        registro.RegistrarDialogo(Of RetenerPedidoDialog, RetenerPedidoDialogViewModel)(RetenerPedidoDialogViewModel.NOMBRE)

        ' Registrar servicios del módulo
        registro.Singleton(Of Services.IServicioFacturacionRutas, Services.ServicioFacturacionRutas)()
        registro.Singleton(Of IServicioImpresionDocumentos, ServicioImpresionDocumentos)()

        ' Issue #263: Servicios de autocomplete
        registro.Transitorio(Of ServicioBusquedaProductos)()
        registro.Transitorio(Of ServicioBusquedaCuentas)()

        ' Nesto#490 (4C.5): lo que Unity construía sin registrar. PedidoVentaView y ListaPedidosVenta también se piden
        ' por su tipo (PedidoVentaViewModel.CargarPedido y el maestro-detalle de PedidoVentaView).
        registro.Transitorio(Of PedidoVentaMenuBar)()
        registro.Transitorio(Of PedidoVentaViewModel)()
        registro.Transitorio(Of PedidoVentaView)()
        registro.Transitorio(Of ListaPedidosVenta)()
        ' Los ViewModels de sus vistas
        registro.Transitorio(Of ListaPedidosVentaViewModel)()
        registro.Transitorio(Of DetallePedidoViewModel)()
        ' Nesto#552: «Pendientes de entregar» (Clientes > Ventas en la cinta)
        registro.Transitorio(Of PendientesEntregarView)()
        registro.Transitorio(Of PendientesEntregarViewModel)()
    End Sub

    Public Sub Inicializar(resolutor As IResolutorNesto) Implements IModuloNesto.Inicializar
        Dim view = resolutor.Resolver(Of PedidoVentaMenuBar)()
        If Not IsNothing(view) Then
            resolutor.Resolver(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
