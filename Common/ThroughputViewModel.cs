using Arction.Wpf.Charting;
using Arction.Wpf.Charting.Axes;
using Arction.Wpf.Charting.SeriesXY;
using Arction.Wpf.Charting.Views.ViewXY;
using Arction.Wpf.SignalProcessing;
using IVN.Infrastructure.Interfaces;
using IVN.Infrastructure.Models;
using NVH.DSP;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.ComponentModel;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Media;

namespace IVN.Modules.Administ.ViewModels
{
    public enum WavePlayState{
        Stop,
        Start,
        Pause
    }
    public class ThroughputViewModel : BindableBase
    {
        private byte[] waveHeader = new byte[] {
            0x52,0x49,0x46,0x46,//0－3 资源交换文件标志（RIFF）
            0x00,0x00,0x00,0x00,//4－7 little-endian 32-bit 正整数，整个档案的大小，扣掉辨识字符和长度，共8个字节,即从下一字节开始
            0x57,0x41,0x56,0x45,//8－11 WAV文件标志（WAVE）
            0x66,0x6d,0x74,0x20,//12－15 波形格式标志（fmt ），最后一位空格
            0x10,0x00,0x00,0x00,//16－19 过滤字节,（一般为00000010H）
            0x01,0x00,          //20-21 格式种类（值为1时，表示数据为线性PCM编码）
            0x01,0x00,          //22-23 通道数，单声道为1，双声道为2
            0xC0,0x5D,0x00,0x00,//24-27 采样率240000
            0x00,0x00,0x00,0x00,//28-31 波形数据传输率，每秒多少个字节
            0x02,0x00,          //32-33 数据块的调整数（按字节算的），其值为通道数×每样本的数据位值／8。播放软件需要一次处理多个该值大小的字节数据，以便将其值用于缓冲区的调整
            0x10,0x00,          //34-35 PCM位宽16
            0x64,0x61,0x74,0x61,//36-39 数据标记符＂data＂
            0x00,0x00,0x00,0x00 //40-43 语音数据长度，比档案大小少36
        };

        private IDBClientService _dbClientService;
        private IDataFileNode _dataFileNode;
        private Int16[][] intdata;
        private string _wavefn;
        private MediaPlayer _mplayer;
        private float[][] _throughputdata;
        private float[][] _rmsData;
        private int _selectChannelIndex=0;

        public string[] ChannelNames { get { return _dataFileNode.ChannelNames; } }
        public int SelectChannelIndex { get { return _selectChannelIndex; }
            set
            {
                if(value!=_selectChannelIndex)
                {
                    _selectChannelIndex = value;
                    UpdateChannel();
                }
            }
        }
        
        public DelegateCommand<Window> LoadedCommand { get; }
        public DelegateCommand<object> PlayPauseCommand { get; }
        public DelegateCommand<object> StopCommand { get; }

        public double SamplesALLT { get; }
        double _MediaPlayCurPos;
        public double MediaPlayCurPos
        {
            get { return _MediaPlayCurPos; }
            set
            {
                _MediaPlayCurPos = value;
                RaisePropertyChanged("MediaPlayCurPos");
            }
        }

        private WavePlayState _wavePlayState = WavePlayState.Stop;
        private Timer _ShowTimer=new Timer();
        private SampleDataSeries _throughputSeries;
        private SampleDataSeries _rmsSeries;
        private SampleDataSeries _maxrmsSeries;
        private int flen;
        private LightningChartUltimate _throughputGraph;
        private LightningChartUltimate _tfGraph;
        private LightningChartUltimate _timeGraph;
        private LightningChartUltimate _freqGraph;
        private IntensityGridSeries _tfSeries;
        private SpectrumCalculator spectrumCalculator;
        private double[] _aweightdb;
        private int fstarti;
        private SampleDataSeries _maxfreqSeries;
        private SampleDataSeries _freqSeries;
        private double[][] _tfdata;
        private SampleDataSeries _timeSeries;
        private LineSeriesCursor _tfgraph_verticalCursor;

