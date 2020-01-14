using System.Xml.Linq;
using LINQPad.Extensibility.DataContext;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public class SwaggerProperties
    {
        private IConnectionInfo _cxInfo;
        private XElement DriverData => _cxInfo.DriverData;

        public SwaggerProperties(IConnectionInfo info)
        {
            _cxInfo = info;
        }

        public string Uri
        {
            get => (string) DriverData.Element("Uri") ?? "";
            set => DriverData.SetElementValue("Uri", value);
        }
    }
}
