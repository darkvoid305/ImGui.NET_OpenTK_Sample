using OpenTK.Mathematics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace OpenglEngine.Engine.UX.UX_lib
{
    internal class GlobalCursor 
    {

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int virtualKeyCode);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref Point pos);



        public static bool getInputState(int state) {
            
            return GetAsyncKeyState(state) != 0 ? true : false;
        }

        public static Vector2 getCursorPosition()
        {
            Point cursor = new Point();
            GetCursorPos(ref cursor);
            return new Vector2(cursor.X, cursor.Y);
        }
    }
}
