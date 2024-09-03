using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AddPropertyToBinding.Windows
{
    /// <summary>
    /// Interaction logic for GetTypeWindow.xaml
    /// </summary>
    public partial class GetTypeWindow : Window, IDisposable
    {
        private Action<string> _SetPropertyType;

        public GetTypeWindow(Action<string> SetPropertyType)
        {
            InitializeComponent();
            _SetPropertyType = SetPropertyType;
            tbType.Focus();
        }

        public void Dispose()
        {
            _SetPropertyType.Invoke(tbType.Text.ToString());
        }

        private void tbType_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.Close();
            }
        }
    }
}
