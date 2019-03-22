using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
using System.Windows.Controls.DataVisualization.Charting;

namespace RideComfortUC.Views
{
    /// <summary>
    /// PulseInput.xaml 的交互逻辑
    /// </summary>
    public partial class PulseInput : UserControl
    {
        public PulseInput()
        {
            InitializeComponent();
            ((LineSeries)chart01.Series[0]).ItemsSource =
                new KeyValuePair<int, double>[]
                {
                    new KeyValuePair<int, double>(1,120),
                    new KeyValuePair<int, double>(2,130),
                   new KeyValuePair<int, double>(3,150),
                   new KeyValuePair<int, double>(4,100),
                   new KeyValuePair<int, double>(5,130),
                   new KeyValuePair<int, double>(6,180),
                };
        }
    }
}
