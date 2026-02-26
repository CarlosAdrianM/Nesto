using Nesto.Modulos.Cajas.Bancos;
using Nesto.Modulos.Cajas.Models;

namespace CajasTests
{
    /// <summary>
    /// Tests para el nuevo formato CSV de PayPal (informe de Actividad).
    /// Issue Nesto#303: El informe antiguo fue retirado por PayPal.
    ///
    /// Formato nuevo:
    /// "Fecha","Hora","Zona horaria","Descripción","Divisa","Bruto ","Comisión ","Neto","Saldo",
    /// "Id. de transacción","Correo electrónico del remitente","Nombre",...
    /// </summary>
    [TestClass]
    public class BancoPaypalNuevoFormatoTests
    {
        private readonly ConvertidorPaypalNuevo _convertidor = new();

        #region Detección de formato

        [TestMethod]
        public void PuedeConvertir_CabeceraCorrecta_DevuelveTrue()
        {
            string cabecera = "\"Fecha\",\"Hora\",\"Zona horaria\",\"Descripción\",\"Divisa\"";

            Assert.IsTrue(_convertidor.PuedeConvertir(cabecera));
        }

        [TestMethod]
        public void PuedeConvertir_FormatoAntiguo_DevuelveFalse()
        {
            // El formato antiguo empieza con "RS" o "RD"
            string contenidoAntiguo = "\"RS\",\"F55F2LRZ5CMS8\",\"OPENING\",\"EUR\",\"1234.56\"";

            Assert.IsFalse(_convertidor.PuedeConvertir(contenidoAntiguo));
        }

        [TestMethod]
        public void PuedeConvertir_ContenidoVacio_DevuelveFalse()
        {
            Assert.IsFalse(_convertidor.PuedeConvertir(""));
            Assert.IsFalse(_convertidor.PuedeConvertir(null));
        }

        #endregion

        #region Parsing de fechas

        [TestMethod]
        public void Convertir_FechaFormato_dMyyyy_ParseaCorrectamente()
        {
            string csv = CrearCsvConUnaLinea(
                fecha: "1/7/2025", hora: "15:37:32",
                bruto: "100,00", comision: "-3,00", neto: "97,00", saldo: "97,00",
                idTransaccion: "ABC123", nombre: "Test User");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual(new DateTime(2025, 7, 1), resultado.Apuntes[0].FechaOperacion);
            Assert.AreEqual(new DateTime(2025, 7, 1), resultado.Apuntes[0].FechaValor);
        }

        [TestMethod]
        public void Convertir_FechaFormatoDiaDosDigitos_ParseaCorrectamente()
        {
            string csv = CrearCsvConUnaLinea(
                fecha: "12/11/2025", hora: "09:00:00",
                bruto: "50,00", comision: "-1,50", neto: "48,50", saldo: "48,50",
                idTransaccion: "XYZ789", nombre: "Test");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual(new DateTime(2025, 11, 12), resultado.Apuntes[0].FechaOperacion);
        }

        #endregion

        #region Parsing de importes

        [TestMethod]
        public void Convertir_ImporteConComa_ParseaComoDecimal()
        {
            string csv = CrearCsvConUnaLinea(
                bruto: "160,71", comision: "-5,01", neto: "155,70", saldo: "606,37",
                idTransaccion: "6GB951", nombre: "Veronica");

            var resultado = _convertidor.Convertir(csv);

            // El apunte principal debe tener el importe bruto
            Assert.AreEqual(160.71m, resultado.Apuntes[0].ImporteMovimiento);
        }

        [TestMethod]
        public void Convertir_ImporteConPuntoDeMiles_ParseaCorrectamente()
        {
            // En el ejemplo real: "-4.000,00" para una retirada
            string csv = CrearCsvConUnaLinea(
                bruto: "-4.000,00", comision: "0,00", neto: "-4.000,00", saldo: "366,11",
                idTransaccion: "9C5250", nombre: "",
                descripcion: "Retirada iniciada por el usuario");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual(4000.00m, resultado.Apuntes[0].ImporteMovimiento);
            Assert.AreEqual("1", resultado.Apuntes[0].ClaveDebeOHaberMovimiento,
                "Importe negativo debe ser Debe (1)");
        }

        #endregion

        #region Saldo inicial

