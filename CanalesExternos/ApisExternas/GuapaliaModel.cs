using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    public class GuapaliaOrder
    {
        public int orderId { get; set; }
        public DateTime date { get; set; }
        public string customerFirstName { get; set; }
        public string customerLastName { get; set; }
        public string customerAddress { get; set; }
        public string customerPc { get; set; }
        public string customerCity { get; set; }
        public string customerState { get; set; }
        public string customerStateCode { get; set; }
        public string customerCountry { get; set; }
        public string customerPhone { get; set; }
        public decimal total { get; set; }
        public string shipping { get; set; }
        public string comment { get; set; }
        public List<GuapaliaOrderItem> items { get; set; }
    }

    public class GuapaliaOrderItem
    {
        public int quantity { get; set; }
        public int itemId { get; set; }
        public string description { get; set; }
        public string barcode { get; set; }
        public decimal unitPrice { get; set; }
        public decimal total { get; set; }
    }
    
}
