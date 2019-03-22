using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideComfortUC.Data
{
    class DataHelper
    {
        double[][] _data = new double[9][];
        double[] _ndata = new double[] { 100, 200, 300, 400, 500, 600, 700, 800, 900 };
        double[] _nndata = new double[] { 0, 1, 2, 300, 4, 500, 600, 700, 8, 900 };
        public double[][] Data()
        {
            _data[0] = _ndata;
            _data[1] = _nndata;
            for (int i = 2; i < 9; i++)
            {
                _data[i] = _ndata;
            }
            return _data;
        }
    }
}
