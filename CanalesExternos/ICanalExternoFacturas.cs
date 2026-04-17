using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.Models.Cuadres;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos
{
    public interface ICanalExternoFacturas
    {
        string NombreCanal { get; }
        bool SoportaDescargaPdf { get; }
        bool SoportaCuadreLiquidacion { get; }

        /// Descarga las facturas del canal en el rango indicado.
        /// margenDiasAtras se usa para capturar facturas con fecha anterior al mes objetivo (huecos / tardías).
        Task<ObservableCollection<FacturaCanalExterno>> GetFacturasMesAsync(int año, int mes, int margenDiasAtras);

        /// Devuelve un diccionario InvoiceId → NºFactura Nesto, leído de NestoAPI
        /// (CabFacturaCmp por NºDocumentoProv) en el rango indicado.
        Task<IReadOnlyDictionary<string, int>> GetFacturasYaContabilizadasAsync(DateTime desde, DateTime hasta);

        /// Contabiliza una factura en NestoAPI (pedido + albarán + factura). Rellena NumeroFacturaNesto o MensajeError.
        Task<FacturaCanalExterno> ContabilizarFacturaAsync(FacturaCanalExterno factura);

        /// Descarga el PDF de la factura. Devuelve null si el canal no lo soporta.
        Task<byte[]> DescargarPdfAsync(FacturaCanalExterno factura);

        /// Cuadra las facturas del mes contra las liquidaciones del canal. Null si no aplica.
        Task<CuadreLiquidacionCanalExterno> CuadrarConLiquidacionAsync(int año, int mes);

        /// Issue #349 Fase 1: cuadre de facturas Nesto ↔ Amazon por presencia de InvoiceId.
        /// Empareja las facturas que Amazon reporta (LeerFacturasAsync) con las que tenemos
        /// contabilizadas en Nesto bajo proveedor 999 (FacturasContabilizadasProveedor).
        /// Los importes quedan sin comparar: solo detecta facturas presentes en un lado y no en el otro.
        Task<ResultadoCuadre<string>> CuadrarFacturasAsync(int año, int mes);

        /// Parsea un listado copiado de la web del canal (TSV) y asigna el InvoiceId a cada factura
        /// reconstruida que empareje por (marketplace, tipo). Para documentos sin equivalente en la API
        /// (AGL, Buy Shipping Label, o duplicados) añade facturas sintéticas con Base=0 para que el
        /// usuario las complete desde el PDF.
        /// Devuelve (emparejadas, sintéticasAñadidas, noEmparejadas).
        (int Emparejadas, int Sinteticas, int NoEmparejadas) EmparejarListado(
            IList<FacturaCanalExterno> facturas, string listadoPegado);

        /// Recomprueba qué facturas (con InvoiceId ya asignado) están en NestoAPI y las marca como
        /// YaContabilizada. Útil tras EmparejarListado, porque MarcarEstados se ejecutó cuando aún
        /// no había InvoiceId. Devuelve cuántas se marcaron.
        Task<int> RefrescarEstadosContabilizadasAsync(
            IList<FacturaCanalExterno> facturas, int año, int mes, int margenDiasAtras);
    }
}