        [TestMethod]
        public void Convertir_SaldoInicial_CalculaDesdePrimeraLinea()
        {
            // Saldo primera línea = 606,37. Neto primera línea = 155,70
            // Saldo inicial = 606,37 - 155,70 = 450,67
            string csv = CrearCsvConUnaLinea(
                bruto: "160,71", comision: "-5,01", neto: "155,70", saldo: "606,37",
                idTransaccion: "ABC123", nombre: "Test");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual(450.67m, resultado.Cabecera.ImporteSaldoInicial);
        }

        #endregion

        #region Mapeo de tipos de transacción

        [TestMethod]
        public void Convertir_PagoExpress_MapeaAConcepto02()
        {
            string csv = CrearCsvConUnaLinea(
                descripcion: "Pago con Pago exprés",
                bruto: "100,00", comision: "-3,00", neto: "97,00", saldo: "97,00",
                idTransaccion: "ABC", nombre: "Test");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual("02", resultado.Apuntes[0].ConceptoComun,
                "Pago exprés debe mapearse a concepto 02 (Sale)");
        }

        [TestMethod]
        public void Convertir_Retirada_MapeaAConcepto03()
        {
            string csv = CrearCsvConUnaLinea(
                descripcion: "Retirada iniciada por el usuario",
                bruto: "-4.000,00", comision: "0,00", neto: "-4.000,00", saldo: "366,11",
                idTransaccion: "9C5", nombre: "");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual("03", resultado.Apuntes[0].ConceptoComun,
                "Retirada debe mapearse a concepto 03 (Other Payments)");
        }

        [TestMethod]
        public void Convertir_ConversionDivisas_MapeaAConcepto13()
        {
            string csv = CrearCsvConUnaLinea(
                descripcion: "Conversión de divisas general",
                bruto: "-246,50", comision: "0,00", neto: "-246,50", saldo: "2.595,10",
                idTransaccion: "2L3", nombre: "");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual("13", resultado.Apuntes[0].ConceptoComun,
                "Conversión de divisas debe mapearse a concepto 13");
        }

        [TestMethod]
        public void Convertir_BillPay_MapeaAConcepto03()
        {
            string csv = CrearCsvConUnaLinea(
                descripcion: "Pago con Pago de usuario de BillPay preaprobado",
                bruto: "-276,00", comision: "0,00", neto: "-276,00", saldo: "-276,00",
                idTransaccion: "2S1", nombre: "MailChimp");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual("03", resultado.Apuntes[0].ConceptoComun,
                "BillPay debe mapearse a concepto 03 (Other Payments)");
        }

        [TestMethod]
        public void Convertir_DescripcionDesconocida_MapeaAConcepto99()
        {
            string csv = CrearCsvConUnaLinea(
                descripcion: "Algo completamente nuevo",
                bruto: "10,00", comision: "0,00", neto: "10,00", saldo: "10,00",
                idTransaccion: "ZZZ", nombre: "Test");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual("99", resultado.Apuntes[0].ConceptoComun,
                "Descripción desconocida debe mapearse a concepto 99");
        }

        #endregion

        #region Generación de apuntes bruto + comisión

        [TestMethod]
        public void Convertir_ConComision_GeneraDosApuntes()
        {
            string csv = CrearCsvConUnaLinea(
                bruto: "160,71", comision: "-5,01", neto: "155,70", saldo: "606,37",
                idTransaccion: "6GB951", nombre: "Veronica");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual(2, resultado.Apuntes.Count,
                "Debe generar 2 apuntes: bruto + comisión");
            Assert.AreEqual(160.71m, resultado.Apuntes[0].ImporteMovimiento,
                "Primer apunte: importe bruto");
            Assert.AreEqual(5.01m, resultado.Apuntes[1].ImporteMovimiento,
                "Segundo apunte: comisión");
            Assert.AreEqual("17", resultado.Apuntes[1].ConceptoComun,
                "Comisión debe tener concepto 17 (Varios)");
        }

        [TestMethod]
        public void Convertir_SinComision_GeneraUnSoloApunte()
        {
            // Retirada: comisión = 0
            string csv = CrearCsvConUnaLinea(
                descripcion: "Retirada iniciada por el usuario",
                bruto: "-4.000,00", comision: "0,00", neto: "-4.000,00", saldo: "366,11",
                idTransaccion: "9C5", nombre: "");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual(1, resultado.Apuntes.Count,
                "Sin comisión debe generar solo 1 apunte");
        }

