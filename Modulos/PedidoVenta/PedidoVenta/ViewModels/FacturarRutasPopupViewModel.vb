Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Services
Imports Newtonsoft.Json
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports Unity

''' <summary>
''' ViewModel para el popup de facturación de rutas
''' </summary>
Public Class FacturarRutasPopupViewModel
    Inherits BindableBase
    Implements IDialogAware

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly dialogService As IDialogService
    Private ReadOnly servicioFacturacion As IServicioFacturacionRutas
    Private ReadOnly servicioImpresion As IServicioImpresionDocumentos
    Private ReadOnly container As IUnityContainer

#Region "Constructor"

    Public Sub New(configuracion As IConfiguracion, dialogService As IDialogService, servicioFacturacion As IServicioFacturacionRutas, servicioImpresion As IServicioImpresionDocumentos, container As IUnityContainer)
        Me.configuracion = configuracion
        Me.dialogService = dialogService
        Me.servicioFacturacion = servicioFacturacion
        Me.servicioImpresion = servicioImpresion
        Me.container = container

        ' Inicializar comandos
        VerResumenCommand = New DelegateCommand(AddressOf VerResumen, AddressOf CanVerResumen)
        FacturarCommand = New DelegateCommand(AddressOf FacturarRutas, AddressOf CanFacturar)
        CancelarCommand = New DelegateCommand(AddressOf Cancelar)

        ' Inicializar colección vacía
        TiposRutaDisponibles = New ObservableCollection(Of TipoRutaInfoDTO)()
    End Sub

#End Region

#Region "IDialogAware Implementation"

    Public ReadOnly Property Title As String Implements IDialogAware.Title
        Get
            Return "Facturar Rutas"
        End Get
    End Property

    Public Event RequestClose As Action(Of IDialogResult) Implements IDialogAware.RequestClose

    Public Sub OnDialogClosed() Implements IDialogAware.OnDialogClosed
        ' Limpieza si es necesaria
    End Sub

    Public Async Sub OnDialogOpened(parameters As IDialogParameters) Implements IDialogAware.OnDialogOpened
        ' Cargar tipos de ruta desde la API
        Await CargarTiposRuta()
    End Sub

    Public Function CanCloseDialog() As Boolean Implements IDialogAware.CanCloseDialog
        Return Not EstaProcesando
    End Function

#End Region

#Region "Metodos Privados"

    ''' <summary>
    ''' Carga los tipos de ruta disponibles desde la API
    ''' </summary>
    Private Async Function CargarTiposRuta() As Task
        Try
            ' Usar el servicio de facturación que maneja la autenticación
            Dim tipos = Await servicioFacturacion.ObtenerTiposRuta()

            TiposRutaDisponibles.Clear()
            For Each tipo In tipos
                TiposRutaDisponibles.Add(tipo)
            Next

            ' Seleccionar el primero por defecto
            If TiposRutaDisponibles.Count > 0 Then
                TipoRutaSeleccionado = TiposRutaDisponibles(0)
            End If

        Catch uex As UnauthorizedAccessException
            MensajeEstado = "Sesión expirada. Por favor, inicie sesión nuevamente."
            ColorMensaje = Brushes.Red
            dialogService.ShowError("Su sesión ha expirado. Por favor, inicie sesión nuevamente.")
        Catch ex As Exception
            MensajeEstado = $"Error al cargar tipos de ruta: {ex.Message}"
            ColorMensaje = Brushes.Red
            dialogService.ShowError($"No se pudieron cargar los tipos de ruta: {ex.Message}")
        End Try
    End Function

#End Region

