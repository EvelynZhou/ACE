using System;
using System.Linq;
using MathNet.Numerics.IntegralTransforms;

namespace RideComfortUC.Models
{
    class TimeHisResult
    {
        #region 参数定义
        private double[][] _sample;
        private double[] _rms;
        private double _totrms;
        private int Nfft = 8; //Fourier长度
        private double _sampleFreq; //采样率
        private int _rank;
        private double _xRms;
        private double _yRms;
        private double _zRms;
        double[] freqlimit = new double[24] { 0.45, 0.57, 0.71, 0.9, 1.12, 1.4, 1.8, 2.24, 2.8, 3.55, 4.5, 5.6, 7.1, 9, 11.2, 14, 18, 22.4, 28, 35.5, 45, 56, 71, 90 };
        //0-22为下线，1-23为上限
        double[] wk = new double[23] { 418, 459, 477, 482, 484, 494, 531, 631, 804, 967, 1039, 1054, 1036, 988, 902, 768, 636, 513, 405, 314, 246, 186, 132 };
        double[] wd = new double[23] { 853, 944, 992, 1011, 1008, 968, 890, 776, 642, 512, 409, 323, 253, 212, 161, 125, 100, 80, 63.2, 49.4, 38.8, 29.5, 21.1 };
        double[] wc = new double[23] { 843, 929, 972, 991, 1000, 1007, 1012, 1017, 1022, 1024, 1013, 974, 891, 776, 647, 512, 409, 325, 256, 199, 156, 118, 84.4 };
        double[][] k = new double[][] { new double[] { 1, 1, 1 }, new double[] { 0.8, 0.5, 0.4 }, new double[] { 0.25, 0.25, 0.4 } };
        #endregion
        public double XRms
        {
            get { return _xRms; }
            set { _xRms = value; }
        }

        public double YRms
        {
            get { return _yRms; }
            set { _yRms = value; }
        }

        public double ZRms
        {
            get { return _zRms; }
            set { _zRms = value; }
        }

        private double[] _rmsN;

        public double[] RMS
        {
            get { return _rmsN; }
            set { _rmsN = value; }
        }

        private double _totRmsN;

        public double TotRms
        {
            get { return _totRmsN; }
            set { _totRmsN = value; }
        }

        /// <summary>
        /// 时间历程数据输入计算
        /// </summary>
        /// <param name="sample">采样数据</param>
        /// <param name="rank">1表示座椅坐垫，2表示座椅靠背，3表示脚部</param>
        public TimeHisResult(double[][] sample, int rank)
        {
            _sample = sample;
            _rank = rank;
            GetRMS();
            GetTolRMS();
        }

        private void GetRMS()
        {
            int len1 = _sample.Length;//获取数据个数，默认应为3个
            _rms = new double[len1];//每个位置的加权加速度均方根值
            //double _ratio = _sampleFreq / Nfft;//频域分辨率
            for (int i = 0; i < len1; i++)
            {
                _rms[i] = 0;
                double[] aj = new double[23];
                double[] aw = new double[23];
                double[] sampleCopy = _sample[i];
                int len = _sample[i].Length;
                double _ratio = (double)len / Nfft;

                if (len > 0)
                {
                    double[] data;
                    double[] imag;
                    data = _sample[i];
                    imag = new double[len];
                    Fourier.Forward(data, imag, FourierOptions.Default);//FFT结果也保存在data里
                    double[] wj = new double[23];

                    if (_rank == 1)
                    {
                        switch (i)
                        {
                            case 1:
                            case 2:
                                wj = wd;
                                break;
                            case 3:
                                wj = wk;
                                break;
                            default:
                                wj = wd;
                                break;
                        }
                    }
                    else if (_rank == 2)
                    {
                        switch (i)
                        {
                            case 4:
                                wj = wc;
                                break;
                            case 5:
                            case 6:
                                wj = wd;
                                break;
                            default:
                                wj = wd;
                                break;
                        }
                    }
                    else
                    {
                        wj = wk;
                    }

                    for (int j = 0; j < aj.Length; j++)
                    {
                        aj[j] = 0;
                        aw[j] = 0;
                        int _freqDown = (int)Math.Floor(freqlimit[j] / _ratio);
                        int _freqUp = (int)Math.Floor(freqlimit[j + 1] / _ratio);
                        if (_freqDown < data.Length && _freqDown < _freqUp)
                        {
                            if (_freqUp > data.Length)
                                _freqUp = data.Length;
                            for (int k = _freqDown; k < _freqUp; k++)
                            {
                                aj[j] += Math.Pow(data[k], 2);//周期法自功率谱计算包括在内
                            }
                            aj[j] = Math.Pow((aj[j] * _ratio), 0.5);//计算中心频率为fj的1/3倍频程加速度均方根值
                        }
                        aw[j] = Math.Pow(wj[j] * aj[j] / 1000, 2);
                    }
                    _rms[i] = Math.Pow(aw.Sum(), 0.5);//单轴向加权加速度均方根值
                }
            }
            _rmsN = _rms;
            _xRms = _rms[0];
            _yRms = _rms[1];
            _zRms = _rms[2];
        }

        private void GetTolRMS()
        {
            double[] kj = k[_rank];
            _totrms = 0;
            for (int l = 0; l < 3; l++)
            {
                _totrms += Math.Pow(kj[l] * _rms[l], 2);
            }
            _totrms = Math.Pow(_totrms, 0.5);
            _totRmsN = _totrms;
        }
    }
}
