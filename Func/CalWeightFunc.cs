using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Statistics;

namespace RideComfortUC.Func
{
    class CalWeightFunc
    {
        #region 
        //参数定义
        double _L;   //轴距
        double _velocity;   //车速

        float[] _xval;  //实部
        float[] _yval;  //虚部
        double[] _y;    //幅值
        int _sampleRate;  //采样率
        string _weightType; //轴向加权系数类型
        int _sampleLength;  //输入序列的长度

        double[] _aw;   //加权加速度时间历程
        double _rms;  //加权加速度均方根值

        double _Peak;    //加速度响应的峰值
        double _CF;  //峰值系数
        double _Ap;  //峰-峰值，最大值与最小值的差

        double _localRMS;   //两轮冲击振动有效值
        double _globalRMS;  //四轮冲击振动有效值
        double _DT; //衰减时间

        double _VDV;    //四次方振动剂量值
        double _MTVV;   //移动均方根值最大值
        double _R1; //MTVV评价系数，>1.5则辅助评价方法比较有效
        double _R2; //VDV评价系数，>1.75则辅助评价方法比较有效

        int _leftInd;
        int _rightInd;
        int _YmaxInd;
        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="xval">实部</param>
        /// <param name="yval">虚部</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="weighType">加权类型</param>
        /// <param name="L">轴距</param>
        /// <param name="velocity">车速</param>
        /// <param name="RMS">加权加速度均方根值</param>
        /// <param name="Peak">加权加速度峰值</param>
        /// <param name="CF">峰值系数</param>
        /// <param name="Ap">峰-峰值</param>
        /// <param name="localRMS">两轮冲击振动有效值</param>
        /// <param name="globalRMS">四轮冲击振动有效值</param>
        /// <param name="VDV">四次方振动剂量值</param>
        /// <param name="MTVV">移动均方根值最大值</param>
        /// <param name="R1">MTVV评价系数</param>
        /// <param name="R2">VDV评价系数</param>
        public CalWeightFunc(float[] xval, float[] yval, int sampleRate, string weighType, double L, double velocity, out double RMS, out double Peak, out double CF, out double Ap, out double localRMS, out double globalRMS, out double VDV, out double MTVV, out double R1, out double R2)
        {
            _xval = xval;
            _yval = yval;
            _sampleRate = sampleRate;
            _sampleLength = xval.Length;
            _weightType = weighType;
            _L = L;
            _velocity = velocity;

            CalCF();
            RMS = _rms;
            Peak = _Peak;
            CF = _CF;
            Ap = _Ap;

            CalPulseValue();
            localRMS = _localRMS;
            globalRMS = _globalRMS;

            AdditionalMethod();
            VDV = _VDV;
            MTVV = _MTVV;
            R1 = _R1;
            R2 = _R2;
        }

        #region 
        /// <summary>
        /// 计算峰值系数和加权加速度时间历程
        /// </summary>
        private void CalCF()
        {
            Complex32[] at = new Complex32[_sampleLength];  //原始信号
            float[] freq = new float[_sampleLength];
            for (int i = 0; i < _sampleLength; i++)
            {
                at[i] = new Complex32(_xval[i], _yval[i]);
                freq[i] = i * _sampleRate / _sampleLength;
            }
            Fourier.Forward(at);    //FFT之后的结果保留在at序列中

            Complex32[] weight = new Complex32[_sampleLength];
            Weight weigh = new Weight(_weightType, freq, out weight);
            Complex32[] aw = new Complex32[_sampleLength];  //aw为加权后的信号

            int halfNfft;
            if (_sampleLength % 2 == 1)
            {
                halfNfft = (int)Math.Floor((double)(_sampleLength / 2)) + 1;
            }
            else
            {
                halfNfft = (int)Math.Floor((double)(_sampleLength / 2));
            }
            for (int i = 0; i < halfNfft; i++)
            {
                aw[i] = at[i] * weight[i];
                aw[_sampleLength - i - 1] = aw[i];
            }

            Fourier.Inverse(aw);
            double[] awAbs = new double[_sampleLength];
            for (int i = 0; i < _sampleLength; i++)
            {
                awAbs[i] = Complex32.Abs(aw[i]);
            }
            _aw = awAbs;
            _rms = _aw.RootMeanSquare();
            _Peak = awAbs.Max();
            double tempAp = awAbs.Max() - awAbs.Min();
            _Ap = Math.Abs(tempAp);
            _CF = _Peak / _rms;
        }

