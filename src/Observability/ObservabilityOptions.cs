using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Observability;

public class ObservabilityOptions
{
    public bool HttpClient { get; set; } = false;
    public bool Postgres { get; set; } = false;
    public string JaegerUrl { get; set; } = "";
}
