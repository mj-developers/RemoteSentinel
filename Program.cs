using System;
using System.Threading;
using System.Windows.Forms;

namespace RemoteSentinel
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            using var mutex = new Mutex(initiallyOwned: true, "RemoteSentinel.SingleInstance", out bool isNew);
            if (!isNew)
            {
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new StatusTrayForm());
        }
    }
}
