using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstResponseGPT.Interfaces
{
    public interface IContext
    {
        bool IsRadio { get; set; }
        string Text { get; set; }
        DateTime Timestamp { get; set; }
    }
}
