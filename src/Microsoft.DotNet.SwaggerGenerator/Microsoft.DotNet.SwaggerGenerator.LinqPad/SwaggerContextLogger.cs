using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.Core;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public class SwaggerContextLogger : ISwaggerContextLogger
    {
        private readonly TextWriter _output;

        public SwaggerContextLogger(TextWriter output)
        {
            _output = output;
        }

        private async Task Display(IEnumerable<HttpHeader> headers)
        {
            foreach (var header in headers)
            {
                var name = header.Name;
                var value = header.Value;
                if (name == "Authorization")
                {
                    await _output.WriteLineAsync($"{name}: *********");
                    continue;
                }
                await _output.WriteLineAsync($"{name}: {value}");
            }

            return;
        }

        public async Task RequestStarting(Request request)
        {

            if (_output != null)
            {
                await _output.WriteLineAsync($"{request.Method} {request.Uri}");
                await Display(request.Headers);
                await _output.WriteLineAsync();
            }
        }

        public async Task RequestFinished(Request request, Response response)
        {
            if (_output != null)
            {
                await _output.WriteLineAsync($"{(int)response.Status} {response.ReasonPhrase}");
                await Display(response.Headers);
                await _output.WriteLineAsync();
            }
        }
    }
}
