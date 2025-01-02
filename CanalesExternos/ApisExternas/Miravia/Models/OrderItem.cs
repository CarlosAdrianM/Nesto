using Newtonsoft.Json;
using System;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class OrderItem
    {
        [JsonProperty("buyer_remark_text")]
        public string BuyerRemarkText { get; set; }

        [JsonProperty("pick_up_store_info")]
        public PickUpStoreInfo PickUpStoreInfo { get; set; }

        [JsonProperty("tax_amount")]
        public decimal TaxAmount { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("sla_time_stamp")]
        public DateTime? SlaTimeStamp { get; set; }

        [JsonProperty("voucher_seller")]
        public string VoucherSeller { get; set; }

        [JsonProperty("purchase_order_id")]
        public string PurchaseOrderId { get; set; }

        [JsonProperty("voucher_code_seller")]
        public string VoucherCodeSeller { get; set; }

        [JsonProperty("voucher_code")]
        public string VoucherCode { get; set; }

        [JsonProperty("package_id")]
        public string PackageId { get; set; }

        [JsonProperty("buyer_id")]
        public string BuyerId { get; set; }

        [JsonProperty("seller_remark_text")]
        public string SellerRemarkText { get; set; }

        [JsonProperty("variation")]
        public string Variation { get; set; }

        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        [JsonProperty("voucher_code_platform")]
        public string VoucherCodePlatform { get; set; }

        [JsonProperty("purchase_order_number")]
        public string PurchaseOrderNumber { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("order_type")]
        public string OrderType { get; set; }

        [JsonProperty("invoice_number")]
        public string InvoiceNumber { get; set; }

        [JsonProperty("cancel_return_initiator")]
        public string CancelReturnInitiator { get; set; }

        [JsonProperty("marketplace")]
        public string Marketplace { get; set; }

        [JsonProperty("shop_sku")]
        public string ShopSku { get; set; }

        [JsonProperty("is_reroute")]
        public string IsReroute { get; set; }

        [JsonProperty("stage_pay_status")]
        public string StagePayStatus { get; set; }

        [JsonProperty("sku_id")]
        public string SkuId { get; set; }

        [JsonProperty("tracking_code_pre")]
        public string TrackingCodePre { get; set; }

        [JsonProperty("order_item_id")]
        public string OrderItemId { get; set; }

        [JsonProperty("shop_id")]
        public string ShopId { get; set; }

        [JsonProperty("order_flag")]
        public string OrderFlag { get; set; }

        [JsonProperty("is_fbm")]
        public string IsFbm { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("marketplace_order_line_id")]
        public string MarketplaceOrderLineId { get; set; }

        [JsonProperty("delivery_option_sof")]
        public string DeliveryOptionSof { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("product_main_image")]
        public string ProductMainImage { get; set; }

        [JsonProperty("voucher_platform")]
        public string VoucherPlatform { get; set; }

        [JsonProperty("paid_price")]
        public decimal PaidPrice { get; set; }

        [JsonProperty("product_detail_url")]
        public string ProductDetailUrl { get; set; }

        [JsonProperty("warehouse_code")]
        public string WarehouseCode { get; set; }

        [JsonProperty("promised_shipping_time")]
        public string PromisedShippingTime { get; set; }

        [JsonProperty("shipping_type")]
        public string ShippingType { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("voucher_seller_lpi")]
        public string VoucherSellerLpi { get; set; }

        [JsonProperty("shipping_fee_discount_platform")]
        public string ShippingFeeDiscountPlatform { get; set; }

        [JsonProperty("wallet_credits")]
        public string WalletCredits { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("shipping_provider_type")]
        public string ShippingProviderType { get; set; }

        [JsonProperty("voucher_platform_lpi")]
        public string VoucherPlatformLpi { get; set; }

        [JsonProperty("shipping_fee_original")]
        public string ShippingFeeOriginal { get; set; }

        [JsonProperty("otoStoreInfo")]
        public OtoStoreInfo OtoStoreInfo { get; set; }

        [JsonProperty("item_price")]
        public decimal ItemPrice { get; set; }

        [JsonProperty("is_digital")]
        public bool IsDigital { get; set; }

        [JsonProperty("shipping_service_cost")]
        public string ShippingServiceCost { get; set; }

        [JsonProperty("return_able")]
        public string ReturnAble { get; set; }

        [JsonProperty("tracking_code")]
        public string TrackingCode { get; set; }

        [JsonProperty("shipping_fee_discount_seller")]
        public string ShippingFeeDiscountSeller { get; set; }

        [JsonProperty("shipping_amount")]
        public decimal ShippingAmount { get; set; }

        [JsonProperty("reason_detail")]
        public string ReasonDetail { get; set; }

        [JsonProperty("return_status")]
        public string ReturnStatus { get; set; }

        [JsonProperty("shipment_provider")]
        public string ShipmentProvider { get; set; }

        [JsonProperty("voucher_amount")]
        public string VoucherAmount { get; set; }

        [JsonProperty("digital_delivery_info")]
        public string DigitalDeliveryInfo { get; set; }

        [JsonProperty("extra_attributes")]
        public string ExtraAttributes { get; set; }
    }
}
