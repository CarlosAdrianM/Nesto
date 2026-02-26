using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nesto.Modulos.Cajas.Bancos
{
    /// <summary>
    /// Convierte el nuevo formato CSV de PayPal (informe de Actividad) a Cuaderno43.
    /// Issue Nesto#303: El informe antiguo fue retirado por PayPal.
    ///
    /// Formato:
    /// "Fecha","Hora","Zona horaria","Descripción","Divisa","Bruto ","Comisión ","Neto","Saldo",
    /// "Id. de transacción","Correo electrónico del remitente","Nombre",...
    /// </summary>
    internal class ConvertidorPaypalNuevo : IConvertidorFormatoBancario
    {
        private static readonly CultureInfo CulturaEspañola = new("es-ES");

        private static readonly Dictionary<string, string> MapeoConceptos = new()
        {
            { "Pago con Pago exprés", "02" },
            { "Retirada", "03" },
            { "Conversión de divisas", "13" },
            { "BillPay", "03" }
        };

        public bool PuedeConvertir(string contenido)
        {
            if (string.IsNullOrEmpty(contenido))
            {
                return false;
            }
            string primeraLinea = contenido.Split('\n')[0].Trim();
            return primeraLinea.Contains("\"Fecha\"") && primeraLinea.Contains("\"Zona horaria\"");
        }

        public ContenidoCuaderno43 Convertir(string contenido)
        {
            ContenidoCuaderno43 resultado = new();
            List<string> lineas = contenido.Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lineas.Count < 2)
            {
                throw new Exception("El archivo CSV no contiene datos suficientes.");
            }

            // Parsear cabecera para obtener índices de columnas
            List<string> cabeceras = ParsearLineaCsv(lineas[0]);
            int idxFecha = BuscarColumna(cabeceras, "Fecha");
            int idxDescripcion = BuscarColumna(cabeceras, "Descripción");
            int idxDivisa = BuscarColumna(cabeceras, "Divisa");
            int idxBruto = BuscarColumna(cabeceras, "Bruto");
            int idxComision = BuscarColumna(cabeceras, "Comisión");
            int idxNeto = BuscarColumna(cabeceras, "Neto");
            int idxSaldo = BuscarColumna(cabeceras, "Saldo");
            int idxTransaccion = BuscarColumna(cabeceras, "Id. de transacción");
            int idxEmail = BuscarColumna(cabeceras, "Correo electrónico del remitente");
            int idxNombre = BuscarColumna(cabeceras, "Nombre");

            // Parsear líneas de datos (solo EUR)
            List<LineaPaypalNuevo> lineasDatos = [];
            for (int i = 1; i < lineas.Count; i++)
            {
                List<string> campos = ParsearLineaCsv(lineas[i]);
                if (campos.Count <= Math.Max(idxSaldo, idxTransaccion))
                {
                    continue;
                }

                string divisa = campos[idxDivisa];
                if (divisa != "EUR")
                {
                    continue;
                }

                lineasDatos.Add(new LineaPaypalNuevo
                {
                    Fecha = DateTime.ParseExact(campos[idxFecha], "d/M/yyyy", CulturaEspañola),
                    Descripcion = campos[idxDescripcion],
                    Bruto = ParsearImporte(campos[idxBruto]),
                    Comision = ParsearImporte(campos[idxComision]),
                    Neto = ParsearImporte(campos[idxNeto]),
                    Saldo = ParsearImporte(campos[idxSaldo]),
                    IdTransaccion = campos[idxTransaccion],
                    Email = idxEmail < campos.Count ? campos[idxEmail] : "",
                    Nombre = idxNombre < campos.Count ? campos[idxNombre] : ""
                });
            }

            if (lineasDatos.Count == 0)
            {
                throw new Exception("No se encontraron líneas EUR en el fichero.");
            }

            // Saldo inicial = Saldo de la primera línea - Neto de la primera línea
            decimal saldoInicial = lineasDatos[0].Saldo - lineasDatos[0].Neto;

            // Fechas
            DateTime fechaInicial = lineasDatos.Min(l => l.Fecha);
            DateTime fechaFinal = lineasDatos.Max(l => l.Fecha);

            // Generar apuntes
            List<ApunteBancarioDTO> apuntes = [];
            decimal totalDebe = 0;
            decimal totalHaber = 0;
            int countDebe = 0;
            int countHaber = 0;

            foreach (var linea in lineasDatos)
            {
                string conceptoComun = ObtenerConceptoComun(linea.Descripcion);
                string numeroDocumento = linea.IdTransaccion.Length > 10
                    ? linea.IdTransaccion[..10]
                    : linea.IdTransaccion;

                // Registros de concepto con email y nombre
                List<RegistroComplementarioConcepto> registrosConcepto = CrearRegistrosConcepto(
                    linea.Descripcion, linea.Email, linea.Nombre);

                // Apunte principal (bruto)
                bool esDebe = linea.Bruto < 0;
                decimal importeBruto = Math.Abs(linea.Bruto);

                apuntes.Add(new ApunteBancarioDTO
                {
                    CodigoRegistroPrincipal = "22",
                    ClaveOficinaOrigen = "0000",
                    FechaOperacion = linea.Fecha,
                    FechaValor = linea.Fecha,
                    ConceptoComun = conceptoComun,
                    ConceptoPropio = "PPA",
                    ClaveDebeOHaberMovimiento = esDebe ? "1" : "2",
                    ImporteMovimiento = importeBruto,
                    NumeroDocumento = numeroDocumento,
                    Referencia1 = "",
                    Referencia2 = "",
                    EstadoPunteo = EstadoPunteo.SinPuntear,
                    RegistrosConcepto = registrosConcepto,
                    ImporteEquivalencia = new RegistroComplementarioEquivalencia
                    {
                        CodigoRegistroEquivalencia = "60",
                        CodigoDato = "02",
                        ClaveDivisaOrigen = "EUR",
                        ImporteEquivalencia = importeBruto
                    }
                });

                if (esDebe)
                {
                    totalDebe += importeBruto;
                    countDebe++;
                }
                else
                {
                    totalHaber += importeBruto;
                    countHaber++;
                }

                // Apunte de comisión (si comisión ≠ 0)
                if (linea.Comision != 0)
                {
                    decimal importeComision = Math.Abs(linea.Comision);
                    bool comisionEsDebe = linea.Comision < 0;

                    apuntes.Add(new ApunteBancarioDTO
                    {
                        CodigoRegistroPrincipal = "22",
                        ClaveOficinaOrigen = "0000",
                        FechaOperacion = linea.Fecha,
                        FechaValor = linea.Fecha,
                        ConceptoComun = "17",
                        ConceptoPropio = "PPA",
                        ClaveDebeOHaberMovimiento = comisionEsDebe ? "1" : "2",
                        ImporteMovimiento = importeComision,
                        NumeroDocumento = numeroDocumento,
                        Referencia1 = "",
                        Referencia2 = "",
                        EstadoPunteo = EstadoPunteo.SinPuntear,
                        RegistrosConcepto = registrosConcepto,
                        ImporteEquivalencia = new RegistroComplementarioEquivalencia
                        {
                            CodigoRegistroEquivalencia = "60",
                            CodigoDato = "02",
                            ClaveDivisaOrigen = "EUR",
                            ImporteEquivalencia = importeComision
                        }
                    });

                    if (comisionEsDebe)
                    {
                        totalDebe += importeComision;
                        countDebe++;
                    }
                    else
                    {
                        totalHaber += importeComision;
                        countHaber++;
                    }
                }
            }

            // Cabecera
            resultado.Cabecera = new RegistroCabeceraCuenta
            {
                CodigoRegistroCabecera = "11",
                ClaveEntidad = "PYPL",
                ClaveOficina = "F55F",
                NumeroCuenta = "2LRZ5CMS8 ",
                FechaInicial = fechaInicial,
                FechaFinal = fechaFinal,
                ClaveDebeOHaber = saldoInicial >= 0 ? "2" : "1",
                ImporteSaldoInicial = Math.Abs(saldoInicial),
                ClaveDivisa = "EUR",
                ModalidadInformacion = "2",
                NombreAbreviado = "PAYPAL",
                CampoLibreCabecera = "".PadRight(26)
            };

            resultado.Apuntes = apuntes;

            // Final de cuenta
            resultado.FinalCuenta = new RegistroFinalCuenta
            {
                CodigoRegistroFinal = "33",
                ClaveEntidadFinal = "PYPL",
                ClaveOficinaFinal = "F55F",
                NumeroCuentaFinal = "2LRZ5CMS8 ",
                NumeroApuntesDebe = countDebe,
                TotalImportesDebe = totalDebe,
                NumeroApuntesHaber = countHaber,
                TotalImportesHaber = totalHaber,
                CodigoSaldoFinal = saldoInicial + totalHaber - totalDebe >= 0 ? "2" : "1",
                SaldoFinal = Math.Abs(saldoInicial + totalHaber - totalDebe),
                ClaveDivisaFinal = "EUR",
                CampoLibreFinal = "".PadRight(4)
            };

            // Final de fichero
            resultado.FinalFichero = new RegistroFinalFichero
            {
                CodigoRegistroFinFichero = "88",
                Nueves = "999",
                NumeroRegistros = apuntes.Count + 2,
                CampoLibreFinFichero = "".PadRight(79)
            };

            return resultado;
        }

        private static int BuscarColumna(List<string> cabeceras, string nombre)
        {
            // Las cabeceras pueden tener espacios al final (ej: "Bruto " → buscar por StartsWith)
            for (int i = 0; i < cabeceras.Count; i++)
            {
                if (cabeceras[i].Trim().StartsWith(nombre, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static decimal ParsearImporte(string valor)
        {
            // Formato español: punto para miles, coma para decimales (ej: "-4.000,00")
            string limpio = valor.Trim().Replace(".", "").Replace(",", ".");
            return decimal.Parse(limpio, CultureInfo.InvariantCulture);
        }

        private static string ObtenerConceptoComun(string descripcion)
        {
            foreach (var kvp in MapeoConceptos)
            {
                if (descripcion.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }
            return "99";
        }

        private static List<RegistroComplementarioConcepto> CrearRegistrosConcepto(
            string descripcion, string email, string nombre)
        {
            const int maxLongitudCampo = 38;
            const int maxCamposPorRegistro = 2;
            const int maxRegistros = 3;

            // Construir texto completo: descripción + email + nombre
            string textoCompleto = descripcion;
            if (!string.IsNullOrEmpty(email))
            {
                textoCompleto += " " + email;
            }
            if (!string.IsNullOrEmpty(nombre))
            {
                textoCompleto += " " + nombre;
            }

            int maxTotalLength = maxLongitudCampo * maxCamposPorRegistro * maxRegistros;
            string textoTruncado = textoCompleto.Length > maxTotalLength
                ? textoCompleto[..maxTotalLength]
                : textoCompleto;

            List<RegistroComplementarioConcepto> registros = [];

            for (int j = 0; j < textoTruncado.Length; j += maxLongitudCampo * maxCamposPorRegistro)
            {
                string fragmento = textoTruncado.Substring(j,
                    Math.Min(maxLongitudCampo * maxCamposPorRegistro, textoTruncado.Length - j));

                string concepto = fragmento.Length > maxLongitudCampo
                    ? fragmento[..maxLongitudCampo]
                    : fragmento;
                string concepto2 = fragmento.Length > maxLongitudCampo
                    ? fragmento[maxLongitudCampo..]
                    : string.Empty;

                registros.Add(new RegistroComplementarioConcepto
                {
                    CodigoRegistroConcepto = "23",
                    CodigoDato = "01",
                    Concepto = concepto,
                    Concepto2 = concepto2
                });

                if (registros.Count >= maxRegistros)
                {
                    break;
                }
            }

            return registros;
        }

        /// <summary>
        /// Parsea una línea CSV respetando comillas.
        /// </summary>
        private static List<string> ParsearLineaCsv(string linea)
        {
            List<string> campos = [];
            int startIndex = 0;
            bool inQuotes = false;

            for (int j = 0; j < linea.Length; j++)
            {
                if (linea[j] == '"')
                {
                    inQuotes = !inQuotes;
                }

                if (j == linea.Length - 1 || (linea[j] == ',' && !inQuotes))
                {
                    string campo = linea.Substring(startIndex,
                        j - startIndex + (j == linea.Length - 1 ? 1 : 0));
                    campo = campo.Trim('"', ' ');
                    campos.Add(campo);
                    startIndex = j + 1;
                }
            }

            return campos;
        }

        private class LineaPaypalNuevo
        {
            public DateTime Fecha { get; set; }
            public string Descripcion { get; set; } = "";
            public decimal Bruto { get; set; }
            public decimal Comision { get; set; }
            public decimal Neto { get; set; }
            public decimal Saldo { get; set; }
            public string IdTransaccion { get; set; } = "";
            public string Email { get; set; } = "";
            public string Nombre { get; set; } = "";
        }
    }
}
