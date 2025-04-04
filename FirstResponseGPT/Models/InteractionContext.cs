using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstResponseGPT.Interfaces;

namespace FirstResponseGPT.Models
{
    public class InteractionContext : IContext
    {
        public bool IsRadio { get; set; }
        public string Text { get; set; }
        public float Confidence { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
