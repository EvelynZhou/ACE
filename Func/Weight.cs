using System;
using MathNet.Numerics;
using System.Windows;

namespace RideComfortUC.Func
{
    class Weight
    {
        float f1, f2, f3, f4, f5, f6, q1, q2, q3, q4, q5, q6;
        
        /// <summary>
        /// 频率加权网络，计算频率序列所在的加权系数
        /// </summary>
        /// <param name="weightType">权重类型，例如Wk，Wd……</param>
        /// <param name="freq">频率序列</param>
        public Weight(string weightType,float[] freq,out Complex32[] weighting)
        {
            int freqLength = freq.Length;
            Complex32[] p = new Complex32[freqLength];
            Complex32[] Hs = new Complex32[freqLength];
            Complex32[] Hh = new Complex32[freqLength];
            Complex32[] Hl = new Complex32[freqLength];
            Complex32[] Ht = new Complex32[freqLength];
            Complex32[] weight = new Complex32[freqLength];
            for (int i = 0; i < freqLength; i++)
            {
                p[i] = new Complex32(0, freq[i]);
            }
            bool typeFlag = String.Equals(weightType, "Wh", StringComparison.OrdinalIgnoreCase);
            if (!typeFlag)
            {
                switch (weightType)
                {
                    case "Wk":
                        f1 = 0.4F;   f2 = 100F;   f3 = 12.5F;  f4 = 12.5F;  q4 = 0.63F;
                        f5 = 2.37F;  q5 = 0.91F;  f6 = 3.35F;  q6 = 0.91F;
                        for (int i = 0; i < freqLength; i++)
                        {
                            Hs[i] = (1 + p[i] / (q5 * f5) + Complex32.Pow(p[i] / f5, 2)) / (1 + p[i] / (q6 * f6) + (p[i] / f6) * (p[i] / f6)) * (float)Math.Pow(f5 / f6, 2);
                        }
                        break;
                    case "Wd":
                        f1 = 0.4F;  f2 = 100F;  f3 = 2.0F;  f4 = 2.0F;  q4 = 0.63F;
                        for (int i = 0; i < freqLength; i++)
                        {
                            Hs[i] = 1;
                        }
                        break;
                    case "Wc":
                        f1 = 0.4F;  f2 = 100F;  f3 = 8F;    f4 = 8F;    q4 = 0.63F;
                        for (int i = 0; i < freqLength; i++)
                        {
                            Hs[i] = 1;
                        }
                        break;
                    case "We":
                        f1 = 0.4f;  f2 = 100f;  f3 = 1f;    f4 = 1f;    q4 = 0.63f;
                        for (int i = 0; i < freqLength; i++)
                        {
                            Hs[i] = 1;
                        }
                        break;
                    case "Wf":
                        f1 = 0.08f; f2 = 0.63f; f3 = 1 / 0.0f;  f4 = 0.25f; q4 = 0.86f;
                        f5 = 0.0625f;   q5 = 0.80f; f6 = 0.1f;  q6 = 0.80f;
                        for (int i = 0; i < freqLength; i++)
                        {
                            Hs[i] = (1 + p[i] / (q5 * f5) + Complex32.Pow(p[i] / f5, 2)) / (1 + p[i] / (q6 * f6) + Complex32.Pow(p[i] / f6, 2)) * (float)Math.Pow(f5 / f6, 2);
                        }
                        break;
                    case "Wj":
                        f1 = 0.4f;  f2 = 100f;
                        f5 = 3.75f; q5 = 0.91f; f6 = 5.32f; q6 = 0.91f;
                        for (int i = 0; i < freqLength; i++)
                        {
                            Hs[i] = (1 + p[i] / (q5 * f5) + Complex32.Pow(p[i] / f5, 2)) / (1 + p[i] / (q6 * f6) + Complex32.Pow(p[i] / f6, 2)) * (float)Math.Pow(f5 / f6, 2);
                        }
                        break;
                    default:
                        MessageBox.Show("Weight type is wrong.");
                        weighting = null;
                        return;
                }
                for (int i = 0; i < freqLength; i++)
                {
                    Hh[i] = 1 / (1 + ((float)Math.Sqrt(2)) * f1 / p[i] + Complex32.Pow(f1 / p[i], 2));
                    Hl[i] = 1 / (1 + ((float)Math.Sqrt(2)) * p[i] / f2 + Complex32.Pow(p[i] / f2, 2));
                    if (string.Equals(weightType,"Wj",StringComparison.OrdinalIgnoreCase))
                    {
                        Ht[i] = 1;
                    }
                    else
                    {
                        Ht[i] = (1 + p[i] / f3) / (1 + p[i] / (q4 * f4) + (p[i] * p[i] / f4 / f4));
                    }
                    weight[i] = Hh[i] * Hl[i] * Ht[i] * Hs[i];
                }
            }
            else
            {
                float k;
                switch (weightType)
                {
                    case "Wh":
                        f1 = 6.310f;    f2 = 1258.9f;   q1 = 0.71f; f3 = 15.915f;
                        f4 = 15.915f;   q2 = 0.64f;
                        k = 15.915f;
                        break;
                    default:
                        MessageBox.Show("Weight type is wrong.");
                        weighting = null;
                        return;
                }
                Complex32[] Hw = new Complex32[freqLength];
                for (int i = 0; i < freqLength; i++)
                {
                    Complex32 pp = p[i] * 2 * (float)Math.PI;
                    Hh[i] = (Complex32.Pow(pp, 2) * 4 * (float)Math.Pow(Math.PI, 2) * f2 * f2) / (Complex32.Pow(pp, 2) + 2 * (float)Math.PI * f1 * pp / q1 + 4 * (float)Math.Pow(Math.PI, 2) * f1 * f1) * (Complex32.Pow(pp, 2) + 2 * (float)Math.PI * f2 * pp / q1 + 4 * (float)Math.Pow(Math.PI, 2) * (float)Math.Pow(f2, 2));
                    Hw[i] = (pp + 2 * (float)Math.PI * f3) * 2 * (float)Math.PI * k * f4 / f3 / (Complex32.Pow(pp, 2) + 2 * (float)Math.PI * f4 * pp / q2 + 4 * (float)Math.Pow(Math.PI, 2) * f4 * f4);
                    weight[i] = Hh[i] * Hw[i];
                }
            }
            weighting = weight;
        }
    }
}
