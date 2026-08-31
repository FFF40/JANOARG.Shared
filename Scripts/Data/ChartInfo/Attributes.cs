
using System;

namespace JANOARG.Shared.Data.ChartInfo
{

    /// <summary>
    /// Indicates a float field can have a value of <c>float.NaN</c> to express a lack of value
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ToggleableFloatAttribute : Attribute
    {
        public ToggleableFloatAttribute() { }
    }
}