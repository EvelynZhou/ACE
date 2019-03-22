using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideComfortUC.Models
{
    interface IImpulseInputHandle
    {
        double GetMaxZ();
        double GetPeakFactor();
        double GetVDV();
    }
}
