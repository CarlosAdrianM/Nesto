using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public class DatosMarkets
    {
        public static Dictionary<string, string> CuentaContablePago = new Dictionary<string, string>()
        {
            {"A1F83G8C2ARO7P", "55500049"}, // Amazon.co.uk
            {"A1PA6795UKMFR9", "55500046"}, // Amazon.de
            {"A1RKKUPIHCS9HS", "55500047"}, // Amazon.es
            {"A13V1IB3VIYZZH", "55500045"}, // Amazon.fr
            {"APJ6JRA9NG5V4", "55500048"},   // Amazon.it
            {"A1805IZSGTT6HS", "55500050"}   // Amazon.nl
        };
        
        public static Dictionary<string, string> CuentaContableComision = new Dictionary<string, string>()
        {
            {"A1F83G8C2ARO7P", "55500066"}, // Amazon.co.uk
            {"A1PA6795UKMFR9", "55500065"}, // Amazon.de
            {"A1RKKUPIHCS9HS", "55500062"}, // Amazon.es
            {"A13V1IB3VIYZZH", "55500064"}, // Amazon.fr
            {"APJ6JRA9NG5V4", "55500063"},   // Amazon.it
            {"A1805IZSGTT6HS", "55500069"}   // Amazon.nl
        };

        public static Dictionary<string, string> MarketCuentaPago = new Dictionary<string, string>()
        {
            {"55500049", "A1F83G8C2ARO7P"}, // Amazon.co.uk
            {"55500046", "A1PA6795UKMFR9"}, // Amazon.de
            {"55500047", "A1RKKUPIHCS9HS"}, // Amazon.es
            {"55500045", "A13V1IB3VIYZZH"}, // Amazon.fr
            {"55500048", "APJ6JRA9NG5V4"},   // Amazon.it
            {"55500050", "A1805IZSGTT6HS"}   // Amazon.nl
        };

        public static Dictionary<string, string> NombreMarket = new Dictionary<string, string>()
        {
            {"A1F83G8C2ARO7P", "Amazon.co.uk"},
            {"A1PA6795UKMFR9", "Amazon.de"},
            {"A1RKKUPIHCS9HS", "Amazon.es"},
            {"A13V1IB3VIYZZH", "Amazon.fr"},
            {"APJ6JRA9NG5V4", "Amazon.it" },
            {"A1805IZSGTT6HS", "Amazon.nl" }
        };

        public static Dictionary<string, string> CodigoMarket = new Dictionary<string, string>()
        {
            {"Amazon.co.uk", "A1F83G8C2ARO7P"},
            {"Amazon.de", "A1PA6795UKMFR9"},
            {"Amazon.es", "A1RKKUPIHCS9HS"},
            {"Amazon.fr", "A13V1IB3VIYZZH"},
            {"Amazon.it", "APJ6JRA9NG5V4"},
            {"Amazon.nl", "A1805IZSGTT6HS"}
        };
    }
}
