using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public class DatosMarkets
    {
        public static List<Mercado> Mercados { 
            get
            {
                return new List<Mercado>
                {
                    new Mercado {
                        Id = "A1F83G8C2ARO7P",
                        NombreMarket = "Amazon.co.uk",
                        CuentaContablePago = "55500049",
                        CuentaContableComision = "55500066"
                    },
                    new Mercado {
                        Id = "A1PA6795UKMFR9",
                        NombreMarket = "Amazon.de",
                        CuentaContablePago = "55500046",
                        CuentaContableComision = "55500065"
                    },
                    new Mercado {
                        Id = "A1RKKUPIHCS9HS",
                        NombreMarket = "Amazon.es",
                        CuentaContablePago = "55500047",
                        CuentaContableComision = "55500062"
                    },
                    new Mercado {
                        Id = "A13V1IB3VIYZZH",
                        NombreMarket = "Amazon.fr",
                        CuentaContablePago = "55500045",
                        CuentaContableComision = "55500064"
                    },
                    new Mercado {
                        Id = "APJ6JRA9NG5V4",
                        NombreMarket = "Amazon.it",
                        CuentaContablePago = "55500048",
                        CuentaContableComision = "55500063"
                    },
                    new Mercado {
                        Id = "A1805IZSGTT6HS",
                        NombreMarket = "Amazon.nl",
                        CuentaContablePago = "55500050",
                        CuentaContableComision = "55500069"
                    },
                    new Mercado {
                        Id = "A2NODRKZP88ZB9",
                        NombreMarket = "Amazon.se",
                        CuentaContablePago = "55500072",
                        CuentaContableComision = "55500073"
                    },
                    new Mercado {
                        Id = "A33AVAJ2PDY3EV",
                        NombreMarket = "Amazon.tr",
                        CuentaContablePago = "sin_crear",
                        CuentaContableComision = "sin_crear"
                    },
                    new Mercado
                    {
                        Id = "AMEN7PMS3EDWL",
                        NombreMarket = "Amazon.com.be",
                        CuentaContablePago = "55500075",
                        CuentaContableComision = "55500076"
                    },
                    new Mercado
                    {
                        Id = "A1C3SOZRARQ6R3",
                        NombreMarket = "Amazon.pl",
                        CuentaContablePago = "55500039",
                        CuentaContableComision = "55500038"
                    }
                };
            } 
        }
        public static Mercado Buscar(string Id)
        {
            return Mercados.Single(m => m.Id == Id);
        }
    }

    public class Mercado
    {
        public string Id { get; set; }
        public string CuentaContablePago { get; set; }
        public string CuentaContableComision { get; set; }
        public string NombreMarket { get; set; }
    }
}
