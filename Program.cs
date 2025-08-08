using System;
using System.Windows.Forms;

namespace RemoteSentinel;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new StatusTrayForm());
    }
}
