using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Statistics;

namespace RideComfortUC.Func
{
    class CalRandInputFunc
    {
        #region
        //参数定义
        float[] _xval;
        float[] _yval;
        int _sampleRate;
        string _weightType;
        int _sampleLength;
        double[] _aw;

        double _RMS;
        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="xval">实部</param>
        /// <param name="yval">虚部</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="weighType">轴加权类型</param>
        public CalRandInputFunc(float[] xval,float[] yval,int sampleRate,string weighType)
        {
            _xval = xval;
            _yval = yval;
            _sampleRate = sampleRate;
            _weightType = weighType;
            _sampleLength = xval.Length;
        }

        private void CalRMS()
        {
            Complex32[] at = new Complex32[_sampleLength];  //原始信号
            float[] freq = new float[_sampleLength];
            for (int i = 0; i < _sampleLength; i++)
            {
                at[i] = new Complex32(_xval[i], _yval[i]);
                freq[i] = i * _sampleRate / _sampleLength;
            }
            Fourier.Forward(at);    //FFT之后的结果保留在at序列中
            for (int i = _sampleLength / 2 + 1; i < _sampleLength; i++) //去掉大于fs/2的成分，之后幅值再乘以2/N
            {
                at[i] = 0;
            }
            Complex32[] weight = new Complex32[_sampleLength];
            Weight weigh = new Weight(_weightType, freq, out weight);
            Complex32[] aw = new Complex32[_sampleLength];  //aw为加权后的信号
            for (int i = 0; i < _sampleLength; i++)
            {
                aw[i] = 2 * at[i] * weight[i] / _sampleLength;    //恢复幅值，幅值乘以2/N
            }
            Fourier.Inverse(aw);
            double[] awAbs = new double[_sampleLength];
            for (int i = 0; i < _sampleLength; i++)
            {
                awAbs[i] = Complex32.Abs(aw[i]);
            }
            _aw = awAbs;
            _RMS = _aw.RootMeanSquare();
        }
    }
}
