using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class Order
    {
        [JsonProperty("buyer_remark_text")]
        public string BuyerRemarkText { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("voucher")]
        public decimal Voucher { get; set; }

        [JsonProperty("warehouse_code")]
        public string WarehouseCode { get; set; }

        [JsonProperty("order_number")]
        public long OrderNumber { get; set; }

        [JsonProperty("voucher_seller")]
        public decimal VoucherSeller { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("voucher_code")]
        public string VoucherCode { get; set; }

        [JsonProperty("seller_remark_text")]
        public string SellerRemarkText { get; set; }

        [JsonProperty("gift_option")]
        public bool GiftOption { get; set; }

        [JsonProperty("customer_last_name")]
        public string CustomerLastName { get; set; }

        [JsonProperty("promised_shipping_times")]
        public string PromisedShippingTimes { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("national_registration_number")]
        public string NationalRegistrationNumber { get; set; }

        [JsonProperty("shipping_fee_original")]
        public decimal ShippingFeeOriginal { get; set; }

        [JsonProperty("payment_method")]
        public string PaymentMethod { get; set; }

        [JsonProperty("marketplace")]
        public string Marketplace { get; set; }

        [JsonProperty("customer_first_name")]
        public string CustomerFirstName { get; set; }

        [JsonProperty("shipping_fee_discount_seller")]
        public decimal ShippingFeeDiscountSeller { get; set; }

        [JsonProperty("shipping_fee")]
        public decimal ShippingFee { get; set; }

        [JsonProperty("items_count")]
        public int ItemsCount { get; set; }

        [JsonProperty("delivery_info")]
        public string DeliveryInfo { get; set; }

        [JsonProperty("statuses")]
        public List<string> Statuses { get; set; }

        [JsonProperty("address_billing")]
        public Address AddressBilling { get; set; }

        [JsonProperty("extra_attributes")]
        public string ExtraAttributes { get; set; }

        [JsonProperty("order_id")]
        public long OrderId { get; set; }

        [JsonProperty("remarks")]
        public string Remarks { get; set; }

        [JsonProperty("gift_message")]
        public string GiftMessage { get; set; }

        [JsonProperty("address_shipping")]
        public Address AddressShipping { get; set; }
    }
}
