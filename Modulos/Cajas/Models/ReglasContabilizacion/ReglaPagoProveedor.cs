using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaPagoProveedor : IReglaContabilizacion
    {
        private readonly IBancosService _bancosService;

        public string Nombre => "Pago a proveedor";

        public ReglaPagoProveedor(IBancosService bancosService)
        {
            _bancosService = bancosService;
        }

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.First();
            var apunteContabilidad = apuntesContabilidad.First();
            var importeDescuadre = apuntesBancarios.Sum(b => b.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            string proveedor;
            ExtractoProveedorDTO pagoPendiente;

            if (EsPagoNacional(apunteBancario))
            {
                proveedor = Task.Run(async () => await _bancosService.LeerProveedorPorNombre(apunteBancario.RegistrosConcepto[2].Concepto)).GetAwaiter().GetResult();
                pagoPendiente = Task.Run(async () => await _bancosService.PagoPendienteUnico(proveedor, apunteBancario.ImporteMovimiento)).GetAwaiter().GetResult();
            }
            else if (EsReciboDomiciliado(apunteBancario))
            {
                //proveedor = Task.Run(async () => await _bancosService.LeerProveedorPorNombre(apunteBancario.RegistrosConcepto[0].Concepto.Substring(4))).GetAwaiter().GetResult();
                proveedor = Task.Run(async () => await _bancosService.LeerProveedorPorNif(apunteBancario.RegistrosConcepto[1].Concepto.Substring(7,9))).GetAwaiter().GetResult();
                pagoPendiente = Task.Run(async () => await _bancosService.PagoPendienteUnico(proveedor, apunteBancario.ImporteMovimiento)).GetAwaiter().GetResult();
            }
            else if (EsTransferenciaInternacional(apunteBancario))
            {
                proveedor = Task.Run(async () => await _bancosService.LeerProveedorPorNombre(apunteBancario.RegistrosConcepto[3].Concepto.Trim())).GetAwaiter().GetResult();
                Regex regex = new Regex(@"invoice\s*(nº|)\s+(\b\w+\b)", RegexOptions.IgnoreCase);
                Match match = regex.Match(apunteBancario.RegistrosConcepto[2].ConceptoCompleto);
                string documentoProveedor;
                if (match.Success)
                {
                    documentoProveedor = match.Groups[2].Value;
                }
                else
                {
                    return new ReglaContabilizacionResponse(); // comprobar en EsContabilizable
                }
                pagoPendiente = new ExtractoProveedorDTO()
                {
                    Proveedor = proveedor,
                    Contacto = "0",
                    DocumentoProveedor = documentoProveedor,
                    Documento = documentoProveedor.Length > 10
                                ? documentoProveedor.Substring(documentoProveedor.Length - 10)
                                : documentoProveedor,
                    Delegacion = Constantes.Empresas.DELEGACION_DEFECTO,
                    FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                };
            }
            else
            {
                throw new Exception("No se puede contabilizar el pago único del proveedor");
            }

            if (string.IsNullOrEmpty(proveedor))
            {
                throw new Exception("No se encuentra el proveedor");
            }
            
            if (pagoPendiente is null)
            {
                throw new Exception("No hay pagos pendientes de ese proveedor por ese importe");
            }

            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.TipoApunte = Constantes.TiposApunte.PAGO;
            linea1.TipoCuenta = Constantes.TiposCuenta.PROVEEDOR;
            linea1.Cuenta = pagoPendiente.Proveedor;
            linea1.Contacto = pagoPendiente.Contacto;
            linea1.Concepto = $"N/Pago S/Fra.{pagoPendiente.DocumentoProveedor}";
            if (!string.IsNullOrWhiteSpace(pagoPendiente.Documento) && pagoPendiente.Documento != pagoPendiente.DocumentoProveedor)
            {
                linea1.Concepto += $" - { pagoPendiente.Documento}";
            }
            linea1.Documento = pagoPendiente.Documento;
            linea1.FacturaProveedor = pagoPendiente.DocumentoProveedor;
            linea1.Fecha = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
            linea1.FechaVto = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
            linea1.Delegacion = pagoPendiente.Delegacion;
            linea1.FormaVenta = pagoPendiente.FormaVenta;
            linea1.FormaPago = Constantes.FormasPago.TRANSFERENCIA;
            linea1.Debe = apunteBancario.ImporteMovimiento < 0 ? -apunteBancario.ImporteMovimiento : 0;
            linea1.Haber = apunteBancario.ImporteMovimiento > 0 ? apunteBancario.ImporteMovimiento : 0;
            if (pagoPendiente.Id != 0)
            {
                linea1.Liquidado = pagoPendiente.Id;
            }
            linea1.Contrapartida = banco.CuentaContable;
            lineas.Add(linea1);

            ReglaContabilizacionResponse response = new ReglaContabilizacionResponse
            {
                Lineas = lineas
            };

            return response;
        }


        public bool EsContabilizable(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad)
        {
            if (apuntesBancarios is null || !apuntesBancarios.Any())
            {
                return false;
            }
            var apunteBancario = apuntesBancarios.First();
            
            string proveedor;
            bool liquidacionObligatoria = true;
            if (EsPagoNacional(apunteBancario))
            {
                proveedor = Task.Run(async () => await _bancosService.LeerProveedorPorNombre(apunteBancario.RegistrosConcepto[2].Concepto)).GetAwaiter().GetResult();
            }
            else if (EsReciboDomiciliado(apunteBancario))
            {
                proveedor = Task.Run(async () => await _bancosService.LeerProveedorPorNif(apunteBancario.RegistrosConcepto[1].Concepto.Substring(7,9))).GetAwaiter().GetResult();
            }
            else if (EsTransferenciaInternacional(apunteBancario))
            {
                proveedor = Task.Run(async () => await _bancosService.LeerProveedorPorNombre(apunteBancario.RegistrosConcepto[3].Concepto.Trim())).GetAwaiter().GetResult();
                liquidacionObligatoria = false;
            }
            else
            {
                return false;
            }

            if (string.IsNullOrEmpty(proveedor))
            {
                return false;
            }

            if (liquidacionObligatoria)
            {
                ExtractoProveedorDTO pagoPendiente = Task.Run(async () => await _bancosService.PagoPendienteUnico(proveedor, apunteBancario.ImporteMovimiento)).GetAwaiter().GetResult();
                if (pagoPendiente is null)
                {
                    return false;
                }
            }
            else
            {
                Regex regex = new Regex(@"invoice\s*(nº|)\s+(\b\w+\b)", RegexOptions.IgnoreCase);
                Match match = regex.Match(apunteBancario.RegistrosConcepto[2].ConceptoCompleto);
                if (!match.Success)
                {
                    return false;
                }
            }
            
            return true;
        }

        private static bool EsPagoNacional(ApunteBancarioDTO apunteBancario)
        {
            return apunteBancario.ConceptoComun == "99" &&
                            apunteBancario.ConceptoPropio == "067" &&
                            apunteBancario.RegistrosConcepto != null &&
                            apunteBancario.RegistrosConcepto.Any() &&
                            apunteBancario.RegistrosConcepto[1] != null &&
                            apunteBancario.RegistrosConcepto[1].Concepto.ToLower().Contains("pago");
        }

        private static bool EsReciboDomiciliado(ApunteBancarioDTO apunteBancario)
        {
            return apunteBancario.ConceptoComun == "03" &&
                            (apunteBancario.ConceptoPropio == "038" || apunteBancario.ConceptoPropio == "005" || apunteBancario.ConceptoPropio == "006") &&
                            apunteBancario.RegistrosConcepto != null &&
                            apunteBancario.RegistrosConcepto.Any() &&
                            apunteBancario.RegistrosConcepto[0] != null &&
                            apunteBancario.RegistrosConcepto[0].Concepto.ToLower().StartsWith("core");
        }

        private static bool EsTransferenciaInternacional(ApunteBancarioDTO apunteBancario)
        {
            return apunteBancario.ConceptoComun == "04" &&
                            apunteBancario.ConceptoPropio == "002" &&
                            apunteBancario.RegistrosConcepto != null &&
                            apunteBancario.RegistrosConcepto.Any() &&
                            apunteBancario.RegistrosConcepto[2] != null &&
                            apunteBancario.RegistrosConcepto[2].Concepto.Trim().ToLower().StartsWith("invoice");
        }
    }
}
