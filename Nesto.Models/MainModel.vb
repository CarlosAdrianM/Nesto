Imports System.Data
Imports System.Data.SqlClient
Imports System.Collections.ObjectModel

Module Publico
    Public Const strConexion As String = "Data Source=DC2016;Initial Catalog=NV;Integrated Security=True"
    Public Const strConexionOdbc As String = "Dsn=Nesto"
    Public Const strEmpresa As String = "1"
End Module

Public Class MainModel
    'Implements INotifyPropertyChanged

    Public Class RatioDeuda
        Inherits RatioDeudas.RatioDeudaDataTable

        Public Shared Function CargarRatiosDeuda() As RatioDeuda
            Dim ratioCollection As New RatioDeuda

            'Dim dllparametros As New Global.dllParametros.dllParametros
            'Dim strVendedor As String = dllParametros.LeerParámetro(strConexionOdbc, "1", "Vendedor")
            Dim strVendedor As String = "NV"

            Dim cnn As New SqlConnection(strConexion)
            Dim cmd As New SqlCommand("prdRatiosDeuda", cnn)
            cmd.CommandType = CommandType.StoredProcedure
            If strVendedor <> "" Then
                cmd.Parameters.Add("@Vendedor", SqlDbType.Char)
                cmd.Parameters("@Vendedor").Value = strVendedor
            End If


            Dim dtRatioDeudas As New RatioDeuda
            Dim daRatioDeudas As New SqlDataAdapter

            Try
                cnn.Open()
                daRatioDeudas.SelectCommand = cmd
                daRatioDeudas.Fill(dtRatioDeudas)
                ratioCollection = dtRatioDeudas
                cnn.Close()
            Catch
                MsgBox("No se pueden actualizar los ratios de venta")
            End Try


            Return ratioCollection

        End Function


    End Class

    Public Class DetalleVentaReal
        Inherits RatioDeudas.DetalleVentaRealDataTable

        Private Shared _DetalleVentaTrim As DetalleVentaReal
        Public Shared Property DetalleVentaTrim As DetalleVentaReal
            Get
                Return _DetalleVentaTrim
            End Get
            Set(value As DetalleVentaReal)
                _DetalleVentaTrim = value
            End Set
        End Property
        Public Shared Function CargarDetalleVentaReal(strVendedor As String) As DetalleVentaReal
            Dim ratioCollection As New DetalleVentaReal



            'Dim dllparametros As New Global.dllParametros.dllParametros
            'Dim strVendedor As String = dllparametros.LeerParámetro(strConexionOdbc, "1", "Vendedor")

            Dim cnn As New SqlConnection(strConexion)
            Dim cmd As New SqlCommand("prdRatiosDeudaDetalle", cnn)
            cmd.CommandType = CommandType.StoredProcedure
            'If strVendedor <> "" Then
            cmd.Parameters.Add("@Vendedor", SqlDbType.Char)
            cmd.Parameters("@Vendedor").Value = strVendedor
            cmd.Parameters.Add("@TipoDetalle", SqlDbType.Int)
            cmd.Parameters("@TipoDetalle").Value = 1
            'End If


            Dim dtRatioDeudas As New DetalleVentaReal
            Dim daRatioDeudas As New SqlDataAdapter

            Try
                cnn.Open()
                daRatioDeudas.SelectCommand = cmd
                daRatioDeudas.Fill(dtRatioDeudas)
                ratioCollection = dtRatioDeudas
                cnn.Close()
            Catch
                MsgBox(Err.Description)
            End Try

            DetalleVentaTrim = ratioCollection
            Return ratioCollection

        End Function
    End Class

    Public Class DetalleVentaPeriodo
        Inherits RatioDeudas.DetalleVentaPeriodoDataTable

        Private Shared _DetalleVentaTrim As DetalleVentaPeriodo
        Public Shared Property DetalleVentaTrim As DetalleVentaPeriodo
            Get
                Return _DetalleVentaTrim
            End Get
            Set(value As DetalleVentaPeriodo)
                _DetalleVentaTrim = value
            End Set
        End Property
        Public Shared Function CargarDetalleVentaPeriodo(strVendedor As String) As DetalleVentaPeriodo
            Dim ratioCollection As New DetalleVentaPeriodo



            'Dim dllparametros As New Global.dllParametros.dllParametros
            'Dim strVendedor As String = dllparametros.LeerParámetro(strConexionOdbc, "1", "Vendedor")

            Dim cnn As New SqlConnection(strConexion)
            Dim cmd As New SqlCommand("prdRatiosDeudaDetalle", cnn)
            cmd.CommandType = CommandType.StoredProcedure
            'If strVendedor <> "" Then
            cmd.Parameters.Add("@Vendedor", SqlDbType.Char)
            cmd.Parameters("@Vendedor").Value = strVendedor
            cmd.Parameters.Add("@TipoDetalle", SqlDbType.Int)
            cmd.Parameters("@TipoDetalle").Value = 2
            'End If


            Dim dtRatioDeudas As New DetalleVentaPeriodo
            Dim daRatioDeudas As New SqlDataAdapter

            Try
                cnn.Open()
                daRatioDeudas.SelectCommand = cmd
                daRatioDeudas.Fill(dtRatioDeudas)
                ratioCollection = dtRatioDeudas
                cnn.Close()
            Catch
                MsgBox(Err.Description)
            End Try

            DetalleVentaTrim = ratioCollection
            Return ratioCollection

        End Function
    End Class

    Public Class DetalleDeuda
        Inherits RatioDeudas.DetalleDeudaDataTable

        Private Shared _DetalleVentaTrim As DetalleDeuda
        Public Shared Property DetalleVentaTrim As DetalleDeuda
            Get
                Return _DetalleVentaTrim
            End Get
            Set(value As DetalleDeuda)
                _DetalleVentaTrim = value
            End Set
        End Property
        Public Shared Function CargarDetalleDeuda(strVendedor As String) As DetalleDeuda
            Dim ratioCollection As New DetalleDeuda



            'Dim dllparametros As New Global.dllParametros.dllParametros
            'Dim strVendedor As String = dllparametros.LeerParámetro(strConexionOdbc, "1", "Vendedor")

            Dim cnn As New SqlConnection(strConexion)
            Dim cmd As New SqlCommand("prdRatiosDeudaDetalle", cnn)
            cmd.CommandType = CommandType.StoredProcedure
            'If strVendedor <> "" Then
            cmd.Parameters.Add("@Vendedor", SqlDbType.Char)
            cmd.Parameters("@Vendedor").Value = strVendedor
            cmd.Parameters.Add("@TipoDetalle", SqlDbType.Int)
            cmd.Parameters("@TipoDetalle").Value = 3
            'End If


            Dim dtRatioDeudas As New DetalleDeuda
            Dim daRatioDeudas As New SqlDataAdapter

            Try
                cnn.Open()
                daRatioDeudas.SelectCommand = cmd
                daRatioDeudas.Fill(dtRatioDeudas)
                ratioCollection = dtRatioDeudas
                cnn.Close()
            Catch
                MsgBox(Err.Description)
            End Try

            DetalleVentaTrim = ratioCollection
            Return ratioCollection

        End Function
    End Class

    'Public Class DeudaAgrupada
    '    Inherits RatioDeudas.DeudaAgrupadaDataTable

    '    Private Shared _DetalleVentaTrim As DeudaAgrupada
    '    Public Shared Property DetalleVentaTrim As DeudaAgrupada
    '        Get
    '            Return _DetalleVentaTrim
    '        End Get
    '        Set(value As DeudaAgrupada)
    '            _DetalleVentaTrim = value
    '        End Set
    '    End Property
    '    Public Shared Function CargarDeudaAgrupada(strVendedor As String) As DeudaAgrupada
    '        Dim ratioCollection As New DeudaAgrupada

    '        Dim cnn As New SqlConnection(strConexion)
    '        Dim cmd As New SqlCommand("prdRatiosDeudaDetalle", cnn)
    '        cmd.CommandType = CommandType.StoredProcedure
    '        'If strVendedor <> "" Then
    '        cmd.Parameters.Add("@Vendedor", SqlDbType.Char)
    '        cmd.Parameters("@Vendedor").Value = strVendedor
    '        cmd.Parameters.Add("@TipoDetalle", SqlDbType.Int)
    '        cmd.Parameters("@TipoDetalle").Value = 3
    '        'End If


    '        Dim dtRatioDeudas As New DeudaAgrupada
    '        Dim daRatioDeudas As New SqlDataAdapter

    '        Try
    '            cnn.Open()
    '            daRatioDeudas.SelectCommand = cmd
    '            daRatioDeudas.Fill(dtRatioDeudas)
    '            ratioCollection = dtRatioDeudas
    '            cnn.Close()
    '        Catch
    '            MsgBox(Err.Description)
    '        End Try

    '        DetalleVentaTrim = ratioCollection
    '        Return ratioCollection

    '    End Function
    'End Class

    Public Class RatioVenta
        Inherits RatioVentas.RatioVentaDataTable

        Public Shared Function CargarRatiosVenta() As RatioVenta
            Dim ratioCollection As New RatioVenta



            'Dim dllparametros As New Global.dllParametros.dllParametros
            'Dim strVendedor As String = dllparametros.LeerParámetro(strConexionOdbc, "1", "Vendedor")
            Dim strVendedor As String = "NV"

            Dim cnn As New SqlConnection(strConexion)
            Dim cmd As New SqlCommand("prdRatioVentas", cnn)
            cmd.CommandType = CommandType.StoredProcedure
            If strVendedor <> "" Then
                cmd.Parameters.Add("@Vendedor", SqlDbType.Char)
                cmd.Parameters("@Vendedor").Value = strVendedor
            End If


            Dim dtRatioVentas As New RatioVenta
            Dim daRatioVentas As New SqlDataAdapter

            Try
                cnn.Open()
                daRatioVentas.SelectCommand = cmd
                daRatioVentas.Fill(dtRatioVentas)
                ratioCollection = dtRatioVentas
                cnn.Close()
            Catch
                MsgBox("No se pueden actualizar los ratios de venta")
            End Try


            Return ratioCollection

        End Function

    End Class

    Public Class NVDataSetM
        Public Shared Function CargarInformePeluqueria(fchInicial As Date, fchFinal As Date) As NVDataSet
            Dim cnn As New SqlConnection(strConexion)
            Dim da As New SqlDataAdapter("prdInformePremioVendedoresUL", cnn)
            da.SelectCommand.CommandType = CommandType.StoredProcedure
            da.SelectCommand.Parameters.Add("@FechaInicial", SqlDbType.DateTime)
            da.SelectCommand.Parameters.Add("@FechaFinal", SqlDbType.DateTime)

            da.SelectCommand.Parameters("@FechaInicial").Value = fchInicial
            da.SelectCommand.Parameters("@FechaFinal").Value = fchFinal


            Dim ds As New NVDataSet       ' Change this name to match .xsd file name.
            da.Fill(ds, "Informe")
            Return ds
        End Function
    End Class

    Public Class ControlPedidosM
        Public Shared Function CargarInformePedidos() As ControlPedidos
            Dim cnn As New SqlConnection(strConexion)
            Dim da As New SqlDataAdapter("prdInformeControlPedidos", cnn)
            da.SelectCommand.CommandType = CommandType.StoredProcedure

            Dim ds As New ControlPedidos       ' Change this name to match .xsd file name.
            da.Fill(ds, "ControlPedidos")
            Return ds
        End Function
    End Class

    Public Class UbicacionesM
        Public Shared Function CargarInformeUbicaciones(Numero As Integer) As Ubicaciones
            Dim cnn As New SqlConnection(strConexion)
            Dim da As New SqlDataAdapter("prdUbicacionesParaInventario", cnn)
            da.SelectCommand.CommandType = CommandType.StoredProcedure
            da.SelectCommand.Parameters.Add("@Número", SqlDbType.Int)

            da.SelectCommand.Parameters("@Número").Value = Numero


            Dim ds As New Ubicaciones
            da.Fill(ds, "Ubicaciones")
            Return ds
        End Function
    End Class


    Public Class Vendedor
        'Inherits RatioVentas.RatioVentaDataTable

        Public Shared Function CargarVendedor() As String
            'Throw New NotImplementedException("Parte del programa no implementada")

            'Dim dllparametros As New Global.dllParametros.dllParametros
            'Dim strVendedor As String = dllparametros.LeerParámetro(strConexionOdbc, "1", "Vendedor")
            'Return strVendedor

            Return "NV"

        End Function

    End Class

    Public Function leerParametro(empresa As String, clave As String) As String
        'Throw New NotImplementedException("Parte del programa no implementada")
        'Dim dllparametros As New Global.dllParametros.dllParametros
        'Return dllparametros.LeerParámetro(strConexionOdbc, empresa, clave)
        If clave = "EmpresaPorDefecto" Then
            Return "1"
        ElseIf clave = "UltNumCliente" Then
            Return "992"
        ElseIf clave = "RutaMandatos" Then
            Return "F:\DATOS2\Mandatos\"
        End If
        Return "parte del programa no implementada"
    End Function

End Class