        public string PlayPauseState
        {
            get
            {
                if (WavePlayState.Start != _wavePlayState)
                    return "播放";
                else
                    return "暂停";
            }
        }


        public ThroughputViewModel(IDBClientService dBClientService, IDataFileNode dn)
        {
            LoadedCommand = new DelegateCommand<Window>(OnLoaded);

            _dbClientService = dBClientService;
            _dataFileNode = dn;

            _throughputdata = _dbClientService.getTimeData(_dataFileNode, out intdata);
            _rmsData = _dbClientService.getRMS(dn.Id);

            SamplesALLT = _throughputdata[0].Length / dn.fs;
            MediaPlayCurPos = 0;

            int len = (int)(intdata[0].Length * 2) + 36;
            ValueTypeConverter.Int32ToByteArray(ref len, ref waveHeader, 4);
            len -= 36;
            ValueTypeConverter.Int32ToByteArray(ref len, ref waveHeader, 40);
            int fs = (int)dn.fs;
            ValueTypeConverter.Int32ToByteArray(ref fs, ref waveHeader, 24);
            int rate = 1 * fs * 2;
            ValueTypeConverter.Int32ToByteArray(ref rate, ref waveHeader, 28);
            Int16 block = 1 * 16 / 8;
            ValueTypeConverter.Int16ToByteArray(ref block, ref waveHeader, 32);

            _wavefn = dn.filename + dn.uploadDate.Ticks + ".wav";
            _mplayer = new MediaPlayer();
            _mplayer.Volume = 1;

            _mplayer.MediaEnded += _mplayer_MediaEnded;
            _mplayer.MediaFailed += _mplayer_MediaFailed;
            _mplayer.MediaOpened += _mplayer_MediaOpened;

            PlayPauseCommand = new DelegateCommand<object>(OnPlayPause);
            StopCommand = new DelegateCommand<object>(OnStop);
        }

