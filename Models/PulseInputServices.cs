using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideComfortUC.Models
{
    class PulseInputServices : IImpulseInputHandle
    {
        private double[][] _sample;

        public double[][] Sample
        {
            get { return _sample; }
            set { _sample = value; }
        }

        private double _maxz;
        private double[] _peakFactor;
        private double[] _VDV;

        public double GetMaxZ()
        {
            _maxz = 0.0;
            int len1 = _sample.GetLength(1);
            for (int i = 0; i < len1; i++)
            {
                _maxz += _sample[i].Max();
            }
            _maxz = _maxz / len1;
            return _maxz;
        }

        public double GetPeakFactor()
        {
            throw new NotImplementedException();
        }

        public double GetVDV()
        {
            throw new NotImplementedException();
        }
    }
}
