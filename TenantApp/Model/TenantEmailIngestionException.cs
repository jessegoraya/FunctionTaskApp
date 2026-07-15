namespace Taslow.Tenant.Model
{
    public class TenantEmailIngestionException : Exception
    {
        public TenantEmailIngestionException(string message, bool isTransient, Exception? innerException = null)
            : base(message, innerException)
        {
            IsTransient = isTransient;
        }

        public bool IsTransient { get; }
    }
}
