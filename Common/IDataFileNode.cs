using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IVN.Infrastructure.Models
{
    public interface IDataFileNode
    {
        int ChannelNum { get; }
        double fs { get; }
        int Nfft { get; }
        double fstart { get; }
        int NFrame { get; }
        int FrameDN { get; }      
        string[] ChannelNames { get; }
        double[] Sens { get; }
        string filename { get;  }
        object Id { get; }
        DateTime uploadDate { get;  }

        float[] MaxdBA { get; }
    }
}