        /// <summary>
        /// 计算两轮冲击振动有效值和四轮冲击振动有效值
        /// </summary>
        private void CalPulseValue()
        {
            int Mlocal = (int)Math.Round((_sampleRate * _L / 3.6 / _velocity) * 0.52) + 1;  //汽车通过凸块的采样点数
            int Mglobal = (int)Math.Round((_sampleRate * _L / 3.6 / _velocity) * 1.05) + 1; //汽车通过凸块的采样点数
            double rmsSmooth = 0;

            // 这里需要确定光滑路面的rms值

            int Nlocal = _sampleLength - Mlocal + 1;
            int Nglobal = _sampleLength - Mglobal + 1;

            double[] deltaRMSLocal = new double[Nlocal];
            for (int i = 0; i < Nlocal; i++)
            {
                double[] awTemp = new double[Mlocal];
                for (int j = 0; j < Mlocal; j++)
                {
                    awTemp[j] = _aw[i + j];
                }
                deltaRMSLocal[i] = awTemp.RootMeanSquare();
            }
            _localRMS = deltaRMSLocal.Max() - rmsSmooth;

            double[] deltaRMSGlobal = new double[Nglobal];
            for (int m = 0; m < Nglobal; m++)
            {
                double[] awTemp2 = new double[Mglobal];
                for (int n = 0; n < Mglobal; n++)
                {
                    awTemp2[m] = _aw[m + n];
                }
                deltaRMSGlobal[m] = awTemp2.RootMeanSquare();
            }
            _globalRMS = deltaRMSGlobal.Max() - rmsSmooth;
        }

        /// <summary>
        /// 辅助评价方法，计算四次方振动剂量值VDV、移动均方根值的最大值MTVV
        /// </summary>
        private void AdditionalMethod()
        {
            double temp3 = 0;
            for (int i = 0; i < _sampleLength; i++)
            {
                temp3 = temp3 + Math.Pow(_aw[i], 4) * (_sampleLength / _sampleRate);
            }
            _VDV = Math.Pow(temp3 / _sampleLength, 0.25);

            if (_sampleLength < _sampleRate)
            {
                MessageBox.Show("Sample length is shorter than sampleRate.");
            }
            double[] at0 = new double[_sampleLength - _sampleRate + 1];
            for (int j = 0; j < _sampleLength - _sampleRate + 1; j++)
            {
                double[] temp4 = new double[_sampleRate];
                for (int k = 0; k < _sampleRate; k++)
                {
                    temp4[j] = _aw[j + k];
                }
                at0[j] = temp4.RootMeanSquare();
            }
            _MTVV = at0.Max();

            _R1 = _MTVV / _rms;
            double T = _sampleLength / _sampleRate; //信号作用时间
            _R2 = _VDV / _rms / Math.Pow(T, 0.25);
        }


        private void CalDT()
        {
            //计算rmsDelta曲线
            int M = (int)Math.Round(_sampleRate * 4 / 3.6 / 10 * 1.05) + 1;
            int Len = _yval.Length - M + 1;
            double[] rmsDelta = new double[Len];
            for (int k = 0; k < Len; k++)
            {
                double[] temp = new double[M];
                for (int m = k; m < k + M; m++)
                {
                    temp[m - k] = _y[m];
                }
                rmsDelta[k] = temp.RootMeanSquare();
            }

            //计算能量分布曲线
            double[] power = new double[Len];
            double[] _y2 = new double[_sampleLength]; //y^2的大小，可视为能量
            for (int i = 0; i < _sampleLength; i++)
            {
                _y2[i] = Math.Pow(Math.Pow(_xval[i], 2) + Math.Pow(_yval[i], 2), 0.5);
            }
            power[0] = _y2[0];
            for (int i = 1; i < Len; i++)
            {
                power[i] = power[i - 1] + _y2[i];
                //能量分布曲线，最后一点为最大的能量值
            }

            //找到rmsGlobal最大值的点的位置以及值
            double ymax;
            int indYmax;
            ymax = rmsDelta[0];
            indYmax = 0;
            for (int i = 1; i < Len; i++)
            {
                if (ymax < rmsDelta[i])
                {
                    ymax = rmsDelta[i];
                    indYmax = i;
                }
            }
            _YmaxInd = indYmax;

            double w = 0.05;
            int lowIND = CalId(w, rmsDelta, power, _y);
            while (lowIND >= indYmax)
            {
                w = w / 2;
                lowIND = CalId(w, rmsDelta, power, _y);
            }

            _leftInd = lowIND;
        }