        #endregion

        #region Filtrado de divisas no-EUR

        [TestMethod]
        public void Convertir_LineaEnUSD_SeFiltra()
        {
            // Las líneas en USD no deben generar apuntes (la cuenta es EUR)
            string cabecera = CrearCabeceraCsv();
            string lineaUSD = "\"13/7/2025\",\"08:35:20\",\"Europe/Berlin\",\"Pago con Pago de usuario de BillPay preaprobado\",\"USD\",\"-276,00\",\"0,00\",\"-276,00\",\"-276,00\",\"2S153777KB4947103\",\"paypal@mailchimp.com\",\"MailChimp\",\"\",\"\",\"0,00\",\"0,00\",\"56661209-23270113\",\"B-2HN06547U12102536\"";
            string lineaEUR = "\"1/7/2025\",\"15:37:32\",\"Europe/Berlin\",\"Pago con Pago exprés\",\"EUR\",\"160,71\",\"-5,01\",\"155,70\",\"606,37\",\"6GB9514155364603V\",\"sara@test.com\",\"Sara Test\",\"\",\"\",\"0,00\",\"-27,86\",\"\",\"\"";
            string csv = $"{cabecera}\n{lineaEUR}\n{lineaUSD}";

            var resultado = _convertidor.Convertir(csv);

            // Solo la línea EUR debe generar apuntes (2: bruto + comisión)
            Assert.IsTrue(resultado.Apuntes.All(a => a.ImporteEquivalencia.ClaveDivisaOrigen == "EUR"),
                "Solo deben existir apuntes en EUR");
        }

        #endregion

        #region Información extendida (registros concepto)

        [TestMethod]
        public void Convertir_IncluyeEmailEnRegistrosConcepto()
        {
            string csv = CrearCsvConUnaLinea(
                bruto: "100,00", comision: "-3,00", neto: "97,00", saldo: "97,00",
                idTransaccion: "ABC", nombre: "Sara Gomez",
                email: "sara@example.com");

            var resultado = _convertidor.Convertir(csv);

            var conceptos = resultado.Apuntes[0].RegistrosConcepto;
            string todosConceptos = string.Join(" ", conceptos.Select(c => c.ConceptoCompleto));

            Assert.IsTrue(todosConceptos.Contains("sara@example.com"),
                $"Los registros de concepto deben incluir el email. Contenido: {todosConceptos}");
        }

        [TestMethod]
        public void Convertir_IncluyeNombreEnRegistrosConcepto()
        {
            string csv = CrearCsvConUnaLinea(
                bruto: "100,00", comision: "-3,00", neto: "97,00", saldo: "97,00",
                idTransaccion: "ABC", nombre: "Veronica Sara Ghiran",
                email: "test@test.com");

            var resultado = _convertidor.Convertir(csv);

            var conceptos = resultado.Apuntes[0].RegistrosConcepto;
            string todosConceptos = string.Join(" ", conceptos.Select(c => c.ConceptoCompleto));

            Assert.IsTrue(todosConceptos.Contains("Veronica Sara Ghiran"),
                $"Los registros de concepto deben incluir el nombre. Contenido: {todosConceptos}");
        }

        #endregion

        #region Cabecera y pie

        [TestMethod]
        public void Convertir_CabeceraYPie_TotalesCorrectos()
        {
            string cabecera = CrearCabeceraCsv();
            string linea1 = "\"1/7/2025\",\"15:37:32\",\"Europe/Berlin\",\"Pago con Pago exprés\",\"EUR\",\"160,71\",\"-5,01\",\"155,70\",\"606,37\",\"6GB9514155364603V\",\"sara@test.com\",\"Sara Test\",\"\",\"\",\"0,00\",\"-27,86\",\"\",\"\"";
            string linea2 = "\"2/7/2025\",\"13:44:36\",\"Europe/Berlin\",\"Pago con Pago exprés\",\"EUR\",\"131,30\",\"-4,16\",\"127,14\",\"733,51\",\"3WB15734TL8687408\",\"test@test.com\",\"Lluis Test\",\"\",\"\",\"0,00\",\"-22,75\",\"\",\"\"";
            string csv = $"{cabecera}\n{linea1}\n{linea2}";

            var resultado = _convertidor.Convertir(csv);

            // Cabecera
            Assert.AreEqual(new DateTime(2025, 7, 1), resultado.Cabecera.FechaInicial);
            Assert.AreEqual(new DateTime(2025, 7, 2), resultado.Cabecera.FechaFinal);
            Assert.AreEqual("PYPL", resultado.Cabecera.ClaveEntidad);
            Assert.AreEqual("EUR", resultado.Cabecera.ClaveDivisa);

            // Pie - Los importes haber son los brutos + comisiones como debe
            decimal totalHaber = 160.71m + 131.30m; // brutos positivos
            decimal totalDebe = 5.01m + 4.16m; // comisiones
            Assert.AreEqual(totalHaber, resultado.FinalCuenta.TotalImportesHaber, 0.01m,
                "Total haber debe sumar los brutos");
            Assert.AreEqual(totalDebe, resultado.FinalCuenta.TotalImportesDebe, 0.01m,
                "Total debe debe sumar las comisiones");
        }

