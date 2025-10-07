using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shared.DTOs;

public class SqsCommandWrapper<T>
{
    [JsonPropertyName("commandType")]
    public string CommandType { get; set; }

    [JsonPropertyName("payload")]
    public T Payload { get; set; }

    public SqsCommandWrapper(string commandType, T payload)
    {
        CommandType = commandType;
        Payload = payload;
    }
}