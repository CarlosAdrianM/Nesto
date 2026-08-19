using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public class DatosMarkets
    {
        // NestoAPI#390: las cuentas de pago pasan de 555 (partidas pendientes de aplicación)
        // a 440 (deudores varios): son el derecho de cobro frente a Amazon Payments por
        // pedidos cobrados y aún no liquidados. Las CuentaContableComision (555) quedan en
        // extinción: ya no se contabiliza contra ellas (las comisiones van como pago a
        // cuenta al proveedor 999); se mantienen aquí solo para verlas drenar en el cuadre.
        public static List<Mercado> Mercados => [
                    new Mercado {
                        Id = "A1F83G8C2ARO7P",
                        NombreMarket = "Amazon.co.uk",
                        CuentaContablePago = "44000049",
                        CuentaContableComision = "55500066"
                    },
                    new Mercado {
                        Id = "A1PA6795UKMFR9",
                        NombreMarket = "Amazon.de",
                        CuentaContablePago = "44000046",
                        CuentaContableComision = "55500065"
                    },
                    new Mercado {
                        Id = "A1RKKUPIHCS9HS",
                        NombreMarket = "Amazon.es",
                        CuentaContablePago = "44000047",
                        CuentaContableComision = "55500062"
                    },
                    new Mercado {
                        Id = "A13V1IB3VIYZZH",
                        NombreMarket = "Amazon.fr",
                        CuentaContablePago = "44000045",
                        CuentaContableComision = "55500064"
                    },
                    new Mercado {
                        Id = "APJ6JRA9NG5V4",
                        NombreMarket = "Amazon.it",
                        CuentaContablePago = "44000048",
                        CuentaContableComision = "55500063"
                    },
                    new Mercado {
                        Id = "A1805IZSGTT6HS",
                        NombreMarket = "Amazon.nl",
                        CuentaContablePago = "44000050",
                        CuentaContableComision = "55500069"
                    },
                    new Mercado {
                        Id = "A2NODRKZP88ZB9",
                        NombreMarket = "Amazon.se",
                        CuentaContablePago = "44000072",
                        CuentaContableComision = "55500073"
                    },
                    new Mercado {
                        Id = "A33AVAJ2PDY3EV",
                        NombreMarket = "Amazon.tr",
                        CuentaContablePago = "44000080",
                        CuentaContableComision = "55500081"
                    },
                    new Mercado
                    {
                        Id = "AMEN7PMS3EDWL",
                        NombreMarket = "Amazon.com.be",
                        CuentaContablePago = "44000075",
                        CuentaContableComision = "55500076"
                    },
                    new Mercado
                    {
                        Id = "A1C3SOZRARQ6R3",
                        NombreMarket = "Amazon.pl",
                        CuentaContablePago = "44000039",
                        CuentaContableComision = "55500038"
                    },
                    new Mercado
                    {
                        Id = "A28R8C7NBKEWEA",
                        NombreMarket = "Amazon.ie",
                        CuentaContablePago = "44000082",
                        CuentaContableComision = "55500083"
                    },
                    new Mercado
                    {
                        Id = "A2VIGQ35RCS4UG",
                        NombreMarket = "Amazon.ae",
                        CuentaContablePago = "44000084",
                        CuentaContableComision = "55500085"
                    },
                    new Mercado
                    {
                        Id = "A17E79C6D8DWNP",
                        NombreMarket = "Amazon.sa",
                        CuentaContablePago = "44000087",
                        CuentaContableComision = "55500088"
                    },
                    new Mercado
                    {
                        Id = "miravia",
                        NombreMarket = "Miravia",
                        CuentaContablePago = "57200013",
                        CuentaContableComision = ""
                    }
                ];
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
