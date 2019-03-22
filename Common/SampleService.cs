using IVN.Infrastructure.Events;
using IVN.Infrastructure.Interfaces;
using Prism.Events;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;

namespace IVN.Modules.SampleService
{
    [Export(typeof(ISampleService))]
    public class SampleService : TcpListener, ISampleService
    {
        const double _fs = 24000;
        const double dt = 1 / _fs;
        const int _ChannelNum = 4;
        const int _packLen = (int)_fs/1000*100;// (int)fs / 2;
        const int readbuffersize = _packLen * 2 * _ChannelNum;

        IEventAggregator _eventAggregator;

        bool bAuthed = false;
        byte[] readbuffer1 = new byte[readbuffersize];
        byte[] readbuffer2 = new byte[readbuffersize];
        byte[] curreadbuffer = null;
        byte[] readedbuffer = null;

        int curreadlen;

        TcpClient client = null;
        Timer timer = new Timer(5000);
        System.Threading.Thread ByteReceiveThread = null;

        [ImportingConstructor]
        public SampleService(IEventAggregator eventAggregator)
            : base(Dns.GetHostAddresses(Dns.GetHostName()).First(e => e.AddressFamily.ToString().CompareTo("InterNetwork") == 0), 9090)
        {
            _eventAggregator = eventAggregator;

            IPAddress address = Dns.GetHostAddresses(Dns.GetHostName()).First(e => e.AddressFamily.ToString().CompareTo("InterNetwork") == 0);
            _ServerIP = address.ToString();

            Server.ReceiveBufferSize = (int)(_fs * _ChannelNum * 2 * 256);
            timer.AutoReset = false;
            timer.Elapsed += timer_Elapsed;
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            bConnected = false;
        }

        private void Disconnect()
        {
            Console.WriteLine("断开连接");
            if (client != null)
            {
                client.Close();
                client = null;
            }
            bAuthed = false;

            if (ByteReceiveThread != null)
            {
                ByteReceiveThread.Abort();
                ByteReceiveThread = null;
            }

            if (bStarted)
                BeginAcceptTcpClient(OnAcceptTcpClient, this);
        }

        string _ServerIP = "";
        public string ServerIP
        {
            get
            {
                return _ServerIP;
            }
        }

        public bool StartServer()
        {
            if (bStarted)
                return false;

            try
            {
                Start();
                client = null;
                bStarted = true;
                BeginAcceptTcpClient(OnAcceptTcpClient, this);
                return false;
            }
            catch (Exception ex)
            {
                bStarted = false;
                Console.WriteLine("StartSample error:" + ex.Message);
                return true;
            }
        }

        private void OnAcceptTcpClient(IAsyncResult ar)
        {
            SampleService sv = (SampleService)(ar.AsyncState);
            if (sv.bStarted)
            {
                try
                {
                    sv.client = sv.EndAcceptTcpClient(ar);
                    sv.bConnected = true;
                    sv.timer.Start();
                    //开始异步读取数据  
                    sv.client.GetStream().BeginRead(sv.readbuffer1, 0, readbuffersize,
                        OnDataReceived, sv);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("AcceptTcpClient Error:" + ex.Message);
                }
            }
        }