#Region "Propiedades"

    Private _tiposRutaDisponibles As ObservableCollection(Of TipoRutaInfoDTO)
    ''' <summary>
    ''' Lista de tipos de ruta disponibles, cargada dinámicamente desde la API
    ''' </summary>
    Public Property TiposRutaDisponibles As ObservableCollection(Of TipoRutaInfoDTO)
        Get
            Return _tiposRutaDisponibles
        End Get
        Set(value As ObservableCollection(Of TipoRutaInfoDTO))
            Dim unused = SetProperty(_tiposRutaDisponibles, value)
        End Set
    End Property

    Private _tipoRutaSeleccionado As TipoRutaInfoDTO
    ''' <summary>
    ''' Tipo de ruta actualmente seleccionado por el usuario
    ''' </summary>
    Public Property TipoRutaSeleccionado As TipoRutaInfoDTO
        Get
            Return _tipoRutaSeleccionado
        End Get
        Set(value As TipoRutaInfoDTO)
            If SetProperty(_tipoRutaSeleccionado, value) Then
                LimpiarResumen()
                VerResumenCommand.RaiseCanExecuteChanged()
                FacturarCommand.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _estaProcesando As Boolean
    Public Property EstaProcesando As Boolean
        Get
            Return _estaProcesando
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_estaProcesando, value)
            VerResumenCommand.RaiseCanExecuteChanged()
            FacturarCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _mensajeEstado As String
    Public Property MensajeEstado As String
        Get
            Return _mensajeEstado
        End Get
        Set(value As String)
            Dim unused = SetProperty(_mensajeEstado, value)
        End Set
    End Property

    Private _colorMensaje As Brush = Brushes.Black
    Public Property ColorMensaje As Brush
        Get
            Return _colorMensaje
        End Get
        Set(value As Brush)
            Dim unused = SetProperty(_colorMensaje, value)
        End Set
    End Property

    Private _previewData As PreviewFacturacionRutasResponseDTO
    Public Property PreviewData As PreviewFacturacionRutasResponseDTO
        Get
            Return _previewData
        End Get
        Set(value As PreviewFacturacionRutasResponseDTO)
            Dim unused = SetProperty(_previewData, value)
            FacturarCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _mostrandoPreview As Boolean
    Public Property MostrandoPreview As Boolean
        Get
            Return _mostrandoPreview
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_mostrandoPreview, value)
        End Set
    End Property

#End Region

