namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class MiraviaConnection
    {
        public MiraviaConnection(MiraviaCredential credential)
        {
            Credential = credential;
        }

        public MiraviaCredential Credential { get; }
    }
}
