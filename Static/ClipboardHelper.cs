using enviro.Models;

namespace enviro.Static;

/// <summary>
/// Static helper class for clipboard operations with environmental variable models.
/// </summary>
internal static class ClipboardHelper
{
    /// <summary>
    /// Copies a property value from an environmental variable model to the clipboard.
    /// </summary>
    /// <param name="model">The model to extract the property from. If null, no action is taken.</param>
    /// <param name="selector">A function that selects which property to copy from the model.</param>
    public static void CopyModelProperty(EnvModel? model, Func<EnvModel, string> selector)
    {
        if (model is not null)
        {
            var value = selector(model);
            Clipboard.SetText(value);
        }
    }
}
