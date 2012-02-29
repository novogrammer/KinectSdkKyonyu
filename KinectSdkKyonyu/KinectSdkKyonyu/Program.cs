using System;
using System.Runtime.InteropServices;

namespace KinectSdkKyonyu
{
#if WINDOWS || XBOX
    static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint MessageBox(IntPtr hWnd, String text, String caption, uint type);
        /// <summary>
        /// アプリケーションのメイン エントリー ポイントです。
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                using (Game1 game = new Game1())
                {
                    game.Run();
                }
            }
            catch (Exception e)
            {
                MessageBox(
                    new System.IntPtr(),
                    e.Message+" "+e.StackTrace,
                    "エラーにより終了します",
                    0);
            }
        }
    }
#endif
}

