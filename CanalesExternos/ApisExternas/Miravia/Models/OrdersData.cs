using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class OrdersData
    {
        public int Count { get; set; }
        public List<Order> Orders { get; set; }
        public int CountTotal { get; set; }
    }
}
