Imports System.Collections.ObjectModel
Imports System.Data.Objects
Imports System.Transactions
Imports Nesto.Contratos
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

Public Class AgenciaService
    Implements IAgenciaService

    Public Sub Modificar(envio As EnviosAgencia) Implements IAgenciaService.Modificar
        Using context As New NestoEntities
            context.EnviosAgencia.Attach(envio)
            context.Entry(envio).State = EntityState.Modified
            context.SaveChanges()
        End Using
    End Sub

    Public Sub Borrar(Id As Integer) Implements IAgenciaService.Borrar
        Using DbContext As New NestoEntities
            Dim historias As List(Of EnviosHistoria) = (From h In DbContext.EnviosHistoria Where h.NumeroEnvio = Id).ToList
            For Each historia In historias
                DbContext.EnviosHistoria.Remove(historia)
            Next
            Dim envioActual = DbContext.EnviosAgencia.Single(Function(e) e.Numero = Id)
            DbContext.EnviosAgencia.Remove(envioActual)
            DbContext.SaveChanges()
        End Using
    End Sub

    Public Function CargarListaPendientes() As IEnumerable(Of EnvioAgenciaWrapper) Implements IAgenciaService.CargarListaPendientes
        Using contexto = New NestoEntities()
            Dim lista As List(Of EnviosAgencia) = contexto.EnviosAgencia.Where(Function(e) e.Estado < 0).ToList
            Dim listaWrapper As List(Of EnvioAgenciaWrapper) = New List(Of EnvioAgenciaWrapper)
            For Each envio In lista
                listaWrapper.Add(EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envio))
            Next
            Return listaWrapper
        End Using
    End Function

    Public Function GetEnvioById(Id As Integer) As EnviosAgencia Implements IAgenciaService.GetEnvioById
        Using contexto = New NestoEntities
            Return contexto.EnviosAgencia.Include("Empresas").Single(Function(e) e.Numero = Id)
        End Using
    End Function

    Public Function Insertar(envio As EnviosAgencia) As EnviosAgencia Implements IAgenciaService.Insertar
        Using contexto As New NestoEntities
            contexto.EnviosAgencia.Add(envio)
            contexto.SaveChanges()
            contexto.Entry(envio).Reference(Function(e) e.AgenciasTransporte).Load()
            contexto.Entry(envio).Reference(Function(e) e.Empresas).Load()
        End Using
    End Function

    Public Function CargarPedido(empresa As String, numeroPedido As Integer?) As CabPedidoVta Implements IAgenciaService.CargarPedido
        Using contexto = New NestoEntities
            Return contexto.CabPedidoVta.Include("Clientes").Include("Clientes.PersonasContactoCliente").SingleOrDefault(Function(p) p.Empresa = empresa AndAlso p.Número = numeroPedido)
        End Using
    End Function

    Public Function CargarListaReembolsos(empresa As String, agencia As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaReembolsos
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of EnviosAgencia)(From e In contexto.EnviosAgencia Where e.Empresa = empresa And e.Agencia = agencia And e.Estado >= Constantes.Agencias.ESTADO_TRAMITADO_ENVIO And e.Reembolso <> 0 And e.FechaPagoReembolso Is Nothing)
        End Using
    End Function

    Public Function CargarListaRetornos(empresa As String, agencia As Integer, tipoDeRetornoExcluido As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaRetornos
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of EnviosAgencia)(From e In contexto.EnviosAgencia Where e.Empresa = empresa And e.Agencia = agencia And e.Estado >= Constantes.Agencias.ESTADO_TRAMITADO_ENVIO And e.Retorno <> tipoDeRetornoExcluido And e.FechaRetornoRecibido Is Nothing Order By e.Fecha)
        End Using
    End Function

    Private Function CargarListaEnviosTramitadosGenerica(contexto As NestoEntities, empresa As String) As IQueryable(Of EnviosAgencia)
        Return From e In contexto.EnviosAgencia.Include("AgenciasTransporte") Where e.Empresa = empresa And e.Estado = Constantes.Agencias.ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending
    End Function
    Public Function CargarListaEnviosTramitados(empresa As String, agencia As Integer, fechaFiltro As Date) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosTramitados
        Using contexto = New NestoEntities
            Dim respuestaGenerica = CargarListaEnviosTramitadosGenerica(contexto, empresa)
            Dim respuesta = New ObservableCollection(Of EnviosAgencia)(From e In respuestaGenerica Where e.Agencia = agencia AndAlso e.Fecha = fechaFiltro)
            Return respuesta
        End Using
    End Function

    Public Function CargarListaEnviosTramitadosPorFecha(empresa As String, fechaFiltro As Date) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosTramitadosPorFecha
        Using contexto = New NestoEntities
            Dim respuestaGenerica = CargarListaEnviosTramitadosGenerica(contexto, empresa)
            Dim respuesta = New ObservableCollection(Of EnviosAgencia)(From e In respuestaGenerica Where e.Fecha = fechaFiltro)
            Return respuesta
        End Using
    End Function

    Public Function CargarListaEnvios(agencia As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnvios
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of EnviosAgencia)(From e In contexto.EnviosAgencia.Include("AgenciasTransporte") Where e.Agencia = agencia And e.Estado = Constantes.Agencias.ESTADO_INICIAL_ENVIO Order By e.Numero)
        End Using
    End Function

    Public Function CargarListaEnviosTramitadosPorCliente(empresa As String, clienteFiltro As String) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosTramitadosPorCliente
        Using contexto = New NestoEntities
            Dim respuestaGenerica = CargarListaEnviosTramitadosGenerica(contexto, empresa)
            Dim respuesta = New ObservableCollection(Of EnviosAgencia)(From e In respuestaGenerica Where e.Cliente = clienteFiltro)
            Return respuesta
        End Using
    End Function

    Public Function CargarListaEnviosTramitadosPorNombre(empresa As String, nombreFiltro As String) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosTramitadosPorNombre
        Using contexto = New NestoEntities
            Dim respuestaGenerica = CargarListaEnviosTramitadosGenerica(contexto, empresa)
            Dim respuesta = New ObservableCollection(Of EnviosAgencia)(From e In respuestaGenerica Where e.Clientes.Nombre.Contains(nombreFiltro))
            Return respuesta
        End Using
    End Function

    Public Function CargarListaAgencias(empresa As String) As ObservableCollection(Of AgenciasTransporte) Implements IAgenciaService.CargarListaAgencias
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of AgenciasTransporte)(From c In contexto.AgenciasTransporte Where c.Empresa = empresa)
        End Using
    End Function

    Public Function CargarListaEnviosPedido(empresa As String, pedido As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosPedido
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of EnviosAgencia)(From e In contexto.EnviosAgencia.Include("AgenciasTransporte") Where e.Empresa = empresa AndAlso e.Pedido = pedido Order By e.Numero)
        End Using
    End Function

    Public Function CargarAgencia(agencia As Integer) As AgenciasTransporte Implements IAgenciaService.CargarAgencia
        Using contexto = New NestoEntities
            Return contexto.AgenciasTransporte.Where(Function(a) a.Numero = agencia).SingleOrDefault
        End Using
    End Function

    Public Function CargarListaHistoriaEnvio(envio As Integer) As ObservableCollection(Of EnviosHistoria) Implements IAgenciaService.CargarListaHistoriaEnvio
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of EnviosHistoria)(From h In contexto.EnviosHistoria Where h.NumeroEnvio = envio)
        End Using
    End Function

    Public Function CargarPedidoPorNumero(pedido As Integer) As CabPedidoVta Implements IAgenciaService.CargarPedidoPorNumero
        Using contexto = New NestoEntities
            Return (From c In contexto.CabPedidoVta.Include("Clientes").Include("Clientes.PersonasContactoCliente") Where c.Número = pedido).FirstOrDefault
        End Using
    End Function

    Public Function CargarPedidoPorNumero(pedido As Integer, espejo As Boolean) As CabPedidoVta Implements IAgenciaService.CargarPedidoPorNumero
        If espejo Then
            Return CargarPedidoPorNumero(pedido)
        Else
            Using contexto = New NestoEntities
                Return contexto.CabPedidoVta.Include("Clientes").Include("Clientes.PersonasContactoCliente").FirstOrDefault(Function(c) c.Número = pedido And c.Empresa <> Constantes.Empresas.EMPRESA_ESPEJO)
            End Using
        End If
    End Function

    Public Function CargarPedidoPorFactura(numeroPedido As String) As CabPedidoVta Implements IAgenciaService.CargarPedidoPorFactura
        Using contexto = New NestoEntities
            'Return (From c In contexto.CabPedidoVta Join l In contexto.LinPedidoVta On c.Empresa Equals l.Empresa And c.Número Equals l.Número Where l.Nº_Factura = numeroPedido Select c).FirstOrDefault
            Dim pedido = contexto.LinPedidoVta.Where(Function(l) l.Nº_Factura = numeroPedido).Select(Function(l) l.Número).FirstOrDefault
            Return contexto.CabPedidoVta.Include("Clientes").Include("Clientes.PersonasContactoCliente").Where(Function(c) c.Número = pedido).FirstOrDefault
        End Using
    End Function

    Public Function CargarClientePorUnDato(empresa As String, datoABuscar As String) As Clientes Implements IAgenciaService.CargarClientePorUnDato
        Using contexto = New NestoEntities
            Return (From c In contexto.Clientes Where c.Empresa = empresa AndAlso (c.Nombre.Contains(datoABuscar) OrElse c.Dirección.Contains(datoABuscar) OrElse c.Teléfono.Contains(datoABuscar))).FirstOrDefault
        End Using
    End Function

    Public Function CargarMultiusuario(empresa As String, multiusuario As Integer) As MultiUsuarios Implements IAgenciaService.CargarMultiusuario
        Using contexto = New NestoEntities
            Return (From m In contexto.MultiUsuarios Where m.Empresa = empresa And m.Número = multiusuario).FirstOrDefault
        End Using
    End Function

    Public Function CalcularSumaContabilidad(empresa As String, cuentaReembolsos As String) As Double? Implements IAgenciaService.CalcularSumaContabilidad
        Using contexto = New NestoEntities
            Return (Aggregate c In contexto.Contabilidad Where c.Empresa = empresa And c.Nº_Cuenta = cuentaReembolsos Into Sum(CType(c.Debe, Double?) - CType(c.Haber, Double?)))
        End Using
    End Function

    Public Function CargarListaEmpresas() As ObservableCollection(Of Empresas) Implements IAgenciaService.CargarListaEmpresas
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of Empresas)(From c In contexto.Empresas)
        End Using
    End Function

    Public Function CargarClientePrincipal(empresa As String, cliente As String) As Clientes Implements IAgenciaService.CargarClientePrincipal
        Using contexto = New NestoEntities
            Return (From c In contexto.Clientes Where c.Empresa = empresa And c.Nº_Cliente = cliente And c.ClientePrincipal = True And c.Estado >= Constantes.Clientes.ESTADO_NORMAL).FirstOrDefault
        End Using
    End Function

    Public Function CargarLineasPedidoPendientes(pedido As Integer) As List(Of LinPedidoVta) Implements IAgenciaService.CargarLineasPedidoPendientes
        Using contexto = New NestoEntities
            Return (From l In contexto.LinPedidoVta Where l.Número = pedido And l.Estado = Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE).ToList
        End Using
    End Function

    Public Function CargarLineasPedidoSinPicking(pedido As Integer) As List(Of LinPedidoVta) Implements IAgenciaService.CargarLineasPedidoSinPicking
        Using contexto = New NestoEntities
            Return (From l In contexto.LinPedidoVta Where l.Número = pedido And l.Picking <> 0 And l.Estado = Constantes.LineasPedido.ESTADO_SIN_FACTURAR).ToList
        End Using
    End Function

    Public Function HayAlgunaLineaConPicking(empresa As String, pedido As Integer) As Boolean Implements IAgenciaService.HayAlgunaLineaConPicking
        Using contexto = New NestoEntities
            Dim lineaConPicking = contexto.LinPedidoVta.FirstOrDefault(Function(l) l.Empresa = empresa AndAlso l.Número = pedido AndAlso l.Estado <= Constantes.LineasPedido.ESTADO_SIN_FACTURAR AndAlso l.Estado >= Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE AndAlso l.Picking <> 0)
            Return Not IsNothing(lineaConPicking)
        End Using
    End Function

    Public Function CargarAgenciaPorNombreYCuentaReembolsos(empresa As String, cuentaReembolsos As String, nombreAgencia As String) As AgenciasTransporte Implements IAgenciaService.CargarAgenciaPorNombreYCuentaReembolsos
        Using contexto = New NestoEntities
            Return contexto.AgenciasTransporte.SingleOrDefault(Function(a) a.Empresa = empresa AndAlso a.CuentaReembolsos = cuentaReembolsos AndAlso a.Nombre = nombreAgencia)
        End Using
    End Function

    Public Function CargarEnvio(empresa As String, pedido As Integer) As EnviosAgencia Implements IAgenciaService.CargarEnvio
        Using contexto = New NestoEntities
            Dim respuesta As EnviosAgencia = contexto.EnviosAgencia.Include("AgenciasTransporte").FirstOrDefault(Function(e) e.Estado < Constantes.Agencias.ESTADO_INICIAL_ENVIO AndAlso e.Empresa = empresa AndAlso e.Pedido = pedido)
            contexto.Entry(respuesta).Reference(Function(e) e.Empresas).Load()
            Return respuesta
        End Using
    End Function

    Public Function CargarExtractoCliente(empresa As String, cliente As String, positivos As Boolean) As ObservableCollection(Of ExtractoCliente) Implements IAgenciaService.CargarExtractoCliente
        Using contexto = New NestoEntities
            If positivos Then
                Return New ObservableCollection(Of ExtractoCliente)(From e In contexto.ExtractoCliente Where e.Empresa = empresa And e.Número = cliente And e.ImportePdte > 0 And (e.Estado = "NRM" Or e.Estado Is Nothing))
            Else
                Return New ObservableCollection(Of ExtractoCliente)(From e In contexto.ExtractoCliente Where e.Empresa = empresa And e.Número = cliente And e.ImportePdte < 0 And (e.Estado = "NRM" Or e.Estado Is Nothing))
            End If
        End Using
    End Function

    Public Function CargarPagoExtractoClientePorEnvio(envio As EnviosAgencia, concepto As String, importeAnterior As Double) As ObservableCollection(Of ExtractoCliente) Implements IAgenciaService.CargarPagoExtractoClientePorEnvio
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of ExtractoCliente)(From e In contexto.ExtractoCliente Where e.Empresa = envio.Empresa And
                                                                    e.Número = envio.Cliente And e.Contacto = envio.Contacto And e.Fecha = envio.Fecha And e.TipoApunte = 3 And e.Concepto = concepto And
                                                                    e.Importe = -importeAnterior)
        End Using
    End Function

    Public Function CargarAgenciaPorRuta(empresa As String, ruta As String) As AgenciasTransporte Implements IAgenciaService.CargarAgenciaPorRuta
        Using contexto = New NestoEntities
            Return contexto.AgenciasTransporte.FirstOrDefault(Function(a) a.Empresa = empresa AndAlso a.Ruta = ruta)
        End Using
    End Function

    Public Function CargarCliente(empresa As String, cliente As String, contacto As String) As Clientes Implements IAgenciaService.CargarCliente
        Using contexto = New NestoEntities
            Return contexto.Clientes.Single(Function(c) c.Empresa = empresa AndAlso c.Nº_Cliente = cliente AndAlso c.Contacto = contacto)
        End Using
    End Function

    Public Function CargarEnvioPorClienteYDireccion(cliente As String, contacto As String, direccion As String) As EnviosAgencia Implements IAgenciaService.CargarEnvioPorClienteYDireccion
        Using contexto = New NestoEntities
            Return (From e In contexto.EnviosAgencia.Include("AgenciasTransporte") Where e.Cliente = cliente And e.Contacto = contacto And e.Direccion = direccion And e.Estado = Constantes.Agencias.ESTADO_INICIAL_ENVIO).FirstOrDefault
        End Using
    End Function

    Public Function CargarDeudasCliente(cliente As String, fechaReclamar As Date) As List(Of ExtractoCliente) Implements IAgenciaService.CargarDeudasCliente
        Using contexto = New NestoEntities
            Return (From e In contexto.ExtractoCliente Where e.Número = cliente AndAlso
                e.ImportePdte <> 0 AndAlso
                (e.Estado Is Nothing Or (e.Estado <> "RTN" And e.Estado <> "RHS")) AndAlso
                (e.FormaPago <> "TRN") AndAlso
                (e.Ruta Is Nothing Or e.Ruta <> "RG") AndAlso
                e.FechaVto < fechaReclamar AndAlso
                e.TipoApunte <> "4").ToList
        End Using

    End Function

    Public Function TramitarEnvio(envio As EnviosAgencia) As String Implements IAgenciaService.TramitarEnvio
        Dim success As Boolean = False

        Using transaction As New TransactionScope()
            Using DbContext As New NestoEntities
                Dim asiento As Integer = 0

                Dim envioEncontrado = DbContext.EnviosAgencia.Where(Function(e) e.Numero = envio.Numero).Single

                envioEncontrado.Estado = Constantes.Agencias.ESTADO_TRAMITADO_ENVIO 'Enviado
                envioEncontrado.Fecha = Today
                envioEncontrado.FechaEntrega = Today.AddDays(1) 'Se entrega al día siguiente
                success = DbContext.SaveChanges()

                If success AndAlso envio.Reembolso <> 0 Then
                    asiento = ContabilizarReembolso(envioEncontrado)
                    If asiento <= 0 Then
                        success = False
                    End If
                End If

                If success Then
                    transaction.Complete()
                    DbContext.SaveChanges()
                    Return "Envío del pedido " + envio.Pedido.ToString + " tramitado correctamente."
                Else
                    transaction.Dispose()
                    Return "Error al tramitar pedido " + envio.Pedido.ToString + "."
                End If
            End Using ' Cerramos el contexto
        End Using ' Cerramos la transaccion
    End Function


    Public Function ContabilizarReembolso(envio As EnviosAgencia) As Integer Implements IAgenciaService.ContabilizarReembolso

        If IsNothing(envio.AgenciasTransporte.CuentaReembolsos) Then
            Throw New Exception("Esta agencia no tiene establecida una cuenta de reembolsos. No se puede contabilizar.")
            Return -1
        End If

        Dim lineaInsertar As New PreContabilidad
        Dim movimientoLiq As ExtractoCliente
        movimientoLiq = CalcularMovimientoLiq(envio)


        With lineaInsertar
            .Empresa = envio.Empresa.Trim
            .Diario = Constantes.DiariosContables.DIARIO_REEMBOLSOS
            .TipoApunte = "3" 'Pago
            .TipoCuenta = "2" 'Cliente
            .Nº_Cuenta = envio.Cliente.Trim
            .Contacto = envio.Contacto.Trim
            .Fecha = Today 'envio.Fecha
            .FechaVto = Today ' envio.Fecha
            .Haber = envio.Reembolso
            .Concepto = GenerarConcepto(envio)
            .Contrapartida = envio.AgenciasTransporte.CuentaReembolsos.Trim
            .Asiento_Automático = False
            .FormaPago = envio.Empresas.FormaPagoEfectivo
            .Vendedor = envio.Vendedor
            If IsNothing(movimientoLiq) Then
                .Nº_Documento = envio.Pedido
                .Delegación = envio.Empresas.DelegaciónVarios
                .FormaVenta = envio.Empresas.FormaVentaVarios
            Else
                .Nº_Documento = movimientoLiq.Nº_Documento
                .Liquidado = movimientoLiq.Nº_Orden
                .Delegación = movimientoLiq.Delegación
                .FormaVenta = movimientoLiq.FormaVenta
                .Ruta = movimientoLiq.Ruta
                .Efecto = movimientoLiq.Efecto
            End If
        End With

        Dim asiento As Integer

        Using transaction As New TransactionScope()
            Using DbContext As New NestoEntities
                ' Iniciamos transacción
                Dim success As Boolean

                Try
                    DbContext.PreContabilidad.Add(lineaInsertar)
                    DbContext.SaveChanges()
                    asiento = DbContext.prdContabilizar(lineaInsertar.Empresa, Constantes.DiariosContables.DIARIO_REEMBOLSOS)
                    transaction.Complete()
                    success = asiento > 0
                Catch e As Exception
                    transaction.Dispose()
                    Return -1
                End Try

                ' Comprobamos que las transacciones sean correctas
                If success Then
                    ' Reset the context since the operation succeeded. 
                    DbContext.SaveChanges()
                Else
                    Throw New Exception("Se ha producido un error y no se grabado los datos")
                End If
            End Using ' cerramos el contexto
        End Using 'cerramos la transcacción


        Return asiento

    End Function

    Private Function CalcularMovimientoLiq(env As EnviosAgencia) As ExtractoCliente Implements IAgenciaService.CalcularMovimientoLiq
        Return CalcularMovimientoLiq(env, env.Reembolso)
    End Function
    Private Function CalcularMovimientoLiq(env As EnviosAgencia, reembolsoAnterior As Double) As ExtractoCliente Implements IAgenciaService.CalcularMovimientoLiq
        Dim movimientos As ObservableCollection(Of ExtractoCliente)
        Dim movimientosConImporte As ObservableCollection(Of ExtractoCliente)

        If reembolsoAnterior > 0 Then
            movimientos = CargarExtractoCliente(env.Empresa, env.Cliente, True)
        Else
            movimientos = CargarExtractoCliente(env.Empresa, env.Cliente, False)
        End If


        If movimientos.Count = 0 Then
            Return Nothing
        ElseIf movimientos.Count = 1 Then
            Return movimientos.SingleOrDefault
        Else
            If reembolsoAnterior > 0 Then
                movimientosConImporte = New ObservableCollection(Of ExtractoCliente)(From m In movimientos Where m.ImportePdte = reembolsoAnterior)
            Else
                movimientosConImporte = New ObservableCollection(Of ExtractoCliente)(From m In movimientos Where m.ImportePdte = env.Reembolso And m.Fecha = Today) ' con env.Fecha hay problemas cuando la etiqueta es del día anterior
            End If

            If movimientosConImporte.Count = 0 Then
                Return movimientos.FirstOrDefault
            Else
                Return movimientosConImporte.FirstOrDefault
            End If
        End If
    End Function
    Private Function GenerarConcepto(envio As EnviosAgencia) As String Implements IAgenciaService.GenerarConcepto
        Dim agenciaEnvio As AgenciasTransporte = CargarAgencia(envio.Agencia)
        Return Left("S/Pago pedido " + envio.Pedido.ToString + " a " + agenciaEnvio.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
    End Function
End Class
