using RideComfortUC.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RideComfortUC.ViewModels
{
    class StochasticInputHandle : INotifyPropertyChanged
    {
        private double[][] _data;

        private ObservableCollection<TimeHisResult> _rmsResult;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<TimeHisResult> RmsResult
        {
            get { return _rmsResult; }
            set
            {
                _rmsResult = value;
                RaisePropertyChanged("RmsResult");
            }
        }

        /// <summary>
        /// 构造器 
        /// </summary>
        /// <param name="data">输入的各个位置的数据</param>
        public StochasticInputHandle(double[][] data)
        {
            _data = data;
            CalResult();
        }
        public StochasticInputHandle()
        {

        }

        private void CalResult()
        {
            for (int i = 0; i < (_data.Length % 3); i++)
            {
                double[][] _sigData = new double[3][];
                for (int j = 0; j < 3; j++)
                {
                    _sigData[j] = _data[i * 3 + j];
                }
                TimeHisResult sto = new TimeHisResult(_sigData, i);
                RmsResult.Add(sto);
            }
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
