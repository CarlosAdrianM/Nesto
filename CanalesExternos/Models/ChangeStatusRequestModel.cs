namespace CanalesExternos.Models
{
    /// <summary>
    /// Modelo para solicitar el cambio de estado de un poison pill
    /// </summary>
    public class ChangeStatusRequestModel
    {
        /// <summary>
        /// ID del mensaje cuyo estado se va a cambiar
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// Nuevo estado: "Reprocess", "Resolved", o "PermanentFailure"
        /// </summary>
        public string NewStatus { get; set; }
    }
}
