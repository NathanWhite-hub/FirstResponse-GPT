using FirstResponseGPT.Interfaces;
using Rage;
using System;
using LSPD_First_Response.Mod.Callouts;

namespace FirstResponseGPT.Models
{
    public class CalloutContext : IContext
    {
        public bool IsRadio { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }

        public string CalloutName { get; set; }
        public Vector3 Location { get; set; }
        public string StreetName { get; set; }
        public string Description { get; set; }

        public CalloutContext(Callout callout)
        {
            IsRadio = true;
            Timestamp = DateTime.Now;
            Text = "New callout received";
            Location = callout.CalloutPosition;
            StreetName = World.GetStreetName(callout.CalloutPosition);
            Description = callout.CalloutMessage;
            CalloutName = callout.FriendlyName;
            Description = callout.CalloutAdvisory;
        }
    }
}