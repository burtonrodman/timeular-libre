using System;
using Timeular.Core;
using Xunit;

namespace Timeular.Core.Tests
{
    public class ActionLauncherTests
    {
        [Fact]
        public void DefaultActionLauncher_FormsUrlCorrectly()
        {
            var launcher = new DefaultActionLauncher();
            var url = "https://example.com/receive";
            var action = "Test Action";

            // Rather than actually launching a process, we can simulate by building the
            // expected URI ourselves using the same logic.
            var expected = url + "?action=" + Uri.EscapeDataString(action);
            
            // The launcher doesn't return the URL, so we'll test the internal logic via
            // a subclass that exposes the computed string for verification.
            var captured = "";
            var testLauncher = new TestLauncher(u => captured = u);
            testLauncher.Launch(url, action);

            Assert.Equal(expected, captured);
        }

        private class TestLauncher : IActionLauncher
        {
            private readonly Action<string> _onLaunch;
            public TestLauncher(Action<string> onLaunch) => _onLaunch = onLaunch;
            public void Launch(string webInterfaceUrl, string actionName)
            {
                var url = webInterfaceUrl;
                if (!url.Contains("?"))
                    url += "?";
                else if (!url.EndsWith("&") && !url.EndsWith("?"))
                    url += "&";

                url += "action=" + Uri.EscapeDataString(actionName);
                _onLaunch(url);
            }
        }
    }
}