using System;
using System.Collections.Generic;

namespace Taslow.Shared.Model
{
    public static class ProjectTypes
    {
        public const string Administrative = "Administrative";
        public const string Delivery = "Delivery";
        public const string Support = "Support";
        public const string Capture = "Capture";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Administrative,
            Delivery,
            Support,
            Capture
        };
    }
}
