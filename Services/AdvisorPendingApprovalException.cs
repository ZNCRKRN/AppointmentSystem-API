namespace AppointmentSystem.API.Services
{
    // Signals that a self-registered advisor exists but cannot log in (or get an immediate
    // session right after registering) until an admin approves them.
    public class AdvisorPendingApprovalException : Exception
    {
        public AdvisorPendingApprovalException(string message) : base(message)
        {
        }
    }
}
