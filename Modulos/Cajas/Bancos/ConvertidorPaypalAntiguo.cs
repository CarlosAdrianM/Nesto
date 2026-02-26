using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nesto.Modulos.Cajas.Bancos
{
    /// <summary>
    /// Convierte el formato antiguo de PayPal (RS/RD) a Cuaderno43.
    /// Este formato fue retirado por PayPal (Issue Nesto#303), pero se mantiene
    /// por compatibilidad con ficheros históricos.
    /// </summary>
    internal class ConvertidorPaypalAntiguo : IConvertidorFormatoBancario
    {
        public bool PuedeConvertir(string contenido)
        {
            if (string.IsNullOrEmpty(contenido))
            {
                return false;
            }
            string primeraLinea = contenido.Split('\n')[0].Trim();
            return primeraLinea.Contains("\"RS\"");
        }

        public ContenidoCuaderno43 Convertir(string contenido)
        {
            ContenidoCuaderno43 resultado = new();
            List<string> lines = contenido.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (lines.Count < 2)
            {
                throw new Exception("El archivo CSV no contiene datos suficientes.");
            }

            // Inicializar valores por defecto
            DateTime fechaInicial = DateTime.MaxValue;
            DateTime fechaFinal = DateTime.MinValue;
            decimal saldoInicial = 0;
            decimal totalDebe = 0;
            decimal totalHaber = 0;
            int countDebe = 0;
            int countHaber = 0;
            string claveDivisa = "EUR";
            List<ApunteBancarioDTO> apuntes = [];
            string idCuenta = string.Empty;

            // Obtener el saldo inicial de la sección de saldos
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                List<string> parts = line.Split(',').Select(p => p.Trim('"')).ToList();

                if (parts.Count >= 3 && parts[0] == "RS" && parts[2] == "OPENING" && parts[3] == "EUR")
                {
                    if (parts.Count >= 6 && decimal.TryParse(parts[4].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal saldo))
                    {
                        saldoInicial = saldo;
                        idCuenta = parts[1];
                        break;
                    }
                }
            }

            // Buscar la línea de encabezado de los datos de transacciones
            int headerLineIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("\"RD\"") && lines[i + 1].StartsWith("\"RD\""))
                {
                    headerLineIndex = i;
                    break;
                }
            }

            if (headerLineIndex == -1)
            {
                throw new Exception("No se pudo encontrar el encabezado de los datos de transacciones.");
            }

            // Extraer los índices de las columnas basados en el encabezado
            List<string> headers = lines[headerLineIndex].Split(',').Select(h => h.Trim('"')).ToList();

            // Columnas esenciales para el procesamiento
            int idRegIndex = headers.IndexOf("Id. de registro");
            int fechaIndex = headers.IndexOf("Creado a las");
            int importeIndex = headers.IndexOf("Importe neto");
            int importeBrutoIndex = headers.IndexOf("Importe bruto");
            int tipoRegIndex = headers.IndexOf("Tipo de registro");
            int subtipoRegIndex = headers.IndexOf("Subtipo de registro");
            int divisaIndex = headers.IndexOf("Divisa del registro");
            int descriptionIndex = headers.IndexOf("Campo personalizado");
            int nombreIndex = headers.IndexOf("Nombre");
            int instrumentoPagoIndex = headers.IndexOf("Tipo de instrumento de pago");
            int subtipoInstrumentoPagoIndex = headers.IndexOf("Subtipo de instrumento de pago");

            // Verificar que las columnas necesarias estén presentes
            if (fechaIndex == -1 || importeIndex == -1 || tipoRegIndex == -1 || divisaIndex == -1)
            {
                throw new Exception("El archivo CSV no tiene las columnas requeridas.");
            }

            // Procesar las líneas de transacciones
            for (int i = headerLineIndex + 1; i < lines.Count; i++)
            {
                string line = lines[i];

                // Saltarse líneas que no sean datos de transacciones
                if (!line.StartsWith("\"RD\""))
                {
                    continue;
                }

                // Separar la línea en campos, respetando comillas
                List<string> fields = [];
                int startIndex = 0;
                bool inQuotes = false;

                for (int j = 0; j < line.Length; j++)
                {
                    if (line[j] == '"')
                    {
                        inQuotes = !inQuotes;
                    }

                    if (j == line.Length - 1 || (line[j] == ',' && !inQuotes))
                    {
                        string field = line.Substring(startIndex, j - startIndex + (j == line.Length - 1 ? 1 : 0));
                        field = field.Trim('"', ' ');
                        fields.Add(field);
                        startIndex = j + 1;
                    }
                }

                // Asegurarse de que hay suficientes campos
                if (fields.Count <= Math.Max(Math.Max(fechaIndex, importeIndex), Math.Max(tipoRegIndex, divisaIndex)))
                {
                    continue;
                }

                // Extraer la fecha
                string fechaStr = fields[fechaIndex];
                if (string.IsNullOrEmpty(fechaStr) || !fechaStr.Contains(" "))
                {
                    continue;
                }

                string[] fechaParts = fechaStr.Split(' ');
                if (!DateTime.TryParseExact(fechaParts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha))
                {
                    continue;
                }

                // Extraer el importe
                if (!decimal.TryParse(fields[importeIndex].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal importe))
                {
                    continue;
                }

                // Extraer el importe bruto
                if (!decimal.TryParse(fields[importeBrutoIndex].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal importeBruto))
                {
                    continue;
                }

                // Construir la descripción
                string tipoRegistro = fields[tipoRegIndex];
                string subtipoRegistro = fields[subtipoRegIndex];
                string descripcion = string.Empty;

                if (descriptionIndex != -1 && descriptionIndex < fields.Count && !string.IsNullOrEmpty(fields[descriptionIndex]))
                {
                    descripcion = fields[descriptionIndex];
                }

                // Añadir el nombre si está disponible
                descripcion = nombreIndex != -1 && nombreIndex < fields.Count && !string.IsNullOrEmpty(fields[nombreIndex])
                    ? string.IsNullOrEmpty(descripcion) ?
                        $"{tipoRegistro} - {fields[nombreIndex]}" :
                        $"{descripcion} - {fields[nombreIndex]}"
                    : string.IsNullOrEmpty(descripcion) ?
                        $"{tipoRegistro} {(string.IsNullOrEmpty(subtipoRegistro) ? "" : $"({subtipoRegistro})")}" :
                        descripcion;

                string idRegistro = fields[idRegIndex];
                string divisa = fields[divisaIndex];

                // Actualizar fechas inicial y final
                if (fecha < fechaInicial)
                {
                    fechaInicial = fecha;
                }

                if (fecha > fechaFinal)
                {
                    fechaFinal = fecha;
                }

                // Actualizar totales
                if (importe < 0)
                {
                    totalDebe += Math.Abs(importe);
                    countDebe++;
                }
                else
                {
                    totalHaber += importe;
                    countHaber++;
                }

                // Definimos la longitud máxima por campo y por registro
                int maxLengthPerField = 38;
                int maxFieldsPerRecord = 2;
                int maxRecords = 3;

                // Calculamos la longitud máxima que podemos almacenar
                int maxTotalLength = maxLengthPerField * maxFieldsPerRecord * maxRecords;

                // Truncamos la descripción si es necesario
                string truncatedDescription = descripcion.Length > maxTotalLength ? descripcion[..maxTotalLength] : descripcion;

                // Creamos una lista para almacenar los registros de concepto
                List<RegistroComplementarioConcepto> registrosConcepto = [];

                // Iteramos sobre la descripción truncada y la dividimos en fragmentos de 38 caracteres
                for (int j = 0; j < truncatedDescription.Length; j += maxLengthPerField * maxFieldsPerRecord)
                {
                    // Obtenemos un fragmento de la descripción
                    string fragment = truncatedDescription.Substring(j, Math.Min(maxLengthPerField * maxFieldsPerRecord, truncatedDescription.Length - j));

                    // Dividimos el fragmento en dos partes (Concepto y Concepto2)
                    string concepto = fragment.Length > maxLengthPerField ? fragment[..maxLengthPerField] : fragment;
                    string concepto2 = fragment.Length > maxLengthPerField ? fragment[maxLengthPerField..] : string.Empty;

                    // Añadimos un nuevo registro de concepto
                    registrosConcepto.Add(new RegistroComplementarioConcepto
                    {
                        CodigoRegistroConcepto = "23",
                        CodigoDato = "01",
                        Concepto = concepto,
                        Concepto2 = concepto2
                    });

                    // Si ya hemos añadido 3 registros, salimos del bucle
                    if (registrosConcepto.Count >= maxRecords)
                    {
                        break;
                    }
                }

                // Crear la cadena de instrumentos de pago
                string instrumentosPago = $"{fields[instrumentoPagoIndex]}({fields[subtipoInstrumentoPagoIndex]})";

                // Dividir la cadena en dos partes: Concepto y Concepto2
                string conceptoInstrumento = instrumentosPago.Length > maxLengthPerField ? instrumentosPago[..maxLengthPerField] : instrumentosPago;
                string concepto2Instrumento = instrumentosPago.Length > maxLengthPerField ? instrumentosPago.Substring(maxLengthPerField, Math.Min(maxLengthPerField, instrumentosPago.Length - maxLengthPerField)) : string.Empty;

                // Crear el registro de concepto
                RegistroComplementarioConcepto registroInstrumentoPago = new()
                {
                    CodigoRegistroConcepto = "23",
                    CodigoDato = "01",
                    Concepto = conceptoInstrumento,
                    Concepto2 = concepto2Instrumento
                };
                registrosConcepto.Add(registroInstrumentoPago);

                // Definir un diccionario para mapear tipoRegistro a ConceptoComun
                Dictionary<string, string> conceptoComunMap = new()
                {
                    { "Sale", "02" },
                    { "Refund", "98" },
                    { "Other Payments", "03" },
                    { "Currency conversion", "13" }
                };

                // Función para obtener el ConceptoComun basado en tipoRegistro
                string GetConceptoComun(string tipoRegistro)
                {
                    return conceptoComunMap.ContainsKey(tipoRegistro) ? conceptoComunMap[tipoRegistro] : "99";
                }

                // Agregar el apunte
                apuntes.Add(new ApunteBancarioDTO
                {
                    CodigoRegistroPrincipal = "22",
                    ClaveOficinaOrigen = "0000",
                    FechaOperacion = fecha,
                    FechaValor = fecha,
                    ConceptoComun = GetConceptoComun(tipoRegistro),
                    ConceptoPropio = "PPA",
                    ClaveDebeOHaberMovimiento = importeBruto >= 0 ? "2" : "1", // 1 debe, 2 haber
                    ImporteMovimiento = Math.Abs(importeBruto),
                    NumeroDocumento = idRegistro.Length > 10 ? idRegistro[..10] : idRegistro,
                    Referencia1 = "",
                    Referencia2 = importe.ToString("C"),
                    EstadoPunteo = EstadoPunteo.SinPuntear,
                    RegistrosConcepto = registrosConcepto,
                    ImporteEquivalencia = new RegistroComplementarioEquivalencia
                    {
                        CodigoRegistroEquivalencia = "60",
                        CodigoDato = "02",
                        ClaveDivisaOrigen = divisa,
                        ImporteEquivalencia = Math.Abs(importeBruto)
                    }
                });

                if (importe != importeBruto)
                {
                    decimal importeComision = -(importeBruto - importe);
                    apuntes.Add(new ApunteBancarioDTO
                    {
                        CodigoRegistroPrincipal = "22",
                        ClaveOficinaOrigen = "0000",
                        FechaOperacion = fecha,
                        FechaValor = fecha,
                        ConceptoComun = "17", // Varios
                        ConceptoPropio = "PPA",
                        ClaveDebeOHaberMovimiento = importeComision >= 0 ? "2" : "1",
                        ImporteMovimiento = Math.Abs(importeComision),
                        NumeroDocumento = idRegistro.Length > 10 ? idRegistro[..10] : idRegistro,
                        Referencia1 = importeBruto.ToString("C"),
                        Referencia2 = importe.ToString("C"),
                        EstadoPunteo = EstadoPunteo.SinPuntear,
                        RegistrosConcepto = registrosConcepto,
                        ImporteEquivalencia = new RegistroComplementarioEquivalencia
                        {
                            CodigoRegistroEquivalencia = "60",
                            CodigoDato = "02",
                            ClaveDivisaOrigen = divisa,
                            ImporteEquivalencia = Math.Abs(importeComision)
                        }
                    });
                }
            }

            // Si no hay fechas límite, utilizar fecha actual
            if (fechaInicial == DateTime.MaxValue)
            {
                fechaInicial = DateTime.Today;
            }

            if (fechaFinal == DateTime.MinValue)
            {
                fechaFinal = DateTime.Today;
            }

            string claveOficina = idCuenta.Length >= 4 ? idCuenta[..4] : idCuenta.PadRight(4);
            string numeroCuenta = idCuenta.Length > 4 ? idCuenta[4..] : "";

            // Completar la información de cabecera
            resultado.Cabecera = new RegistroCabeceraCuenta
            {
                CodigoRegistroCabecera = "11",
                ClaveEntidad = "PYPL",
                ClaveOficina = claveOficina,
                NumeroCuenta = numeroCuenta.PadRight(10)[..10],
                FechaInicial = fechaInicial,
                FechaFinal = fechaFinal,
                ClaveDebeOHaber = saldoInicial >= 0 ? "2" : "1", // 1 Debe, 2 Haber
                ImporteSaldoInicial = Math.Abs(saldoInicial),
                ClaveDivisa = claveDivisa,
                ModalidadInformacion = "2",
                NombreAbreviado = "PAYPAL",
                CampoLibreCabecera = "".PadRight(26)
            };

            // Completar la información de los apuntes
            resultado.Apuntes = apuntes;

            // Completar la información final de cuenta
            resultado.FinalCuenta = new RegistroFinalCuenta
            {
                CodigoRegistroFinal = "33",
                ClaveEntidadFinal = "PYPL",
                ClaveOficinaFinal = claveOficina,
                NumeroCuentaFinal = numeroCuenta.PadRight(10)[..10],
                NumeroApuntesDebe = countDebe,
                TotalImportesDebe = totalDebe,
                NumeroApuntesHaber = countHaber,
                TotalImportesHaber = totalHaber,
                CodigoSaldoFinal = saldoInicial + totalHaber - totalDebe >= 0 ? "2" : "1",
                SaldoFinal = Math.Abs(saldoInicial + totalHaber - totalDebe),
                ClaveDivisaFinal = claveDivisa,
                CampoLibreFinal = "".PadRight(4)
            };

            // Completar la información final de fichero
            resultado.FinalFichero = new RegistroFinalFichero
            {
                CodigoRegistroFinFichero = "88",
                Nueves = "999",
                NumeroRegistros = apuntes.Count + 2,
                CampoLibreFinFichero = "".PadRight(79)
            };

            return resultado;
        }
    }
}
