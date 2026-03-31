using System.Net;

namespace Taslow.Tenant.Model
{
    public class TenantApiException : Exception
    {
        public TenantApiException(HttpStatusCode statusCode, string code, string message, IEnumerable<string>? details = null)
            : base(message)
        {
            StatusCode = statusCode;
            Code = code;
            Details = details?.ToList() ?? new List<string>();
        }

        public HttpStatusCode StatusCode { get; }
        public string Code { get; }
        public List<string> Details { get; }
    }
}
