using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.Core;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public interface ISwaggerContextLogger
    {
        Task RequestStarting(Request request);
        Task RequestFinished(Request request, Response response);
    }
}
