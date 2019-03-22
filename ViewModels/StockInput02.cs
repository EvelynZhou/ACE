using RideComfortUC.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RideComfortUC.ViewModels
{
    class StockInput02
    {
        private double[][] _data;
        TimeHisResult[] listRes = new TimeHisResult[3];

        public TimeHisResult[] ListRes
        {
            get
            {
                return listRes;
            }

            set
            {
                listRes = value;
            }
        }

        /// <summary>
        /// 构造器 
        /// </summary>
        /// <param name="data">输入的各个位置的数据</param>
        public StockInput02(double[][] data)
        {
            _data = data;
            CalResult();
        }
        public StockInput02()
        {

        }

        private void CalResult()
        {
            for (int i = 0; i < (_data.Length / 3); i++)
            {
                double[][] _sigData = new double[3][];
                for (int j = 0; j < 3; j++)
                {
                    _sigData[j] = _data[i * 3 + j];
                }
                TimeHisResult sto = new TimeHisResult(_sigData, i);
                listRes[i] = sto;
            }
        }
    }
}
