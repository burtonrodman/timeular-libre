using System.Diagnostics;

namespace Timeular.Core;

public class DefaultActionLauncher : IActionLauncher
{
    public void Launch(string webInterfaceUrl, string actionName)
    {
        if (string.IsNullOrWhiteSpace(webInterfaceUrl))
            return;

        try
        {
            var url = webInterfaceUrl;
            if (!url.Contains("?"))
                url += "?";
            else if (!url.EndsWith("&") && !url.EndsWith("?"))
                url += "&";

            url += "action=" + Uri.EscapeDataString(actionName);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ignore failures
        }
    }
}