#Region "Comandos"

    Public Property VerResumenCommand As DelegateCommand
    Public Property FacturarCommand As DelegateCommand
    Public Property CancelarCommand As DelegateCommand

    Private Function CanVerResumen() As Boolean
        Return Not EstaProcesando AndAlso TipoRutaSeleccionado IsNot Nothing
    End Function

    Private Async Sub VerResumen()
        EstaProcesando = True
        MensajeEstado = "Generando resumen..."
        ColorMensaje = Brushes.Blue
        MostrandoPreview = False

        Try
            ' Validar que hay un tipo seleccionado
            If TipoRutaSeleccionado Is Nothing Then
                MensajeEstado = "Por favor seleccione un tipo de ruta"
                ColorMensaje = Brushes.Orange
                Return
            End If

            ' Crear request con el ID del tipo seleccionado
            Dim request As New FacturarRutasRequestDTO With {
                .TipoRuta = TipoRutaSeleccionado.Id,
                .FechaEntregaDesde = Date.Today
            }

            ' Llamar al servicio de preview
            Dim preview As PreviewFacturacionRutasResponseDTO = Await servicioFacturacion.PreviewFacturarRutas(request)

            ' Guardar el preview
            PreviewData = preview

            ' Mostrar resumen del preview
            MostrarResumenPreview(preview)
            MostrandoPreview = True

        Catch ex As Exception
            MensajeEstado = $"Error al generar resumen: {ex.Message}"
            ColorMensaje = Brushes.Red
            dialogService.ShowError(ex.Message)
            PreviewData = Nothing
            MostrandoPreview = False
        Finally
            EstaProcesando = False
        End Try
    End Sub

    Private Function CanFacturar() As Boolean
        Return Not EstaProcesando AndAlso TipoRutaSeleccionado IsNot Nothing AndAlso PreviewData IsNot Nothing
    End Function

    Private Async Sub FacturarRutas()
        EstaProcesando = True
        MensajeEstado = "Obteniendo pedidos..."
        ColorMensaje = Brushes.Blue

        Try
            ' Validar que hay un tipo seleccionado
            If TipoRutaSeleccionado Is Nothing Then
                MensajeEstado = "Por favor seleccione un tipo de ruta"
                ColorMensaje = Brushes.Orange
                Return
            End If

            ' Crear request con el ID del tipo seleccionado
            Dim request As New FacturarRutasRequestDTO With {
                .TipoRuta = TipoRutaSeleccionado.Id,
                .FechaEntregaDesde = Date.Today
            }

            ' Llamar al servicio
            MensajeEstado = "Facturando pedidos..."
            Dim response As FacturarRutasResponseDTO = Await servicioFacturacion.FacturarRutas(request)

            ' Mostrar resultado
            MostrarResumen(response)

            ' Verificar si hay documentos para imprimir
            Dim totalDocumentosParaImprimir = response.FacturasParaImprimir + response.AlbaranesParaImprimir
            If totalDocumentosParaImprimir > 0 Then
                Await PreguntarEImprimirDocumentos(response, totalDocumentosParaImprimir)
            End If

            ' Si hay errores, mostrar ventana de errores
            If response.PedidosConErrores IsNot Nothing AndAlso response.PedidosConErrores.Count > 0 Then
                MostrarVentanaErrores(response.PedidosConErrores)
            Else
                ' Si todo fue exitoso y no hay documentos que imprimir, esperar 2 segundos y cerrar
                If totalDocumentosParaImprimir = 0 Then
                    Await Task.Delay(2000)
                    Cancelar()
                End If
            End If

        Catch ex As Exception
            MensajeEstado = $"Error: {ex.Message}"
            ColorMensaje = Brushes.Red
            dialogService.ShowError(ex.Message)
        Finally
            EstaProcesando = False
        End Try
    End Sub

    Private Sub MostrarResumen(response As FacturarRutasResponseDTO)
        Dim sb As New System.Text.StringBuilder()
        Dim unused6 = sb.AppendLine($"✓ Procesados: {response.PedidosProcesados}")
        Dim unused5 = sb.AppendLine($"✓ Albaranes: {response.AlbaranesCreados}")
        Dim unused4 = sb.AppendLine($"✓ Facturas: {response.FacturasCreadas}")

        If response.FacturasParaImprimir > 0 Then
            Dim unused3 = sb.AppendLine($"🖨 Facturas para imprimir: {response.FacturasParaImprimir}")
        End If
        If response.AlbaranesParaImprimir > 0 Then
            Dim unused2 = sb.AppendLine($"🖨 Albaranes para imprimir: {response.AlbaranesParaImprimir}")
        End If

        If response.PedidosConErrores IsNot Nothing AndAlso response.PedidosConErrores.Count > 0 Then
            Dim unused1 = sb.AppendLine($"ÃƒÆ’Ã‚Â¢Ãƒâ€¦Ã‚Â¡Ãƒâ€šÃ‚Â ⚠ Errores: {response.PedidosConErrores.Count}")
            ColorMensaje = Brushes.Orange
        Else
            ColorMensaje = Brushes.Green
        End If

        Dim unused = sb.AppendLine($"Tiempo: {response.TiempoTotal.TotalSeconds:F1}s")

        MensajeEstado = sb.ToString()
    End Sub

    Private Sub MostrarVentanaErrores(errores As List(Of PedidoConErrorDTO))
        Try
            System.Diagnostics.Debug.WriteLine($"MostrarVentanaErrores - Recibidos {If(errores Is Nothing, 0, errores.Count)} errores")

            ' Persistir los errores en archivo JSON para que persistan aunque se cierre Nesto
            PersistirErrores(errores)

            ' Mostrar el diálogo usando el dialogService de Prism (con auto-wiring)
            Dim parametros = New DialogParameters From {
                {"errores", errores}
            }

            System.Diagnostics.Debug.WriteLine($"MostrarVentanaErrores - Llamando ShowDialog con {errores.Count} errores")
            dialogService.ShowDialog("ErroresFacturacionRutasPopup", parametros, Nothing)
            System.Diagnostics.Debug.WriteLine("MostrarVentanaErrores - ShowDialog completado")

        Catch ex As Exception
            ' Si falla la ventana completa, fallback al diálogo simple
            Dim mensaje As String = String.Format("Se encontraron {0} pedidos con errores.{1}{1}", errores.Count, Environment.NewLine)
            For Each errorItem In errores.Take(5)
                mensaje &= String.Format("Pedido {0} - {1}: {2}{3}", errorItem.NumeroPedido, errorItem.TipoError, errorItem.MensajeError, Environment.NewLine)
            Next

            If errores.Count > 5 Then
                mensaje &= String.Format("... y {0} errores más", errores.Count - 5)
            End If

            dialogService.ShowError(mensaje)
        End Try
    End Sub

    Private Sub PersistirErrores(errores As List(Of PedidoConErrorDTO))
        Try
            ' Guardar en AppData\Local\Nesto\ErroresFacturacion.json
            Dim appDataPath As String = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Nesto")

            ' Crear directorio si no existe
            If Not System.IO.Directory.Exists(appDataPath) Then
                Dim unused = System.IO.Directory.CreateDirectory(appDataPath)
            End If

            Dim archivoErrores As String = System.IO.Path.Combine(appDataPath, "ErroresFacturacion.json")

            ' Serializar a JSON
            Dim jsonSettings As New Newtonsoft.Json.JsonSerializerSettings With {
                .Formatting = Newtonsoft.Json.Formatting.Indented,
                .NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            }

            Dim json As String = Newtonsoft.Json.JsonConvert.SerializeObject(New With {
                .FechaHora = Date.Now,
                errores
            }, jsonSettings)

            ' Guardar en archivo
            System.IO.File.WriteAllText(archivoErrores, json)

        Catch ex As Exception
            ' Si falla la persistencia, no bloquear la aplicación
            System.Diagnostics.Debug.WriteLine($"Error al persistir errores: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Pregunta al usuario si desea imprimir los documentos generados.
    ''' Si acepta, imprime todos los documentos (facturas y albaranes).
    ''' </summary>
    Private Async Function PreguntarEImprimirDocumentos(response As FacturarRutasResponseDTO, totalDocumentos As Integer) As Task
        Try
            ' Preguntar al usuario
            Dim mensaje As String = $"Se han generado {totalDocumentos} documentos para imprimir:{Environment.NewLine}"

            If response.FacturasParaImprimir > 0 Then
                mensaje &= $"- {response.FacturasParaImprimir} factura(s){Environment.NewLine}"
            End If

            If response.AlbaranesParaImprimir > 0 Then
                mensaje &= $"- {response.AlbaranesParaImprimir} albarán(es){Environment.NewLine}"
            End If

            mensaje &= $"{Environment.NewLine}¿Desea imprimirlos ahora?"

            Dim resultado = MessageBox.Show(mensaje, "Imprimir documentos", MessageBoxButton.YesNo, MessageBoxImage.Question)

            If resultado = MessageBoxResult.Yes Then
                ' Imprimir documentos
                MensajeEstado = "Imprimiendo documentos..."
                ColorMensaje = Brushes.Blue

                Dim resultadoImpresion = Await servicioImpresion.ImprimirDocumentos(response)

                ' Mostrar resultado de impresión
                MostrarResultadoImpresion(resultadoImpresion)

                ' Esperar 3 segundos para que el usuario vea el mensaje
                Await Task.Delay(3000)
            End If

        Catch ex As Exception
            MensajeEstado = $"Error al imprimir: {ex.Message}"
            ColorMensaje = Brushes.Red
            dialogService.ShowError($"Error al imprimir documentos: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Muestra el resultado de la impresión de documentos
    ''' </summary>
    Private Sub MostrarResultadoImpresion(resultado As ResultadoImpresion)
        Dim sb As New System.Text.StringBuilder()

        If resultado.DocumentosImpresos > 0 Then
            Dim unused3 = sb.AppendLine($"✓ {resultado.DocumentosImpresos} documento(s) impreso(s) correctamente")
        End If

        If resultado.DocumentosConError > 0 Then
            Dim unused2 = sb.AppendLine($"ÃƒÆ’Ã‚Â¢Ãƒâ€¦Ã‚Â¡Ãƒâ€šÃ‚Â ⚠ {resultado.DocumentosConError} documento(s) con errores de impresión:")
            For Each errorItem In resultado.Errores.Take(3)
                Dim unused1 = sb.AppendLine($"  - {errorItem.TipoDocumento} {errorItem.NumeroDocumento}: {errorItem.MensajeError}")
            Next

            If resultado.Errores.Count > 3 Then
                Dim unused = sb.AppendLine($"  ... y {resultado.Errores.Count - 3} error(es) más")
            End If

            ColorMensaje = Brushes.Orange
        Else
            ColorMensaje = Brushes.Green
        End If

        MensajeEstado &= Environment.NewLine & sb.ToString()
    End Sub

    ''' <summary>
    ''' Muestra el resumen del preview de facturación
    ''' </summary>
    Private Sub MostrarResumenPreview(preview As PreviewFacturacionRutasResponseDTO)
        Dim sb As New System.Text.StringBuilder()
        Dim unused11 = sb.AppendLine("=== RESUMEN DE FACTURACIÓN ===")
        Dim unused10 = sb.AppendLine()
        Dim unused9 = sb.AppendLine($"📦 Pedidos a procesar: {preview.NumeroPedidos}")
        Dim unused8 = sb.AppendLine()

        ' Mostrar contadores
        Dim unused7 = sb.AppendLine($"🖨 Albaranes: {preview.NumeroAlbaranes}")
        If preview.NumeroAlbaranes > 0 Then
            Dim unused6 = sb.AppendLine($"   Base imponible: {preview.BaseImponibleAlbaranes:C2}")
        End If

        Dim unused5 = sb.AppendLine($"🧾 Facturas: {preview.NumeroFacturas}")
        If preview.NumeroFacturas > 0 Then
            Dim unused4 = sb.AppendLine($"   Base imponible: {preview.BaseImponibleFacturas:C2}")
        End If

        Dim unused3 = sb.AppendLine($"🖹 Notas de entrega: {preview.NumeroNotasEntrega}")
        If preview.NumeroNotasEntrega > 0 Then
            Dim unused2 = sb.AppendLine($"   Base imponible: {preview.BaseImponibleNotasEntrega:C2}")
        End If

        Dim unused1 = sb.AppendLine()

        ' Calcular totales
        Dim baseTotal As Decimal = preview.BaseImponibleAlbaranes + preview.BaseImponibleFacturas + preview.BaseImponibleNotasEntrega
        Dim unused = sb.AppendLine($"💰 Base imponible TOTAL: {baseTotal:C2}")

        MensajeEstado = sb.ToString()
        ColorMensaje = Brushes.Green
    End Sub

    Private Sub Cancelar()
        RaiseEvent RequestClose(New DialogResult(ButtonResult.Cancel))
    End Sub

    ''' <summary>
    ''' Limpia el resumen y preview cuando se cambia el tipo de ruta
    ''' </summary>
    Private Sub LimpiarResumen()
        PreviewData = Nothing
        MostrandoPreview = False
        MensajeEstado = String.Empty
        ColorMensaje = Brushes.Black
    End Sub

#End Region
End Class
