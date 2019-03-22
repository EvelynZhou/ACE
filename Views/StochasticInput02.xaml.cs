using RideComfortUC.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using RideComfortUC.Models;

namespace RideComfortUC.Views
{
    /// <summary>
    /// StochasticInput.xaml 的交互逻辑
    /// </summary>
    public partial class StochasticInput02 : UserControl
    {
        private double[][] _data;//输入数据
        StockInput02 _sto;
        TimeHisResult[] _result;
        public StochasticInput02()
        {
            InitializeComponent();
            Data.DataHelper data = new Data.DataHelper();
            _data = data.Data();
            _sto = new StockInput02(_data);
            _result = _sto.ListRes;
            GetBinding();
        }

        private void GetBinding()
        {
            TimeHisResult cushion = _result[0];
            textBlock01.SetBinding(TextBlock.TextProperty, new Binding("XRms") { Source = cushion });
            textBlock02.SetBinding(TextBlock.TextProperty, new Binding("YRms") { Source = cushion });
            textBlock03.SetBinding(TextBlock.TextProperty, new Binding("ZRms") { Source = cushion });
            textBlock11.SetBinding(TextBlock.TextProperty, new Binding("TotRms") { Source = cushion });
            TimeHisResult back = _result[1];
            textBlock04.SetBinding(TextBlock.TextProperty, new Binding("XRms") { Source =back });
            textBlock05.SetBinding(TextBlock.TextProperty, new Binding("YRms") { Source = back });
            textBlock06.SetBinding(TextBlock.TextProperty, new Binding("ZRms") { Source = back });
            textBlock12.SetBinding(TextBlock.TextProperty, new Binding("TotRms") { Source = back });
            TimeHisResult foot = _result[2];
            textBlock07.SetBinding(TextBlock.TextProperty, new Binding("XRms") { Source = foot });
            textBlock08.SetBinding(TextBlock.TextProperty, new Binding("YRms") { Source = foot });
            textBlock09.SetBinding(TextBlock.TextProperty, new Binding("ZRms") { Source = foot });
            textBlock13.SetBinding(TextBlock.TextProperty, new Binding("TotRms") { Source = foot });
        }
    }
}
