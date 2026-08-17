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
        // Nos los cobran a 13 céntimos + IVA
        private const decimal ComisionPorRecibo = .13M;
        private const decimal IvaComision = 1.21M;

        private readonly IBancosService _servicio;
        public ReglaComisionRemesaRecibos(IBancosService servicio)
        {
            _servicio = servicio;
        }

        public string Nombre => "Remesa recibos";

        // NestoAPI#384: con las remesas por vencimientos (NestoAPI#345) el banco carga las
        // comisiones en N apuntes (uno por abono/fecha de cargo), no en los 2 de siempre
        // (FRST+RCUR). Cada apunte es AUTOCONTENIDO: lleva su nº de factura del banco en
        // Concepto2 ("PR.FA189642364"), su tipo FR/RC en Referencia2 y su importe (nº de
        // recibos = importe / (0,13 × 1,21)). Se contabiliza 1:1 — una factura por apunte,
        // que es como puntea el banco — expandiendo la selección a los hermanos de la misma
        // remesa y día: basta seleccionar UN apunte cualquiera. El servidor es idempotente
        // (NestoAPI#384): si alguna factura ya estaba contabilizada no se duplica.
        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteSeleccionado = apuntesBancarios.First();
            var remesa = apunteSeleccionado.Referencia2.Substring(9, 5);

            List<ApunteBancarioDTO> apuntesComision = ExpandirAComisionesDeLaRemesa(apuntesBancarios, banco, apunteSeleccionado, remesa);

            var lineas = new List<PreContabilidadDTO>();
            int totalRecibosCalculados = 0;
            foreach (ApunteBancarioDTO apunte in apuntesComision)
            {
                int recibos = (int)Math.Round(-apunte.ImporteMovimiento / (ComisionPorRecibo * IvaComision), 0, MidpointRounding.AwayFromZero);
                totalRecibosCalculados += recibos;
                var tipoRecibosApunte = apunte.Referencia2.Substring(14, 2);
                string etiquetaTipo = tipoRecibosApunte == "FR" ? "FRST"
                    : tipoRecibosApunte == "RC" ? "RCUR"
                    : throw new Exception($"Tipo de recibo {tipoRecibosApunte} no contemplado en el proceso");
                string facturaApunte = FuncionesAuxiliaresReglas.UltimosDiezCaracteres(apunte.RegistrosConcepto[0].Concepto2.Substring(5).Trim());

                var linea = BancosViewModel.CrearPrecontabilidadDefecto();
                linea.Diario = "_ConcBanco";
                linea.TipoCuenta = Constantes.TiposCuenta.PROVEEDOR;
                linea.TipoApunte = Constantes.TiposApunte.FACTURA;
                linea.Cuenta = "433"; // Caixabank
                linea.Contacto = "0";
                linea.Concepto = $"Comisión {recibos} rbos. {etiquetaTipo} remesa {remesa} ({ComisionPorRecibo.ToString("c")}/rbo)";
                linea.Documento = facturaApunte;
                linea.Fecha = new DateOnly(apunte.FechaOperacion.Year, apunte.FechaOperacion.Month, apunte.FechaOperacion.Day);
                linea.Delegacion = "ALG";
                linea.Departamento = "ADM";
                linea.CentroCoste = "CA";
                linea.Debe = Math.Round(recibos * ComisionPorRecibo, 2, MidpointRounding.AwayFromZero);
                linea.Contrapartida = "62600002";
                lineas.Add(linea);
            }

            // Red de seguridad: los recibos deducidos de los apuntes deben cuadrar con los de
            // la remesa. Si no cuadran (comisiones cargadas en otra fecha, formato nuevo del
            // banco...), mejor parar con un mensaje claro que contabilizar mal.
            var numeroRecibosRemesa = Task.Run(async () => await _servicio.NumeroRecibosRemesa(remesa)).GetAwaiter().GetResult();
            if (totalRecibosCalculados != numeroRecibosRemesa)
            {
                throw new Exception($"Los apuntes de comisión encontrados suman {totalRecibosCalculados} recibos, " +
                    $"pero la remesa {remesa} tiene {numeroRecibosRemesa}. Revise si el banco ha cargado alguna " +
                    "comisión en otra fecha y seleccione también esos apuntes antes de contabilizar.");
            }

            return new ReglaContabilizacionResponse
            {
                Lineas = lineas,
                CrearFacturas = true,
                CrearPagosFacturas = true,
                Documento = remesa.ToString()
            };
        }

        // La selección del usuario (típicamente UN apunte) se expande a todos los apuntes de
        // comisión de la MISMA remesa del día — también los ya punteados/contabilizados: sus
        // facturas existen y el servidor idempotente las salta, y así el cuadre de recibos
        // siempre suma la remesa entera (un reintento parcial no se bloquea). Best-effort: si
        // la carga falla, se contabilizan los seleccionados y el cuadre avisa si falta alguno.
        private List<ApunteBancarioDTO> ExpandirAComisionesDeLaRemesa(IEnumerable<ApunteBancarioDTO> seleccionados,
            BancoDTO banco, ApunteBancarioDTO apunteSeleccionado, string remesa)
        {
            List<ApunteBancarioDTO> resultado = seleccionados.Where(a => EsComisionDeRemesa(a, remesa)).ToList();
            try
            {
                List<ApunteBancarioDTO> delDia = Task.Run(async () => await _servicio.LeerApuntesBanco(
                    banco.Empresa, banco.Codigo, apunteSeleccionado.FechaOperacion.Date, apunteSeleccionado.FechaOperacion.Date))
                    .GetAwaiter().GetResult();
                foreach (ApunteBancarioDTO apunte in delDia.Where(a => EsComisionDeRemesa(a, remesa)))
                {
                    if (!resultado.Any(r => r.Id == apunte.Id))
                    {
                        resultado.Add(apunte);
                    }
                }
            }
            catch
            {
                // Sin hermanos cargados se sigue con la selección; el cuadre de recibos avisa.
            }
            return resultado.OrderBy(a => a.Id).ToList();
        }

        private static bool EsComisionDeRemesa(ApunteBancarioDTO apunte, string remesa)
        {
            return apunte?.Referencia2 != null && apunte.Referencia2.Length >= 16
                && apunte.ConceptoComun == "17" && apunte.ConceptoPropio == "036"
                && apunte.RegistrosConcepto != null && apunte.RegistrosConcepto.Count > 1
                && apunte.RegistrosConcepto[1]?.Concepto2?.ToUpper().Trim() == "PREC.FATUR.DOMICIL"
                && apunte.Referencia2.Substring(9, 5) == remesa;
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
