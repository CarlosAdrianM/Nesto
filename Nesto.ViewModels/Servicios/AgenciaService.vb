﻿Imports System.Collections.ObjectModel
Imports System.Data.Entity
Imports System.Net.Http
Imports System.Text
Imports System.Transactions
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Newtonsoft.Json
Imports Prism.Services.Dialogs

Public Class AgenciaService
    Implements IAgenciaService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _dialogService As IDialogService

    Public Sub New(configuracion As IConfiguracion, dialogService As IDialogService)
        Me.configuracion = configuracion
        _dialogService = dialogService
    End Sub

    Public Sub Modificar(envio As EnviosAgencia) Implements IAgenciaService.Modificar
        Using context As New NestoEntities
            Dim unused1 = context.EnviosAgencia.Attach(envio)
            context.Entry(envio).State = EntityState.Modified
            Dim unused = context.SaveChanges()
        End Using
    End Sub

    Public Sub Borrar(Id As Integer) Implements IAgenciaService.Borrar
        Try
            Using DbContext As New NestoEntities
                Dim historias As List(Of EnviosHistoria) = (From h In DbContext.EnviosHistoria Where h.NumeroEnvio = Id).ToList
                For Each historia In historias
                    Dim unused2 = DbContext.EnviosHistoria.Remove(historia)
                Next
                Dim envioActual = DbContext.EnviosAgencia.Single(Function(e) e.Numero = Id)
                Dim unused1 = DbContext.EnviosAgencia.Remove(envioActual)
                Dim unused = DbContext.SaveChanges()
            End Using
        Catch ex As Exception
            _dialogService.ShowError(ex.Message)
        End Try
    End Sub

    Public Function CargarListaPendientes() As IEnumerable(Of EnvioAgenciaWrapper) Implements IAgenciaService.CargarListaPendientes
        Using contexto = New NestoEntities()
            Dim lista As List(Of EnviosAgencia) = contexto.EnviosAgencia.Where(Function(e) e.Estado < 0).ToList
            Dim listaWrapper As New List(Of EnvioAgenciaWrapper)
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
            Dim unused1 = contexto.EnviosAgencia.Add(envio)
            Dim unused = contexto.SaveChanges()
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
            Dim respuesta As New ObservableCollection(Of EnviosAgencia)(From e In contexto.EnviosAgencia.Include("AgenciasTransporte") Where e.Agencia = agencia And e.Estado = Constantes.Agencias.ESTADO_INICIAL_ENVIO Order By e.Numero)
            If Not IsNothing(respuesta) Then
                For Each envio In respuesta
                    contexto.Entry(envio).Reference(Function(e) e.Empresas).Load()
                Next
            End If
            Return respuesta
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
            Dim respuesta = New ObservableCollection(Of EnviosAgencia)(From e In respuestaGenerica Where e.Nombre.Contains(nombreFiltro) OrElse e.Direccion.Contains(nombreFiltro) OrElse e.Telefono.Contains(nombreFiltro) OrElse e.Movil.Contains(nombreFiltro))
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
            Dim fechaInicial As New Date(2019, 1, 1)
            Return Aggregate c In contexto.Contabilidad Where c.Empresa = empresa AndAlso c.Fecha >= fechaInicial AndAlso c.Nº_Cuenta = cuentaReembolsos Into Sum(c.Debe - CType(c.Haber, Double?))
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
            If Not IsNothing(respuesta) Then
                contexto.Entry(respuesta).Reference(Function(e) e.Empresas).Load()
            End If
            Return respuesta
        End Using
    End Function

    Public Function CargarExtractoCliente(empresa As String, cliente As String, positivos As Boolean) As ObservableCollection(Of ExtractoCliente) Implements IAgenciaService.CargarExtractoCliente
        Using contexto = New NestoEntities
            Return If(positivos,
                New ObservableCollection(Of ExtractoCliente)(From e In contexto.ExtractoCliente Where e.Empresa = empresa AndAlso e.Número = cliente AndAlso e.ImportePdte > 0 AndAlso (e.Estado = "NRM" OrElse e.Estado Is Nothing) AndAlso Not e.Nº_Documento.StartsWith(Constantes.Series.SERIE_CURSOS)),
                New ObservableCollection(Of ExtractoCliente)(From e In contexto.ExtractoCliente Where e.Empresa = empresa AndAlso e.Número = cliente AndAlso e.ImportePdte < 0 AndAlso (e.Estado = "NRM" OrElse e.Estado Is Nothing) AndAlso Not e.Nº_Documento.StartsWith(Constantes.Series.SERIE_CURSOS)))
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
            Return If(empresa.Trim = Constantes.Empresas.EMPRESA_DEFECTO,
                contexto.AgenciasTransporte.FirstOrDefault(Function(a) a.Empresa = empresa AndAlso a.Ruta = ruta),
                contexto.AgenciasTransporte.FirstOrDefault(Function(a) a.Empresa = empresa AndAlso a.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS))
        End Using
    End Function

    Public Function CargarCliente(empresa As String, cliente As String, contacto As String) As Clientes Implements IAgenciaService.CargarCliente
        Using contexto = New NestoEntities
            Return contexto.Clientes.Single(Function(c) c.Empresa = empresa AndAlso c.Nº_Cliente = cliente AndAlso c.Contacto = contacto)
        End Using
    End Function

    Public Function CargarEnvioPorClienteYDireccion(cliente As String, contacto As String, direccion As String) As EnviosAgencia Implements IAgenciaService.CargarEnvioPorClienteYDireccion
        Using contexto = New NestoEntities
            Dim respuesta = (From e In contexto.EnviosAgencia.Include("AgenciasTransporte") Where e.Cliente = cliente And e.Contacto = contacto And e.Direccion = direccion And e.Estado = Constantes.Agencias.ESTADO_INICIAL_ENVIO).FirstOrDefault
            If Not IsNothing(respuesta) Then
                contexto.Entry(respuesta).Reference(Function(e) e.Empresas).Load()
            End If
            Return respuesta
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
                    Dim unused = DbContext.SaveChanges()
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
                    Dim unused2 = DbContext.PreContabilidad.Add(lineaInsertar)
                    Dim unused1 = DbContext.SaveChanges()
                    asiento = DbContext.prdContabilizar(lineaInsertar.Empresa, Constantes.DiariosContables.DIARIO_REEMBOLSOS, configuracion.usuario)
                    transaction.Complete()
                    success = asiento > 0
                Catch e As Exception
                    transaction.Dispose()
                    Return -1
                End Try

                ' Comprobamos que las transacciones sean correctas
                If success Then
                    ' Reset the context since the operation succeeded. 
                    Dim unused = DbContext.SaveChanges()
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

        If env.Cliente.Trim = Constantes.Clientes.Especiales.AMAZON OrElse env.Cliente.Trim = Constantes.Clientes.Especiales.TIENDA_ONLINE Then
            Return Nothing
        End If

        movimientos = If(reembolsoAnterior > 0,
            CargarExtractoCliente(env.Empresa, env.Cliente, True),
            CargarExtractoCliente(env.Empresa, env.Cliente, False))


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

            Return If(movimientosConImporte.Count = 0, movimientos.LastOrDefault, movimientosConImporte.LastOrDefault)
        End If
    End Function
    Private Function GenerarConcepto(envio As EnviosAgencia) As String Implements IAgenciaService.GenerarConcepto
        Dim agenciaEnvio As AgenciasTransporte = CargarAgencia(envio.Agencia)
        Return Left("S/Pago pedido " + envio.Pedido.ToString + " a " + agenciaEnvio.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
    End Function

    Public Async Function EnviarCorreoEntregaAgencia(envioActual As EnvioAgenciaWrapper) As Task Implements IAgenciaService.EnviarCorreoEntregaAgencia
        Using client As New HttpClient
            Try
                client.BaseAddress = New Uri(configuracion.servidorAPI)
                Dim response As HttpResponseMessage
                Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(envioActual), Encoding.UTF8, "application/json")
                response = Await client.PostAsync("EnviosAgencias/EnviarCorreoEntregaAgencia", content)
            Catch ex As Exception
                Throw ex
            End Try
        End Using
    End Function

    Public Function EsTodoElPedidoOnline(empresa As String, pedido As Integer) As Boolean Implements IAgenciaService.EsTodoElPedidoOnline
        Using contexto = New NestoEntities
            Dim lineas = contexto.LinPedidoVta.Where(Function(l) l.Empresa = empresa AndAlso l.Número = pedido)
            Dim todoOnline = lineas.All(Function(l) Constantes.FormasVenta.FORMAS_ONLINE.Contains(l.Forma_Venta))
            Return todoOnline
        End Using
    End Function

    Public Async Function GuardarLlamadaAgencia(respuesta As RespuestaAgencia) As Task Implements IAgenciaService.GuardarLlamadaAgencia
        respuesta.Usuario = configuracion.usuario
        Using client As New HttpClient
            Try
                client.BaseAddress = New Uri(configuracion.servidorAPI)
                Dim response As HttpResponseMessage
                Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(respuesta), Encoding.UTF8, "application/json")
                response = Await client.PostAsync("AgenciasLlamadasWeb", content)
            Catch ex As Exception
                Throw ex
            End Try
        End Using
    End Function


    Public Async Function ImporteReembolso(empresa As String, pedido As Integer) As Task(Of Decimal) Implements IAgenciaService.ImporteReembolso
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = $"PedidosVenta/ImporteReembolso?empresa={empresa.Trim}&pedido={pedido}"

                response = Await client.GetAsync(urlConsulta)

                respuesta = If(response.IsSuccessStatusCode, Await response.Content.ReadAsStringAsync(), "")

            Catch ex As Exception
                Throw New Exception("No se ha podido calcular el reembolso del pedido", ex)
            Finally

            End Try

            Dim importe As Decimal = JsonConvert.DeserializeObject(Of Decimal)(respuesta)

            Return importe

        End Using
    End Function

End Class