        private void OnDataReceived(IAsyncResult ar)
        {
            SampleService sv = (SampleService)(ar.AsyncState);
            if (!sv.bStarted || sv.client == null)
                return;

            int recv = 0;
            try
            {
                recv = sv.client.GetStream().EndRead(ar);
            }
            catch (Exception ex)
            {
                recv = 0;
                Console.WriteLine("HandleDataReceived:" + ex.Message);
            }

            try
            {
                if (recv != 0)
                {
                    if (sv.bAuthed)
                    {
                        if (recv % 2 != 0)
                            Console.WriteLine("接收到:" + recv);

                        sv.curreadlen += recv;
                        if (sv.curreadlen == readbuffersize)
                        {
                            if (sv.readedbuffer == null)
                            {
                                sv.readedbuffer = sv.curreadbuffer;
                            }
                            else
                            {
                                Console.WriteLine("data lasted");
                            }
                            if (sv.curreadbuffer == sv.readbuffer1)
                                sv.curreadbuffer = sv.readbuffer2;
                            else
                                sv.curreadbuffer = sv.readbuffer1;
                            //static int fm = 0;
                            //Console::WriteLine("frame:" + (++fm));
                            sv.curreadlen = 0;
                        }

                        sv.client.GetStream().BeginRead(sv.curreadbuffer, sv.curreadlen,
                            readbuffersize - sv.curreadlen,
                            OnDataReceived, sv);
                    }
                    else
                    {
                        if (recv == 19)
                        {
                            byte[] temp = new byte[recv];
                            Array.Copy(sv.readbuffer1, temp, recv);
                            string sn = Encoding.Default.GetString(temp);
                            if (sn.CompareTo("sn:testtesttesttest") == 0)
                            {
                                sv.timer.Stop();
                                sv.bAuthed = true;
                                Console.WriteLine("验证成功");
                                byte[] res = Encoding.Default.GetBytes("snok");
                                sv.client.GetStream().BeginWrite(res, 0, res.GetLength(0), null, null);

                                sv.InitSample();

                                sv.client.GetStream().BeginRead(sv.curreadbuffer, 0, readbuffersize,
                                    OnDataReceived, sv);

                                return;
                            }
                        }
                        Console.WriteLine("验证无效");
                        sv.bConnected = false;
                    }
                }
                else
                    sv.bConnected = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("handle receive error:" + ex.Message);
            }
        }

        private static void ProcessByte_fun(object obj)
        {
            byte[] bytedata = new byte[readbuffersize];
            SampleService sv = (SampleService)obj;
            sv.timer.Start();
            while (sv.bAuthed)
            {
                if (sv.readedbuffer != null)
                {
                    int i, j, k = 0;
                    sv.timer.Stop();
                    sv.readedbuffer.CopyTo(bytedata, 0);
                    sv.readedbuffer = null;
                    Int16[][] data = new Int16[_ChannelNum][];
                    for(j=0;j<_ChannelNum;j++)
                        data[j]=new Int16[_packLen];

                    for (i = 0; i < _packLen; i++)
                    {
                        for (j = 0; j < _ChannelNum; j++)
                        {
                            data[j][i] = (Int16)((bytedata[k++]) | (bytedata[k++] << 8));// *5.0f / 32768;
                                                                                         //Console::WriteLine(j + ":" + d1 + " " + d2 + " " + data[j][i]);
                        }
                    }
                    sv._eventAggregator.GetEvent<SampleServiceDataEvent>().Publish(data);
                    sv.timer.Start();
                }
            }
            sv.timer.Stop();
        }

        private void InitSample()
        {
            curreadlen = 0;
            curreadbuffer = readbuffer1;
            readedbuffer = null;

            //调用Start方法执行线程
            ByteReceiveThread = new System.Threading.Thread(ProcessByte_fun);
            ByteReceiveThread.Priority = System.Threading.ThreadPriority.Highest;
            ByteReceiveThread.Start(this);
        }

        public bool StopServer()
        {
            if (!bStarted)
                return false;

            Console.WriteLine("停止服务");
            bConnected = false;
            StopSample();
            Stop();

            bStarted = false;
            return false;
        }

        public bool StartSample()
        {
            bSampling = true;
            return false;
        }

        public bool StopSample()
        {
            bSampling = false;
            return false;
        }

        bool _bStarted = false;
        public bool bStarted
        {
            get { return _bStarted; }
            private set
            {
                if (_bStarted != value)
                {
                    _bStarted = value;
                    _eventAggregator.GetEvent<SampleServiceStateChangedEvent>().Publish(
                        SampleServiceStateType.Start);
                }
            }
        }

        bool _bConnected = false;
        public bool bConnected
        {
            get { return _bConnected; }
            private set
            {
                if (_bConnected != value)
                {
                    if (!value)
                    {
                        Disconnect();
                    }
                    _bConnected = value;
                    _eventAggregator.GetEvent<SampleServiceStateChangedEvent>().Publish(
                        SampleServiceStateType.Connect);
                }
            }
        }

        bool _bSampling = false;
        public bool bSampling
        {
            get { return _bSampling; }
            private set
            {
                if (_bSampling != value)
                {
                    _bSampling = value;
                    _eventAggregator.GetEvent<SampleServiceStateChangedEvent>().Publish(
                        SampleServiceStateType.Sample);
                }
            }
        }

        public double fs
        {
            get { return _fs; }
        }

        public int PackLen
        {
            get { return _packLen; }
        }


        public int ChannelNum
        {
            get { return _ChannelNum; }
        }
    }
}
