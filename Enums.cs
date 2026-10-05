using System.ComponentModel;
using System.Reflection;

namespace WorldWeatherNow;

public enum MapStyle
{
  [Description("OpenStreetMap")] OpenStreetMap,
  [Description("World Imagery (Satellite)")] WorldImagery,
  [Description("World Physical")] WorldPhysical,
  [Description("National Geographic")] NationalGeographic,
  [Description("Dark Gray Canvas")] DarkGrayCanvas,
  [Description("Light Gray Canvas")] LightGrayCanvas
}

public static class EnumExtensions
{
  public static string GetDescription(this Enum value)
  {
    FieldInfo? field = value.GetType().GetField(value.ToString());
    if (field == null) return value.ToString();

    DescriptionAttribute? attribute = field.GetCustomAttribute<DescriptionAttribute>();
    return attribute != null ? attribute.Description : value.ToString();
  }
}
