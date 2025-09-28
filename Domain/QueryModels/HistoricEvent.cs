using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.QueryModels;

public class HistoricEvent
{
    public int Version { get; set; }
    public object EventData { get; set; }
    public DateTime Timestamp { get; set; }
}