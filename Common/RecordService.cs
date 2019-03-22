using Arction.Wpf.SignalProcessing;
using IVN.Infrastructure.Events;
using IVN.Infrastructure.Interfaces;
using NVH.DSP;
using Prism.Events;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace IVN.Modules.RecordService
{
    [Export(typeof(IRecordService))]
    public class RecordService : IRecordService
    {
        [ImportingConstructor]
        public RecordService(ISampleService sampleService, IEventAggregator eventAggregator)
        {
            int i;

            _sampleService = sampleService;

            ChannelNames = new string[_sampleService.ChannelNum];
            ChannelNames[0] = "主驾驶";
            ChannelNames[1] = "副驾驶";
            ChannelNames[2] = "左后座";
            ChannelNames[3] = "右后座";

            Nfft = 8192;
            FrameDN = _sampleService.PackLen;
            
            _SampleT = Nfft / _sampleService.fs;

            double df = _sampleService.fs / Nfft/2;
            _fstarti = (int)Math.Floor(_fstart / df);
            _fstart = _fstarti * df;
            _flen = (int)Math.Ceiling(_fstop / df);
            if (_flen >= Nfft)
                _flen = Nfft - 1;
            _fstop = _flen * df;
            _flen = _flen - _fstarti + 1;

            _aweight = Weight.GetWeightData(FreqWeightType.AWeight, fstart, df, _flen);
            _aweightdb = new double[_aweight.Length];

            for (i = 0; i < _flen; i++)
            {
                _aweightdb[i] = 20 * Math.Log10(_aweight[i]*Math.Sqrt(2)/ Nfft) + 93.9794;//93.9794为2e-5Pa参考
                _aweight[i] *= _aweight[i];//93.9794为2e-5Pa参考
            }
            _overallFix = 20 * Math.Log10(Math.Sqrt(2) / Nfft) + 93.9794;

            _maxFrfData = new float[_flen];

            _spectrumCalculator = new SpectrumCalculator();

            Sens = new double[4];
            for (i = 0; i < 4; i++)
            {
                Sens[i] =  0.001764087*2/10.0909;// 5.0f / 32768 * 11.56112242;5.0f / 32768;//
            }

            _oldtimeData = new Int16[ChannelNum][];
            for (i=0;i<ChannelNum;i++)
            {
                _oldtimeData[i] = new Int16[Nfft];
            }

            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<SampleServiceStateChangedEvent>().Subscribe(OnSampleStateChanged, ThreadOption.BackgroundThread);
            _eventAggregator.GetEvent<SampleServiceDataEvent>().Subscribe(OnDataSampled, ThreadOption.BackgroundThread);
        }

        private void OnSampleStateChanged(SampleServiceStateType obj)
        {
            _sampledLen = 0;
        }

        string _recordFileName = System.AppDomain.CurrentDomain.BaseDirectory + "tempdata.ivn";
        BinaryFormatter binaryFormatter = null;
        FileStream binaryFS = null;

        UInt64 _sampledLen = 0;
        double _fstart = 20;
        double _fstop = 8000;
        int _fstarti = 0;
        int _flen = 0;

        double _SampleT = 0;

        bool _bRecording = false;
        bool _bRecordFisrt = true;
        private IEventAggregator _eventAggregator;
        private ISampleService _sampleService;
        double[] _aweightdb = null;
        double[] _aweight = null;
        double _overallFix = 0;
        SpectrumCalculator _spectrumCalculator;

        float[] _maxFrfData;
        private Int16[][] _oldtimeData;

        public bool bRecording
        {
            get { return _bRecording; }
            private set
            {
                if (_bRecording != value)
                {
                    _bRecording = value;
                    if (value)
                    {
                        _bRecordFisrt = true;
                        _eventAggregator.GetEvent<RecordServiceStateChangedEvent>().Publish(null);
                    }
                }
            }
        }

        public bool StartRecord(string FileName)
        {
            if (!_sampleService.bConnected)
                return true;

            if (_bRecording)
                return false;

            for (int i = 0; i < _flen; i++)
                _maxFrfData[i] = 0;

            try
            {
                if (File.Exists(_recordFileName))
                    binaryFS = new FileStream(_recordFileName, FileMode.Truncate, FileAccess.ReadWrite);
                else
                    binaryFS = new FileStream(_recordFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                binaryFormatter = new BinaryFormatter();
                RecordedFrames = 0;
                bRecording = true;
                return false;
            }
            catch (Exception)
            {
                binaryFormatter = null;
                binaryFS = null;
                bRecording = false;
                return false;
            }
        }

        private void OnDataSampled(short[][] intdata)
        {
            Parallel.For(0, ChannelNum, i =>
            {
                Array.Copy(_oldtimeData[i], FrameDN, _oldtimeData[i], 0, Nfft - FrameDN);
                Array.Copy(intdata[i], 0, _oldtimeData[i], Nfft - FrameDN, FrameDN);
            });

            _sampledLen += (UInt64)FrameDN;
            if (_sampledLen < (UInt64)Nfft)
                return;

            float[][] timeData = new float[ChannelNum][];
            float[][] frfData = new float[ChannelNum][];
            float[] rms = new float[ChannelNum];
            bool bMaxFrfChanged = false;
            Parallel.For(0, ChannelNum, i =>
              {
                  double crms = 0;
                  timeData[i] = new float[Nfft];
                  float[] dataf = new float[Nfft];
                  frfData[i] = new float[_flen];
                  int j = 0;
                  int mean = 0;

                  Array.Copy(_oldtimeData[i], FrameDN, _oldtimeData[i], 0, Nfft - FrameDN);
                  Array.Copy(intdata[i], 0, _oldtimeData[i], Nfft - FrameDN, FrameDN);

                  for (j = 0; j < Nfft; j++)
                  {
                      mean+= _oldtimeData[i][j];
                  }
                  mean /= Nfft;

                  for (j = 0; j < Nfft; j++)
                  {
                      timeData[i][j] = (float)((_oldtimeData[i][j]-mean) * Sens[i]);
                  }

                  _spectrumCalculator.PowerSpectrum(timeData[i], out dataf);
                  for (j = 0; j < _flen; j++)
                  {
                      crms += dataf[j + _fstarti]* dataf[j + _fstarti] * _aweight[j];
                      //frfData[i][j] = (float)(dataf[j + _fstarti]/_Nfft*Math.Sqrt(2));//有效值谱
                      frfData[i][j] = (float)(20 * Math.Log10(dataf[j + _fstarti]) + _aweightdb[j]);
                      if(bRecording)
                      {
                          if (frfData[i][j] > _maxFrfData[j])
                          {
                              bMaxFrfChanged = true;
                              _maxFrfData[j] = frfData[i][j];
                          }
                      }
                    }
                  rms[i] = (float)(10 * Math.Log10(crms) + _overallFix);
              });

            if (_bRecording && binaryFormatter != null)
            {
                lock (binaryFS)
                RecordedFrames++;
                if(_bRecordFisrt)
                {
                    _bRecordFisrt = false;
                    binaryFormatter.Serialize(binaryFS, _oldtimeData);
                }
                else
                    binaryFormatter.Serialize(binaryFS, intdata);
            }
            _eventAggregator.GetEvent<RecordServiceDataEvent>().Publish(
                new RecordServiceDataEventData
                {
                    TimeData = timeData,
                    FrfData = frfData,
                    RMS = rms,
                    MaxFrfData= (bRecording&&bMaxFrfChanged)?_maxFrfData:null
                });
        }

        public bool StopRecord()
        {
            if (!_bRecording)
                return false;

            bRecording = false;
            _eventAggregator.GetEvent<RecordServiceStateChangedEvent>().Publish(binaryFS);

            if (binaryFS != null)
            {
                binaryFS.Close();
                binaryFS = null;
            }
            if (binaryFormatter != null)
                binaryFormatter = null;

            return false;
        }

        public int RecordedFrames
        {
            get;
            private set;
        }

        public double[] Sens
        {
            get;
            private set;
        }

        public double fstart
        {
            get { return _fstart; }
        }

        public double fstop
        {
            get { return _fstop; }
        }

        public float[] MaxFrf
        {
            get { return _maxFrfData; }
        }

        public int ChannelNum { get { return _sampleService.ChannelNum; } }

        public string[] ChannelNames
        {
            get;private set;
        }

        public string ServerIP { get { return _sampleService.ServerIP; } }

        public bool bStarted { get { return _sampleService.bStarted; } }

        public bool bConnected { get { return _sampleService.bConnected; } }

        public double fs { get { return _sampleService.fs; } }

        public int Nfft { get; private set; }

        public int FrameDN { get; private set; }
    }
}
