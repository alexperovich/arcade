using System.Drawing;
using System.Reflection.Emit;
using System.Windows;
using LINQPad.Extensibility.DataContext;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public partial class ConnectionDialog : Window
    {
        private readonly IConnectionInfo _cxInfo;

        public ConnectionDialog(IConnectionInfo cxInfo)
        {
            _cxInfo = cxInfo;
            DataContext = new SwaggerProperties(cxInfo);
            InitializeComponent();
        }

        void OKClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
