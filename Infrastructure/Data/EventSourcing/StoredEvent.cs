using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.EventSourcing;

public class StoredEvent
{
    public Guid Id { get; set; }        
    public Guid StreamId { get; set; }      
    public int Version { get; set; }        
    public string EventType { get; set; }   
    public string Data { get; set; }        
    public DateTime Timestamp { get; set; }   
}
