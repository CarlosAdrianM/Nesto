using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public enum EstadoFacturaCanalExterno
    {
        PendienteContabilizar,
        YaContabilizada,
        Hueco,
        Contabilizada,
        Error
    }

    public enum TipoFacturaCanalExterno
    {
        /// Facturas de comisiones de venta (Commission, DSF, Shipping, ShippingChargeback...)
        MerchantVAT,
        /// Facturas de tarifas FBA (FBAPerUnitFulfillmentFee)
        FBA,
        /// Abonos por ajustes/reembolsos (Refund:ItemFeeAdj)
        CreditNote,
        /// AGL Merchant Self Billing (envíos Amazon Global Logistics) - sin equivalente en FinancialEvents
        AGL,
        /// Merchant Buy Shipping Label (etiquetas de envío)
        BuyShippingLabel,
        /// Cualquier otro tipo no clasificado
        Otro
    }

    /// Representa una factura reconstruida a partir de los datos disponibles en la API.
    /// El usuario introduce el InvoiceId del PDF real y puede ajustar Base/CodigoIva si no cuadra.
    /// IVA y Total SIEMPRE se calculan a partir de Base × PorcentajeIva (coherencia contable).
    public class FacturaCanalExterno : IFiltrableItem, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _invoiceId;
        private DateTime _fechaFactura;
        private string _marketplaceId;
        private string _nombreMarket;
        private string _pais;
        private string _moneda;
        private string _concepto;
        private TipoFacturaCanalExterno _tipoFactura;
        private decimal _baseImponible;
        private string _codigoIva;
        private EstadoFacturaCanalExterno _estado = EstadoFacturaCanalExterno.PendienteContabilizar;
        private int? _numeroFacturaNesto;
        private int? _numeroPedidoNesto;
        private int? _asientoNesto;
        private int? _asientoPagoNesto;
        private string _mensajeError;
        private string _urlPdf;

        public string InvoiceId { get => _invoiceId; set => Set(ref _invoiceId, value); }
        public DateTime FechaFactura { get => _fechaFactura; set => Set(ref _fechaFactura, value); }
        public string MarketplaceId { get => _marketplaceId; set => Set(ref _marketplaceId, value); }
        public string NombreMarket { get => _nombreMarket; set => Set(ref _nombreMarket, value); }
        public string Pais { get => _pais; set => Set(ref _pais, value); }
        public string Moneda { get => _moneda; set => Set(ref _moneda, value); }
        public string Concepto { get => _concepto; set => Set(ref _concepto, value); }
        public TipoFacturaCanalExterno TipoFactura { get => _tipoFactura; set => Set(ref _tipoFactura, value); }

        public decimal BaseImponible
        {
            get => _baseImponible;
            set
            {
                if (Set(ref _baseImponible, value))
                {
                    OnPropertyChanged(nameof(ImporteIva));
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public string CodigoIva
        {
            get => _codigoIva;
            set
            {
                if (Set(ref _codigoIva, value))
                {
                    OnPropertyChanged(nameof(PorcentajeIva));
                    OnPropertyChanged(nameof(ImporteIva));
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public decimal PorcentajeIva
        {
            get
            {
                switch (CodigoIva)
                {
                    case "G21": return 21M;
                    case "G10": return 10M;
                    case "G04": return 4M;
                    default: return 0M;
                }
            }
        }

        public decimal ImporteIva => Math.Round(BaseImponible * PorcentajeIva / 100M, 2, MidpointRounding.AwayFromZero);

        public decimal Total => BaseImponible + ImporteIva;

        public EstadoFacturaCanalExterno Estado { get => _estado; set => Set(ref _estado, value); }
        public int? NumeroFacturaNesto { get => _numeroFacturaNesto; set => Set(ref _numeroFacturaNesto, value); }
        public int? NumeroPedidoNesto { get => _numeroPedidoNesto; set => Set(ref _numeroPedidoNesto, value); }
        public int? AsientoNesto { get => _asientoNesto; set => Set(ref _asientoNesto, value); }
        public int? AsientoPagoNesto { get => _asientoPagoNesto; set => Set(ref _asientoPagoNesto, value); }
        public string MensajeError { get => _mensajeError; set => Set(ref _mensajeError, value); }
        public string UrlPdf { get => _urlPdf; set => Set(ref _urlPdf, value); }

        /// Desglose opcional por tipo de fee (informativo, no se usa al contabilizar).
        public List<LineaFacturaCanalExterno> DetalleLineas { get; set; } = new List<LineaFacturaCanalExterno>();

        public bool Contains(string filtro)
        {
            if (string.IsNullOrWhiteSpace(filtro)) return true;
            filtro = filtro.ToLower();
            return (InvoiceId != null && InvoiceId.ToLower().Contains(filtro))
                || (NombreMarket != null && NombreMarket.ToLower().Contains(filtro))
                || (Pais != null && Pais.ToLower().Contains(filtro));
        }

        protected bool Set<T>(ref T campo, T valor, [CallerMemberName] string propiedad = null)
        {
            if (EqualityComparer<T>.Default.Equals(campo, valor)) return false;
            campo = valor;
            OnPropertyChanged(propiedad);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propiedad = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
    }

    public class LineaFacturaCanalExterno
    {
        public string Descripcion { get; set; }
        public decimal BaseImponible { get; set; }
        public decimal PorcentajeIva { get; set; }
        public decimal ImporteIva { get; set; }
        public decimal Total { get; set; }
        public string CodigoIva { get; set; }
    }
}
