using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.MessageBus;

public interface ICommandPublisher
{
    Task SendCommandAsync<T>(string commandType, T payload, string queueConfigKey);
}