        [TestMethod]
        public void Convertir_FechasRango_CalculaMinMax()
        {
            string cabecera = CrearCabeceraCsv();
            string linea1 = "\"15/7/2025\",\"10:00:00\",\"Europe/Berlin\",\"Pago con Pago exprés\",\"EUR\",\"50,00\",\"-1,50\",\"48,50\",\"48,50\",\"AAA\",\"\",\"Test\",\"\",\"\",\"0,00\",\"0,00\",\"\",\"\"";
            string linea2 = "\"3/7/2025\",\"10:00:00\",\"Europe/Berlin\",\"Pago con Pago exprés\",\"EUR\",\"30,00\",\"-1,00\",\"29,00\",\"77,50\",\"BBB\",\"\",\"Test\",\"\",\"\",\"0,00\",\"0,00\",\"\",\"\"";
            string linea3 = "\"25/7/2025\",\"10:00:00\",\"Europe/Berlin\",\"Pago con Pago exprés\",\"EUR\",\"20,00\",\"-0,80\",\"19,20\",\"96,70\",\"CCC\",\"\",\"Test\",\"\",\"\",\"0,00\",\"0,00\",\"\",\"\"";
            string csv = $"{cabecera}\n{linea1}\n{linea2}\n{linea3}";

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual(new DateTime(2025, 7, 3), resultado.Cabecera.FechaInicial);
            Assert.AreEqual(new DateTime(2025, 7, 25), resultado.Cabecera.FechaFinal);
        }

        #endregion

        #region End-to-end con datos reales

