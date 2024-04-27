using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dear_ImGui_Sample
{
    internal class OpentkWindow : IDisposable
    {
        private readonly GCHandle _gcHandle;
        private readonly ImGuiViewportPtr _vp;
        private readonly GameWindow _window;
        public GameWindow Window => _window;

        public int _vertexArray;
        public int _vertexBuffer;
        public int _vertexBufferSize;
        public int _indexBuffer;
        public int _indexBufferSize;

        public OpentkWindow(ImGuiViewportPtr vp, IGLFWGraphicsContext context, nint ImGuiContext)
        {
            _gcHandle = GCHandle.Alloc(this);
            vp.PlatformUserData = (IntPtr)_gcHandle;
            _vp = vp;

            int width = (int)vp.Size.X;
            int height = (int)vp.Size.Y;
            NativeWindowSettings settings = new NativeWindowSettings()
            {
                StartVisible = false,
                StartFocused = true,
                WindowState = OpenTK.Windowing.Common.WindowState.Normal,
                Location = new Vector2i((int)vp.Pos.X, (int)vp.Pos.Y),
                ClientSize = new Vector2i(width, height),
                SharedContext = context,
                APIVersion = new Version(4, 6)
            };
            _window = new GameWindow(GameWindowSettings.Default, settings);
            ImGui.SetCurrentContext(ImGuiContext);
            CreateDeviceResources();

            //_window.WindowBorder = OpenTK.Windowing.Common.WindowBorder.Hidden;

            _window.Resize += (e) => { vp.PlatformRequestResize = true; };
            _window.Move += (e) => { vp.PlatformRequestMove = true; };
            _window.Closing += (e) => { vp.PlatformRequestClose = true; };

            _window.Resize += (e) => { Resize((int)e.Width, (int)e.Height); };
        }

        public OpentkWindow(ImGuiViewportPtr vp, GameWindow window)
        {
            _gcHandle = GCHandle.Alloc(this);
            _vp = vp;
            Console.WriteLine(_vp.ID);
            _window = window;
            vp.PlatformUserData = (IntPtr)_gcHandle;
            CreateDeviceResources();
        }
        private void Resize(int width, int height)
        {
            _window.MakeCurrent();
            GL.Viewport(0, 0, width, height);
        }

        public void Update()
        {
            _window.ProcessEvents(0);
        }

        public void CreateDeviceResources()
        {
            _vertexBufferSize = 10000;
            _indexBufferSize = 2000;

            _vertexArray = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArray);

            _vertexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            _indexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);


            int stride = Unsafe.SizeOf<ImDrawVert>();
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);
            GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        public void Dispose()
        {
            _window.Dispose();
            _gcHandle.Free();
            GL.DeleteVertexArray(_vertexArray);
            GL.DeleteBuffer(_vertexBuffer);
            GL.DeleteBuffer(_indexBuffer);
        }
    }
}