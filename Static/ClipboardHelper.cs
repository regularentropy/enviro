using enviro.Models;

namespace enviro.Static;

internal static class ClipboardHelper
{
    public static void CopyModelProperty(EnvModel? model, Func<EnvModel, string> selector)
    {
        if (model is not null)
        {
            var value = selector(model);
            Clipboard.SetText(value);
        }
    }
}