        public void SetGraphs(LightningChartUltimate throughputGraph, LightningChartUltimate tfGraph,
            LightningChartUltimate timeGraph, LightningChartUltimate freqGraph)
        {
            int i,j;
            double fstart = _dataFileNode.fstart;
            double df = _dataFileNode.fs / _dataFileNode.Nfft / 2;

            float[] maxfreq=_dbClientService.getMaxFrf(_dataFileNode.Id);
            flen = maxfreq.Length;

            _throughputGraph = throughputGraph;
            _tfGraph = tfGraph;
            _timeGraph = timeGraph;
            _freqGraph = freqGraph;

            {
                _throughputGraph.BeginUpdate();
                _throughputGraph.Title.Visible = false;
                _throughputGraph.ViewXY.LegendBoxes[0].Position = LegendBoxPositionXY.TopRight;
                _throughputGraph.ViewXY.LegendBoxes[0].Layout = LegendBoxLayout.Vertical;
                AxisX axisX = _throughputGraph.ViewXY.XAxes[0];
                axisX.Title.Text = "时间(s)";
                AxisY axisY0 = _throughputGraph.ViewXY.YAxes[0];
                axisY0.Title.Text = "声压(Pa)";
                AxisY axisY1 = new AxisY(_throughputGraph.ViewXY);
                axisY1.Title.Text = "总声压级(dBA)";
                _throughputGraph.ViewXY.YAxes.Add(axisY1);

                _maxrmsSeries = new SampleDataSeries(_throughputGraph.ViewXY, axisX, axisY1);
                _maxrmsSeries.Title.Text = "最大总声压级";
                _maxrmsSeries.LineStyle.Color = Colors.Red;
                _maxrmsSeries.SampleFormat = SampleFormat.SingleFloat;
                _maxrmsSeries.FirstSampleTimeStamp = _dataFileNode.Nfft / _dataFileNode.fs / 2;
                _maxrmsSeries.SamplingFrequency = _dataFileNode.fs / _dataFileNode.FrameDN;
                _throughputGraph.ViewXY.SampleDataSeries.Add(_maxrmsSeries);

                _rmsSeries = new SampleDataSeries(_throughputGraph.ViewXY, axisX, axisY1);
                _rmsSeries.Title.Text = "总声压级";
                _rmsSeries.LineStyle.Color = Colors.Blue;
                _rmsSeries.SampleFormat = SampleFormat.SingleFloat;
                _rmsSeries.FirstSampleTimeStamp = _maxrmsSeries.FirstSampleTimeStamp;
                _rmsSeries.SamplingFrequency = _maxrmsSeries.SamplingFrequency;
                _throughputGraph.ViewXY.SampleDataSeries.Add(_rmsSeries);

                _throughputgraph_verticalCursor = new LineSeriesCursor(_throughputGraph.ViewXY, axisX);
                _throughputgraph_verticalCursor.Style = CursorStyle.VerticalNoTracking;
                _throughputgraph_verticalCursor.LineStyle.Color = Colors.White;
                _throughputgraph_verticalCursor.LineStyle.Pattern = LinePattern.Dot;
                _throughputgraph_verticalCursor.LineStyle.Width = 3;
                _throughputgraph_verticalCursor.ValueAtXAxis = 10;
                _throughputgraph_verticalCursor.MouseHighlight = MouseOverHighlight.None;
                _throughputGraph.ViewXY.LineSeriesCursors.Add(_throughputgraph_verticalCursor);

                _throughputSeries = new SampleDataSeries(_throughputGraph.ViewXY, axisX, axisY0);
                _throughputSeries.Title.Text = "时域";
                _throughputSeries.LineStyle.Color = Colors.Orange;
                _throughputSeries.SampleFormat = SampleFormat.SingleFloat;
                _throughputSeries.FirstSampleTimeStamp = 0;
                _throughputSeries.SamplingFrequency = _dataFileNode.fs;
                _throughputGraph.ViewXY.SampleDataSeries.Add(_throughputSeries);

                float[] _maxrmsData = new float[_dataFileNode.NFrame];
                _rmsData[0].CopyTo(_maxrmsData, 0);
                for (i = 1; i < _dataFileNode.NFrame; i++)
                {
                    for (j = 0; j < _dataFileNode.ChannelNum; j++)
                    {
                        if (_rmsData[j][i] > _maxrmsData[i])
                            _maxrmsData[i] = _rmsData[j][i];
                    }
                }
                _maxrmsSeries.SamplesSingle = _maxrmsData;

                _throughputGraph.EndUpdate();
            }

            {
                _tfGraph.BeginUpdate();
                _tfGraph.Title.Visible = false;
                //_tfGraph.ViewXY.LegendBoxes[0].Visible = false;
                _tfGraph.ViewXY.LegendBoxes[0].Position = LegendBoxPositionXY.TopRight;
                _tfGraph.ViewXY.LegendBoxes[0].ShowCheckboxes = false;
                AxisX axisX = _tfGraph.ViewXY.XAxes[0];
                axisX.Title.Text = "时间(s)";
                AxisY axisY = _tfGraph.ViewXY.YAxes[0];
                axisY.Title.Text = "频率(Hz)";

                _tfSeries = new IntensityGridSeries(_throughputGraph.ViewXY, axisX, axisY);
                _tfSeries.PixelRendering = true;
                _tfSeries.ContourLineType = ContourLineTypeXY.None;
                _tfSeries.ValueRangePalette = CreatePalette(_tfSeries, 20, 100);
                _tfSeries.SetRangesXY(_dataFileNode.Nfft/_dataFileNode.fs/2, 
                    _dataFileNode.Nfft / _dataFileNode.fs / 2+(_dataFileNode.NFrame-1)*_dataFileNode.FrameDN/_dataFileNode.fs,
                    fstart, fstart+(flen-1)*df);
                _tfSeries.MouseInteraction = false;
                _tfSeries.LegendBoxUnits = null;
                _tfSeries.LegendBoxValuesFormat = "0";
                //_tfSeries.Title.Visible = false;
                _tfSeries.Title.Text = "声压级(dBA)";
                _tfGraph.ViewXY.IntensityGridSeries.Add(_tfSeries);

                _tfgraph_verticalCursor = new LineSeriesCursor(_tfGraph.ViewXY, axisX);
                _tfgraph_verticalCursor.Style = CursorStyle.VerticalNoTracking;
                _tfgraph_verticalCursor.LineStyle.Color = Colors.White;
                _tfgraph_verticalCursor.LineStyle.Pattern = LinePattern.Dot;
                _tfgraph_verticalCursor.LineStyle.Width = 3;
                _tfgraph_verticalCursor.ValueAtXAxis = _throughputgraph_verticalCursor.ValueAtXAxis;
                _tfgraph_verticalCursor.MouseHighlight = MouseOverHighlight.None;
                _tfGraph.ViewXY.LineSeriesCursors.Add(_tfgraph_verticalCursor);
                
                _tfgraph_horizontalCursor = new ConstantLine(_tfGraph.ViewXY, axisX, axisY);
                _tfgraph_horizontalCursor.LineStyle.Color = Colors.White;
                _tfgraph_horizontalCursor.LineStyle.Width = 3;
                _tfgraph_horizontalCursor.LineStyle.Pattern = LinePattern.Dot;
                _tfgraph_horizontalCursor.Value = 2000;
                _tfgraph_horizontalCursor.ShowInLegendBox = false;
                _tfgraph_horizontalCursor.MouseHighlight = MouseOverHighlight.None;
                _tfGraph.ViewXY.ConstantLines.Add(_tfgraph_horizontalCursor);

                spectrumCalculator = new SpectrumCalculator();
                double[] _aweight = Weight.GetWeightData(FreqWeightType.AWeight, fstart, df, flen);
                _aweightdb = new double[_aweight.Length];

                for (i = 0; i < flen; i++)
                {
                    _aweightdb[i] = 20 * Math.Log10(_aweight[i] * Math.Sqrt(2) / _dataFileNode.Nfft) + 93.9794;//93.9794为2e-5Pa参考
                }
                fstarti = (int)(fstart / df);

                ////Configure legend
                _tfGraph.ViewXY.LegendBoxes[0].IntensityScales.ScaleSizeDim1 = 400;
                _tfGraph.ViewXY.LegendBoxes[0].Layout = LegendBoxLayout.Horizontal;
                //_tfGraph.ViewXY.LegendBoxes[0].Offset = new PointIntXY(-15, -70);
                _tfGraph.ViewXY.LegendBoxes[0].ResetLocation();

                _tfGraph.EndUpdate();
            }

            {
                _timeGraph.BeginUpdate();
                _timeGraph.Title.Visible = false;
                _timeGraph.ViewXY.LegendBoxes[0].Position = LegendBoxPositionXY.TopRight;
                _timeGraph.ViewXY.LegendBoxes[0].Visible = false;
                AxisX axisX = _timeGraph.ViewXY.XAxes[0];
                axisX.Title.Text = "时间(s)";
                AxisY axisY = _timeGraph.ViewXY.YAxes[0];
                axisY.Title.Text = "声压(Pa)";

                _timegraph_verticalCursor = new LineSeriesCursor(_timeGraph.ViewXY, axisX);
                _timegraph_verticalCursor.Style = CursorStyle.VerticalNoTracking;
                _timegraph_verticalCursor.LineStyle.Color = Colors.White;
                _timegraph_verticalCursor.LineStyle.Pattern = LinePattern.Dot;
                _timegraph_verticalCursor.LineStyle.Width = 3;
                _timegraph_verticalCursor.ValueAtXAxis = _dataFileNode.Nfft/_dataFileNode.fs/2;
                _timegraph_verticalCursor.MouseHighlight = MouseOverHighlight.None;
                _timeGraph.ViewXY.LineSeriesCursors.Add(_timegraph_verticalCursor);

                _timeSeries = new SampleDataSeries(_timeGraph.ViewXY, axisX, axisY);
                _timeSeries.Title.Text = "时域";
                _timeSeries.LineStyle.Color = Colors.Orange;
                _timeSeries.SampleFormat = SampleFormat.SingleFloat;
                _timeSeries.FirstSampleTimeStamp = 0;
                _timeSeries.SamplingFrequency = _dataFileNode.fs;
                _timeGraph.ViewXY.SampleDataSeries.Add(_timeSeries);

                _timeGraph.ViewXY.ZoomToFit();

                _timeGraph.EndUpdate();
            }

            {
                _freqGraph.BeginUpdate();
                _freqGraph.Title.Visible = false;
                _freqGraph.ViewXY.LegendBoxes[0].Position = LegendBoxPositionXY.TopRight;
                _freqGraph.ViewXY.LegendBoxes[0].Layout = LegendBoxLayout.Vertical;
                AxisX axisX = _freqGraph.ViewXY.XAxes[0];
                axisX.Title.Text = "频率(Hz)";
                AxisY axisY = _freqGraph.ViewXY.YAxes[0];
                axisY.Title.Text = "声压级(dBA)";

                _freqgraph_verticalCursor = new LineSeriesCursor(_freqGraph.ViewXY, axisX);
                _freqgraph_verticalCursor.Style = CursorStyle.VerticalNoTracking;
                _freqgraph_verticalCursor.LineStyle.Color = Colors.White;
                _freqgraph_verticalCursor.LineStyle.Pattern = LinePattern.Dot;
                _freqgraph_verticalCursor.LineStyle.Width = 3;
                _freqgraph_verticalCursor.ValueAtXAxis = _tfgraph_horizontalCursor.Value;
                _freqgraph_verticalCursor.MouseHighlight = MouseOverHighlight.None;
                _freqGraph.ViewXY.LineSeriesCursors.Add(_freqgraph_verticalCursor);

                _maxfreqSeries = new SampleDataSeries(_freqGraph.ViewXY, axisX, axisY);
                _maxfreqSeries.Title.Text = "最大值";
                _maxfreqSeries.LineStyle.Color = Colors.Red;
                _maxfreqSeries.SampleFormat = SampleFormat.SingleFloat;
                _maxfreqSeries.FirstSampleTimeStamp = fstart;
                _maxfreqSeries.SamplingFrequency = 1/df;
                _freqGraph.ViewXY.SampleDataSeries.Add(_maxfreqSeries);
                _maxfreqSeries.SamplesSingle = maxfreq;

                _freqSeries = new SampleDataSeries(_freqGraph.ViewXY, axisX, axisY);
                _freqSeries.Title.Text = "频谱";
                _freqSeries.LineStyle.Color = Colors.Orange;
                _freqSeries.SampleFormat = SampleFormat.DoubleFloat;
                _freqSeries.FirstSampleTimeStamp = fstart;
                _freqSeries.SamplingFrequency = 1/df;
                _freqGraph.ViewXY.SampleDataSeries.Add(_freqSeries);

                _freqGraph.ViewXY.ZoomToFit();

                _freqGraph.EndUpdate();
            }

            _ShowTimer.Interval = _dataFileNode.FrameDN / _dataFileNode.fs * 1000/2;
            _ShowTimer.Elapsed += _ShowTimer_Elapsed;

            _tfgraph_verticalCursor.PositionChanged += verticalCursor_PositionChanged;
            _tfgraph_horizontalCursor.ValueChanged += horizontalCursor_ValueChanged;

            UpdateChannel();
        }

