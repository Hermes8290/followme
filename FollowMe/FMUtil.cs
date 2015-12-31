using Lib_K_Relay.Networking.Packets.DataObjects;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FollowMe
{
    class FMUtil
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, Int32 lParam);

        public static string TargetProcess = "flashbox";
        
        public static bool IsFollowMeTargetActive()
        {

            Process[] processes = Process.GetProcessesByName(TargetProcess);

            foreach (Process p in processes)
            {

                if (p.MainWindowHandle == GetForegroundWindow())
                    return true;

            }

            return false;

        }

        public static Status GenerateStatus(Location location, int stats, int objectid)
        {

            var ret = new Status();

            ret.Data = new StatData[stats];
            for (var i = 0; i < stats; i++)
                ret.Data[i] = new StatData();

            ret.Position = new Location();
            ret.Position.X = location.X;
            ret.Position.Y = location.Y;

            ret.ObjectId = objectid;

            return ret;

        }

        public static StatData SetStatData(StatData data, StatsType id, int ival, string sval)
        {

            data.Id = id;
            data.IntValue = ival;
            data.StringValue = sval;

            return data;

        }

        public static StatData CreateStatData(StatsType id, int ival, string sval)
        {
            return SetStatData(new StatData(), id, ival, sval);
        }

        public static void SendKeyToAll(Keys key, bool down)
        {

            int k = (int)key;

            Process[] processes = Process.GetProcessesByName(TargetProcess);


            for (int i = 0; i < processes.Length; i++)
            {
                if (processes[i].MainWindowHandle == GetForegroundWindow())
                    continue;

                SendMessage(processes[i].MainWindowHandle, down ? 0x0100 : 0x0101, k, 0);

            }

        }
    }
}