        [TestMethod]
        public void Convertir_FicheroEjemploReal_ParseaCorrectamente()
        {
            string csv = @"""Fecha"",""Hora"",""Zona horaria"",""Descripción"",""Divisa"",""Bruto "",""Comisión "",""Neto"",""Saldo"",""Id. de transacción"",""Correo electrónico del remitente"",""Nombre"",""Nombre del banco"",""Cuenta bancaria"",""Importe de envío y manipulación"",""Impuesto de ventas"",""Id. de factura"",""Id. de referencia de trans.""
""1/7/2025"",""15:37:32"",""Europe/Berlin"",""Pago con Pago exprés"",""EUR"",""160,71"",""-5,01"",""155,70"",""606,37"",""6GB9514155364603V"",""sara_ion@yahoo.com"",""Veronica Sara Ghiran"","""","""",""0,00"",""-27,86"","""",""""
""29/7/2025"",""09:23:05"",""Europe/Berlin"",""Retirada iniciada por el usuario"",""EUR"",""-4.000,00"",""0,00"",""-4.000,00"",""366,11"",""9C5250184V339245T"","""","""",""CaixaBank, S.A."",""3554"",""0,00"",""0,00"","""",""""
""13/7/2025"",""08:35:20"",""Europe/Berlin"",""Pago con Pago de usuario de BillPay preaprobado"",""USD"",""-276,00"",""0,00"",""-276,00"",""-276,00"",""2S153777KB4947103"",""paypal@mailchimp.com"",""MailChimp"","""","""",""0,00"",""0,00"",""56661209-23270113"",""B-2HN06547U12102536""
""13/7/2025"",""08:35:20"",""Europe/Berlin"",""Conversión de divisas general"",""USD"",""276,00"",""0,00"",""276,00"",""0,00"",""40M004020G622010U"","""","""","""","""",""0,00"",""0,00"",""56661209-23270113"",""2S153777KB4947103""";

            var resultado = _convertidor.Convertir(csv);

            // Solo líneas EUR generan apuntes
            Assert.IsTrue(resultado.Apuntes.Count > 0, "Debe haber apuntes");
            Assert.IsTrue(resultado.Apuntes.All(a => a.ImporteEquivalencia.ClaveDivisaOrigen == "EUR"),
                "Solo deben existir apuntes en EUR");

            // Línea 1: pago exprés con comisión → 2 apuntes
            var apunteBruto = resultado.Apuntes.First(a => a.ImporteMovimiento == 160.71m);
            Assert.AreEqual("02", apunteBruto.ConceptoComun, "Pago exprés = concepto 02");
            Assert.AreEqual("2", apunteBruto.ClaveDebeOHaberMovimiento, "Ingreso = Haber");
            Assert.AreEqual("6GB9514155", apunteBruto.NumeroDocumento, "Id truncado a 10 chars");

            var apunteComision = resultado.Apuntes.First(a => a.ImporteMovimiento == 5.01m);
            Assert.AreEqual("17", apunteComision.ConceptoComun, "Comisión = concepto 17");
            Assert.AreEqual("1", apunteComision.ClaveDebeOHaberMovimiento, "Comisión = Debe");

            // Línea 2: retirada sin comisión → 1 apunte
            var apunteRetirada = resultado.Apuntes.First(a => a.ImporteMovimiento == 4000.00m);
            Assert.AreEqual("03", apunteRetirada.ConceptoComun, "Retirada = concepto 03");
            Assert.AreEqual("1", apunteRetirada.ClaveDebeOHaberMovimiento, "Retirada = Debe");

            // Fechas
            Assert.AreEqual(new DateTime(2025, 7, 1), resultado.Cabecera.FechaInicial);
            Assert.AreEqual(new DateTime(2025, 7, 29), resultado.Cabecera.FechaFinal);

            // Saldo inicial: primera línea Saldo(606,37) - Neto(155,70) = 450,67
            Assert.AreEqual(450.67m, resultado.Cabecera.ImporteSaldoInicial);
        }

        [TestMethod]
        public void Convertir_IdTransaccion_TruncaA10Caracteres()
        {
            string csv = CrearCsvConUnaLinea(
                bruto: "100,00", comision: "0,00", neto: "100,00", saldo: "100,00",
                idTransaccion: "6GB9514155364603V", nombre: "Test");

            var resultado = _convertidor.Convertir(csv);

            Assert.AreEqual("6GB9514155", resultado.Apuntes[0].NumeroDocumento,
                "Id de transacción debe truncarse a 10 caracteres");
        }

        #endregion

        #region Helpers

        private static string CrearCabeceraCsv()
        {
            return "\"Fecha\",\"Hora\",\"Zona horaria\",\"Descripción\",\"Divisa\",\"Bruto \",\"Comisión \",\"Neto\",\"Saldo\",\"Id. de transacción\",\"Correo electrónico del remitente\",\"Nombre\",\"Nombre del banco\",\"Cuenta bancaria\",\"Importe de envío y manipulación\",\"Impuesto de ventas\",\"Id. de factura\",\"Id. de referencia de trans.\"";
        }

        private static string CrearCsvConUnaLinea(
            string fecha = "1/7/2025",
            string hora = "15:37:32",
            string descripcion = "Pago con Pago exprés",
            string divisa = "EUR",
            string bruto = "100,00",
            string comision = "-3,00",
            string neto = "97,00",
            string saldo = "97,00",
            string idTransaccion = "ABC123TEST",
            string email = "",
            string nombre = "Test User",
            string nombreBanco = "",
            string cuentaBancaria = "",
            string envio = "0,00",
            string impuesto = "0,00",
            string idFactura = "",
            string idReferencia = "")
        {
            string cabecera = CrearCabeceraCsv();
            string linea = $"\"{fecha}\",\"{hora}\",\"Europe/Berlin\",\"{descripcion}\",\"{divisa}\",\"{bruto}\",\"{comision}\",\"{neto}\",\"{saldo}\",\"{idTransaccion}\",\"{email}\",\"{nombre}\",\"{nombreBanco}\",\"{cuentaBancaria}\",\"{envio}\",\"{impuesto}\",\"{idFactura}\",\"{idReferencia}\"";
            return $"{cabecera}\n{linea}";
        }

        #endregion
    }
}
