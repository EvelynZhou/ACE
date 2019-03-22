using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IVN.Infrastructure.Interfaces
{
    public interface ISampleService
    {
        double fs { get; }
        int PackLen { get; }
        int ChannelNum { get; }
        string ServerIP { get; }
        bool bStarted { get; }
        bool bConnected { get; }
        bool StartServer();
        bool StopServer();
    }
}
