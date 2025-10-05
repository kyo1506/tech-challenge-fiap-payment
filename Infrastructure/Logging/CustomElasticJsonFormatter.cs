using Serilog.Events;
using Serilog.Formatting;
using Serilog.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Logging
{
    public class CustomElasticJsonFormatter : ITextFormatter
    {
        private readonly ExpressionTemplate _template = new ExpressionTemplate(
        "{ {@t: @timestamp, @m: Message, ServiceName: ApplicationName, 'X-Correlation-ID': CorrelationId, ..rest()} }\n");

        public void Format(LogEvent logEvent, TextWriter output)
        {
            _template.Format(logEvent, output);
        }
    }
}
