using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IVN.Infrastructure.Interfaces
{
    public interface IRecordService
    {
        int ChannelNum { get; }
        string[] ChannelNames { get; }
        string ServerIP { get; }
        bool bStarted { get; }
        bool bConnected { get; }
        double fs { get; }        
        int Nfft { get; }
        int FrameDN { get; }
        double fstart { get; }
        double fstop { get; }
        int RecordedFrames { get; }
        double[] Sens { get; }
        float[] MaxFrf { get; }
        bool bRecording { get; }
        bool StartRecord(string FileName);
        bool StopRecord();
    }
}
