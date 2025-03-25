using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaComisionRemesaRecibos : IReglaContabilizacion
    {
        private readonly IBancosService _servicio;
        public ReglaComisionRemesaRecibos(IBancosService servicio)
        {
            _servicio = servicio;
        }

        public string Nombre => "Remesa recibos";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.First();
            var apunteContabilidad = apuntesContabilidad.First();
            var importeDescuadre = apuntesBancarios.Sum(b => b.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            // Nos los cobran a 13 céntimos + IVA           
            var comisionPorRecibo = .13M;
            var ivaComision = 1.21M;
            var remesa = apunteBancario.Referencia2.Substring(9, 5);
            var tipoRecibosApunte = apunteBancario.Referencia2.Substring(14, 2);
            var numeroRecibosRemesa = Task.Run(async () => await _servicio.NumeroRecibosRemesa(remesa)).GetAwaiter().GetResult();
            int recibosApunteActual = (int)Math.Round(-apunteBancario.ImporteMovimiento / (comisionPorRecibo * ivaComision), 0, MidpointRounding.AwayFromZero);
            int recibosFRST;
            int recibosRCUR;
            int primeraFactura;
            string facturaApunte = FuncionesAuxiliaresReglas.UltimosDiezCaracteres(apunteBancario.RegistrosConcepto[0].Concepto2.Substring(5).Trim());
            int.TryParse(facturaApunte, out primeraFactura);
            if (tipoRecibosApunte == "FR")
            {
                recibosFRST = recibosApunteActual;
                recibosRCUR = numeroRecibosRemesa - recibosFRST;
            } 
            else if (tipoRecibosApunte == "RC")
            {
                recibosRCUR = recibosApunteActual;
                recibosFRST = numeroRecibosRemesa - recibosRCUR;
                primeraFactura--;
            }
            else
            {
                throw new Exception($"Tipo de recibo {tipoRecibosApunte} no contemplado en el proceso");
            }
                        
            var lineas = new List<PreContabilidadDTO>();

            if (recibosFRST != 0)
            {
                var lineaFR = BancosViewModel.CrearPrecontabilidadDefecto();
                lineaFR.Diario = "_ConcBanco";
                lineaFR.TipoCuenta = Constantes.TiposCuenta.PROVEEDOR;
                lineaFR.TipoApunte = Constantes.TiposApunte.FACTURA;
                lineaFR.Cuenta = "433"; // Caixabank
                lineaFR.Contacto = "0";
                lineaFR.Concepto = $"Comisión {recibosFRST} rbos. FRST remesa {remesa} ({comisionPorRecibo.ToString("c")}/rbo)";
                lineaFR.Documento = primeraFactura.ToString();
                lineaFR.Fecha = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
                lineaFR.Delegacion = "ALG";
                lineaFR.Departamento = "ADM";
                lineaFR.CentroCoste = "CA";
                lineaFR.Debe = Math.Round(recibosFRST * comisionPorRecibo, 2, MidpointRounding.AwayFromZero);
                lineaFR.Contrapartida = "62600002";
                lineas.Add(lineaFR);
            }

            if (recibosRCUR != 0)
            {
                var lineaRC = BancosViewModel.CrearPrecontabilidadDefecto();
                lineaRC.Diario = "_ConcBanco";
                lineaRC.TipoCuenta = Constantes.TiposCuenta.PROVEEDOR;
                lineaRC.TipoApunte = Constantes.TiposApunte.FACTURA;
                lineaRC.Cuenta = "433"; // Caixabank
                lineaRC.Contacto = "0";
                lineaRC.Concepto = $"Comisión {recibosRCUR} rbos. RCUR remesa {remesa} ({comisionPorRecibo.ToString("c")}/rbo)";
                lineaRC.Documento = (++primeraFactura).ToString();
                lineaRC.Fecha = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
                lineaRC.Delegacion = "ALG";
                lineaRC.Departamento = "ADM";
                lineaRC.CentroCoste = "CA";
                lineaRC.Debe = Math.Round(recibosRCUR * comisionPorRecibo, 2, MidpointRounding.AwayFromZero);
                lineaRC.Contrapartida = "62600002";
                lineas.Add(lineaRC);
            }
            
                        

            ReglaContabilizacionResponse response = new ReglaContabilizacionResponse
            {
                Lineas = lineas,
                CrearFacturas = true,
                CrearPagosFacturas = true,
                Documento = remesa.ToString()
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

            if (apunteBancario.ConceptoComun == "17" &&
                apunteBancario.ConceptoPropio == "036" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                apunteBancario.RegistrosConcepto[1]?.Concepto2.ToUpper().Trim() == "PREC.FATUR.DOMICIL")
            {
                return true;
            }

            return false;
        }
    }
}