        private void _ShowTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _tfgraph_verticalCursor.Dispatcher.Invoke(()=>
            {
                MediaPlayCurPos = _mplayer.Position.TotalSeconds;
                _tfgraph_verticalCursor.ValueAtXAxis = MediaPlayCurPos;
                //Console.WriteLine(MediaPlayCurPos);
            });
        }

        private void horizontalCursor_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (_throughputdata == null)
                return;

            _freqgraph_verticalCursor.ValueAtXAxis = _tfgraph_horizontalCursor.Value;
        }

        int oldiColIndex = -1;
        private LineSeriesCursor _freqgraph_verticalCursor;
        private ConstantLine _tfgraph_horizontalCursor;
        private LineSeriesCursor _throughputgraph_verticalCursor;
        private LineSeriesCursor _timegraph_verticalCursor;

        private void verticalCursor_PositionChanged(object sender, PositionChangedEventArgs e)
        {
            if (_tfdata == null)
                return;

            //Solve nearest row data row 
            double dColStep = (_tfSeries.RangeMaxX - _tfSeries.RangeMinX) / (double)(_dataFileNode.NFrame - 1);
            int iColIndex = (int)((e.NewValue - _tfSeries.RangeMinX) / dColStep);
            if (iColIndex < 0)
                iColIndex = 0;
            if (iColIndex > _dataFileNode.NFrame - 1)
                iColIndex = _dataFileNode.NFrame - 1;

            if (oldiColIndex == iColIndex)
                return;
            else
                oldiColIndex = iColIndex;

            float[] data = new float[_dataFileNode.Nfft];
            Array.Copy(_throughputdata[_selectChannelIndex], iColIndex * _dataFileNode.FrameDN, data, 0, _dataFileNode.Nfft);
            _timeGraph.BeginUpdate();
            //_timeSeries.FirstSampleTimeStamp = iColIndex * _dataFileNode.FrameDN / _dataFileNode.fs;
            _timeSeries.SamplesSingle = data;
            _timeGraph.ViewXY.ZoomToFit();
            _timeGraph.EndUpdate();

            _freqGraph.BeginUpdate();
            _freqSeries.SamplesDouble = _tfdata[iColIndex];
            _freqGraph.ViewXY.ZoomToFit();
            _freqGraph.EndUpdate();

            _throughputgraph_verticalCursor.ValueAtXAxis = iColIndex * _dataFileNode.FrameDN / _dataFileNode.fs
                + _dataFileNode.Nfft / _dataFileNode.fs / 2;
        }

        void UpdateChannel()
        {
            int i, j;

            _mplayer.Close();

            _throughputGraph.BeginUpdate();
            _throughputSeries.SamplesSingle = _throughputdata[_selectChannelIndex];
            _rmsSeries.SamplesSingle = _rmsData[_selectChannelIndex];
            _throughputGraph.ViewXY.ZoomToFit();
            _throughputGraph.EndUpdate();

            _tfGraph.BeginUpdate();
            _tfdata = new double[_dataFileNode.NFrame][];
            float[] curdata = new float[_dataFileNode.Nfft];
            float[] fdata = null;
            int curind = 0;
            for (i = 0; i < _dataFileNode.NFrame; i++)
            {
                Array.Copy(_throughputdata[_selectChannelIndex], curind, curdata, 0, _dataFileNode.Nfft);
                curind += _dataFileNode.FrameDN;

                spectrumCalculator.PowerSpectrum(curdata, out fdata);
                _tfdata[i] = new double[flen];
                for (j = 0; j < flen; j++)
                {
                    _tfdata[i][j] = 20 * Math.Log10(fdata[j + fstarti]) + _aweightdb[j];
                }
            }
            _tfSeries.SetValuesData(_tfdata, IntensityGridValuesDataOrder.ColumnsRows);
            _tfGraph.ViewXY.ZoomToFit();
            _tfGraph.EndUpdate();

            oldiColIndex = -1;
            verticalCursor_PositionChanged(null, new PositionChangedEventArgs() { NewValue =_tfgraph_verticalCursor.ValueAtXAxis});
        }

        private ValueRangePalette CreatePalette(SeriesBaseXY ownerSeries, double min, double max)
        {
            ValueRangePalette palette = new ValueRangePalette(ownerSeries);
            double range = max - min;
            palette.Steps.Clear();
            palette.Steps.Add(new PaletteStep(palette, Color.FromRgb(0, 0, 0), min));
            palette.Steps.Add(new PaletteStep(palette, Color.FromRgb(0, 0, 255), min + 1 * range/6));
            palette.Steps.Add(new PaletteStep(palette, Color.FromRgb(0, 255, 255), min + 2 * range / 6));
            palette.Steps.Add(new PaletteStep(palette, Color.FromRgb(0, 255, 0), min + 3 * range / 6));
            palette.Steps.Add(new PaletteStep(palette, Color.FromRgb(255, 255, 0), min + 4 * range / 6));
            palette.Steps.Add(new PaletteStep(palette, Color.FromRgb(255, 0, 0), min + 5 * range / 6));
            palette.Steps.Add(new PaletteStep(palette, Color.FromRgb(255, 0, 255), max));
            palette.Type = PaletteType.Gradient;
            palette.MinValue = min;
            return palette;
        }

        private void OnStop(object obj)
        {
            _ShowTimer.Stop();
            _mplayer.Stop();
            _wavePlayState = WavePlayState.Stop;
            RaisePropertyChanged("PlayPauseState");
        }

        private void OnPlayPause(object obj)
        {
            if (_wavePlayState == WavePlayState.Stop)
            {
                GenerateWavFile(_selectChannelIndex);
                _mplayer.Play();
                _wavePlayState = WavePlayState.Start;

                _ShowTimer.Start();
            }
            else if (_wavePlayState == WavePlayState.Start)
            {
                _mplayer.Pause();
                _wavePlayState = WavePlayState.Pause;
                _ShowTimer.Stop();
            }
            else if (_wavePlayState == WavePlayState.Pause)
            {
                _mplayer.Play();
                _wavePlayState = WavePlayState.Start;
                _ShowTimer.Start();
            }
            RaisePropertyChanged("PlayPauseState");
        }

        private void OnLoaded(Window wnd)
        {
            wnd.Closing += wnd_Closing;
        }

        private void wnd_Closing(object sender, CancelEventArgs e)
        {
            _ShowTimer.Stop();
            _mplayer.Close();
            if (File.Exists(_wavefn))
                File.Delete(_wavefn);
        }
        
        private void _mplayer_MediaOpened(object sender, EventArgs e)
        {
            //_wavePlayState = WavePlayState.Start;
            //RaisePropertyChanged("PlayPauseState");
            Console.WriteLine("_mplayer_MediaOpened");
        }

        private void _mplayer_MediaFailed(object sender, ExceptionEventArgs e)
        {
            _ShowTimer.Stop();
            _mplayer.Close();
            _wavePlayState = WavePlayState.Stop;
            RaisePropertyChanged("PlayPauseState");
            Console.WriteLine("_mplayer_MediaFailed");
        }

        private void _mplayer_MediaEnded(object sender, EventArgs e)
        {
            _ShowTimer.Stop();
            _mplayer.Close();
            _wavePlayState = WavePlayState.Stop;
            RaisePropertyChanged("PlayPauseState");
            Console.WriteLine("_mplayer_MediaEnded");
        }
        private void GenerateWavFile(int channel)
        {
            _mplayer.Close();
            System.Threading.Thread.Sleep(100);

            byte[] bytedata = ValueTypeConverter.Int16ArrayToByteArray(ref intdata[channel]);

            FileMode fm = File.Exists(_wavefn) ? FileMode.Truncate : FileMode.Create;
            using (FileStream filestream = new FileStream(_wavefn, fm, FileAccess.ReadWrite))
            {
                filestream.Write(waveHeader, 0, waveHeader.Length);
                filestream.Write(bytedata, 0, bytedata.Length);
                filestream.Flush();
                filestream.Close();
                filestream.Dispose();
            }
            _mplayer.Open(new Uri(_wavefn, UriKind.RelativeOrAbsolute));
        }
    }
}