        private int CalId(double factor, double[] rmsGlobal, double[] powerAll, double[] yval)
        {

            int Len = rmsGlobal.Length;


            //找到能量值为w的点的位置
            int indw = 0;   //能量取值范围的位置
            for (int i = 0; i < Len; i++)
            {
                if (powerAll[i] >= factor * powerAll[Len - 1])
                {
                    indw = i;
                    break;
                }
            }

            //将前5%能量处作为平滑路面
            double[] temp = new double[indw];
            for (int i = 0; i < indw; i++)
            {
                temp[i] = yval[i];
            }
            double rmsSmooth = temp.RootMeanSquare();

            //找到所有rmsDelta==rmsSmooth的点或者附近
            List<int> indList = new List<int>();
            indList.Add(0);
            List<double> powerList = new List<double>();
            powerList.Add(0);
            for (int i = indw; i < Len - 1; i++)
            {
                if ((rmsGlobal[i + 1] >= rmsSmooth && rmsGlobal[i] <= rmsSmooth) || (rmsGlobal[i + 1] <= rmsSmooth && rmsGlobal[i] >= rmsSmooth))
                {
                    indList.Add(i);
                    powerList.Add(powerAll[i]);
                }
            }
            if (indList.Last() != Len - 1)
            {
                indList.Add(Len - 1);
                powerList.Add(powerAll[Len - 1]);
            }

            int[] indArray = indList.ToArray();
            //定义区间因子

            double w1 = 0.4; //IND所在点的能量不能超过w*PowerMax
            int ind01 = CalFacM(powerAll, rmsGlobal, indArray, w1);
            while (powerAll[ind01] > 0.4 * powerAll[Len - 1])
            {
                w1 = w1 / 2;
                ind01 = CalFacM(powerAll, rmsGlobal, indArray, w1);
            }

            int n = 20; //区间划分次数
            int IND = OptRegion(n, rmsGlobal, powerAll, ind01, _YmaxInd);

            List<int> upList = new List<int>();
            for (int i = 0; i < indArray.Length; i++)
            {
                if (indArray[i]>_YmaxInd)
                {
                    upList.Add(indArray[i]);
                }
            }

            int[] upArray = upList.ToArray();
            int kt = 0;
            int uplen = upArray.Length;
            int upInd01 = upArray[kt];
            while (powerAll[upInd01]<=0.7*powerAll[Len-1])
            {
                if (kt>=uplen-1)
                {
                    break;
                }
                kt += 1;
                upInd01 = upArray[kt];
            }
            _rightInd = upInd01;

            return IND;
        }

        private int OptRegion(int n, double[] rmsGlobal, double[] powerAll, int indLow, int indYmax)
        {
            int inter = (int)Math.Round((double)(indYmax - indLow) / n) - 1;
            int IndSection;
            if (inter > 5)
            {
                if (indLow <= inter + 2)
                {
                    int[] list = new int[n + 2];
                    list[0] = indLow;
                    list[n] = indYmax;
                    list[n + 1] = indYmax + inter;
                    for (int i = 1; i < n; i++)
                    {
                        list[i] = list[i - 1] + inter;
                    }
                    double w = 0.4;
                    IndSection = CalFacM(powerAll, rmsGlobal, list, w);
                }
                else
                {
                    int[] list = new int[n + 3];
                    list[0] = indLow - inter;
                    list[1] = indLow;
                    list[n + 1] = indYmax;
                    list[n + 2] = indYmax + inter;
                    for (int i = 2; i < n + 1; i++)
                    {
                        list[i] = list[i - 1] + inter;
                    }
                    double w = 0.4;
                    IndSection = CalFacM(powerAll, rmsGlobal, list, w);
                }
                return IndSection;
            }
            else
            {
                return indLow;
            }
        }

        //区间最大值比值系数，并返回所给indListArrayy点中第一个能量小于w*最大能量的点的位置
        private int CalFacM(double[] powerSum, double[] rmsDel, int[] indListArray, double w)
        {
            //定义区间因子
            double[] rmsGlobal = rmsDel;
            double[] powerAll = powerSum;
            int[] indArray = indListArray;
            int indArrayLen = indArray.Length;
            double factor = w;
            double[] mArray = new double[indArrayLen - 1];

            int a1;  //a1,a2都是临时变量
            int b1;
            double m1;

            for (int i = 0; i < indArrayLen - 1; i++)
            {
                a1 = indArray[i];
                b1 = indArray[i + 1];
                m1 = 0.000001;
                for (int j = a1; j < b1; j++)
                {
                    if (rmsGlobal[j] >= m1)
                    {
                        m1 = rmsGlobal[j];
                    }
                }
                mArray[i] = m1;
            }

            //定义区间比值因子
            double[] factorList = new double[indArrayLen - 2];
            double temp03;
            double temp04;
            for (int i = 0; i < indArrayLen - 2; i++)
            {
                temp03 = mArray[i];
                temp04 = mArray[i + 1];
                if (temp04 == 0)
                {
                    factorList[i] = 0;
                }
                else
                {
                    factorList[i] = temp03 / temp04;
                }
            }

            //对比值因子factorList进行排序得到factorDescend，其点数位置为indArrayDescend
            double[] factorDescend = factorList;
            int[] indArrayDescend = new int[indArrayLen - 2];
            for (int i = 0; i < indArrayLen - 2; i++)
            {
                indArrayDescend[i] = indArray[i + 1];
            }
            double temp05;
            int temp06;
            for (int i = 0; i < indArrayLen - 2; i++)
            {
                for (int j = i + 1; j < indArrayLen - 2; j++)
                {
                    if (factorDescend[i] <= factorDescend[j])
                    {
                        temp05 = factorDescend[i];
                        factorDescend[i] = factorDescend[j];
                        factorDescend[j] = temp05;
                        temp06 = indArray[i];
                        indArray[i] = indArray[j];
                        indArray[j] = temp06;
                    }
                }
            }

            int kt = 0;
            int IND = indArrayDescend[kt];  //坐标位置
            int Len = powerAll.Length;

            while (powerAll[IND] >= factor * powerAll[Len - 1])
            {
                if (kt == indArrayLen - 1)
                {
                    break;
                }
                kt += 1;
                IND = indArrayDescend[kt];
            }
            return IND;
        }
        #endregion
    }
}
