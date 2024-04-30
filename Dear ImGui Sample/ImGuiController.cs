using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;
using System.Runtime.InteropServices;
using OpenTK.Windowing.Common;

namespace Dear_ImGui_Sample
{
    public class ImGuiController : IDisposable
    {
        // FIXME: Maybe we can share these using context sharing? We would need to create a new VAO though.
        /*struct GLDrawData
        {
            private int _vertexArray;
            private int _vertexBuffer;
            private int _vertexBufferSize;
            private int _indexBuffer;
            private int _indexBufferSize;

            private int _fontTexture;

            private int _shader;
            private int _shaderFontTextureLocation;
            private int _shaderProjectionMatrixLocation;
        }*/

        class ViewportData
        {
            public NativeWindow Window;
            public bool Owned;

            public ViewportData(NativeWindow window, bool owned)
            {
                Window = window;
                Owned = owned;
            }
        }

        private bool _frameBegun;

        private int _fontTexture;

        public int _vertexBuffer;
        public int _vertexBufferSize;
        public int _indexBuffer;
        public int _indexBufferSize;

        private int _shader;
        private int _shaderFontTextureLocation;
        private int _shaderProjectionMatrixLocation;
        
        private int _windowWidth;
        private int _windowHeight;

        private System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;

        private static bool KHRDebugAvailable = false;

        private readonly NativeWindow _mainWindow;

        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void ImGuiPlatformIO_Set_Platform_GetWindowPos(ImGuiPlatformIO* platform_io, IntPtr funcPtr);
        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void ImGuiPlatformIO_Set_Platform_GetWindowSize(ImGuiPlatformIO* platform_io, IntPtr funcPtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void Platform_CreateWindow(ImGuiViewportPtr vp);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void Platform_DestroyWindow(ImGuiViewportPtr vp);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Platform_ShowWindow(ImGuiViewportPtr vp);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Platform_SetWindowPos(ImGuiViewportPtr vp, Vector2 pos);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void Platform_GetWindowPos(ImGuiViewportPtr vp, out Vector2 pos);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Platform_SetWindowSize(ImGuiViewportPtr vp, Vector2 size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void Platform_GetWindowSize(ImGuiViewportPtr vp, out Vector2 size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Platform_SetWindowFocus(ImGuiViewportPtr vp);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate byte Platform_GetWindowFocus(ImGuiViewportPtr vp);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate byte Platform_GetWindowMinimized(ImGuiViewportPtr vp);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Platform_SetWindowTitle(ImGuiViewportPtr vp, IntPtr title);

        private Platform_CreateWindow CreateWindowDelegate;
        private Platform_DestroyWindow DestroyWindowDelegate;
        private Platform_ShowWindow ShowWindowDelegate;
        private Platform_SetWindowPos SetWindowPosDelegate;
        private Platform_GetWindowPos GetWindowPosDelegate;
        private Platform_SetWindowSize SetWindowSizeDelegate;
        private Platform_GetWindowSize GetWindowSizeDelegate;
        private Platform_SetWindowFocus SetWindowFocusDelegate;
        private Platform_GetWindowFocus GetWindowFocusDelegate;
        private Platform_GetWindowMinimized GetWindowMinimizedDelegate;
        private Platform_SetWindowTitle SetWindowTitleDelegate;

        private nint _ImGuiContext;
        private readonly GameWindow _window;

        /// <summary>
        /// Constructs a new ImGuiController.
        /// </summary>
        public ImGuiController(int width, int height)
        {
            CreateDeviceResources();
            SetPerFrameImGuiData(1f / 60f);
        }

        public unsafe ImGuiController(int width, int height, GameWindow window)
        {
            _window = window;
            _windowWidth = width;
            _windowHeight = height;

            int major = GL.GetInteger(GetPName.MajorVersion);
            int minor = GL.GetInteger(GetPName.MinorVersion);

            KHRDebugAvailable = major == 4 && minor >= 3 || IsExtensionSupported("KHR_debug");

            IntPtr context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);
            ImGuiViewportPtr mainViewport = ImGui.GetMainViewport();
            ViewportData vpd = new ViewportData(window, false);
            unsafe
            {
                mainViewport.PlatformHandle = (IntPtr)window.WindowPtr;
            }
            mainViewport.PlatformUserData = (IntPtr)GCHandle.Alloc(vpd, GCHandleType.Normal);
            // FIXME: Should we retain a copy?
            _mainWindow = window;

            _ImGuiContext = context;

            // Setup multiple viewports
            CreateWindowDelegate = CreateWindow;
            DestroyWindowDelegate = DestroyWindow;
            ShowWindowDelegate = ShowWindow;
            SetWindowPosDelegate = SetWindowPos;
            GetWindowPosDelegate = GetWindowPos;
            SetWindowSizeDelegate = SetWindowSize;
            GetWindowSizeDelegate = GetWindowSize;
            SetWindowFocusDelegate = SetWindowFocus;
            GetWindowFocusDelegate = GetWindowFocus;
            GetWindowMinimizedDelegate = GetWindowMinimized;
            SetWindowTitleDelegate = SetWindowTitle;

            var platformIO = ImGui.GetPlatformIO();
            platformIO.Platform_CreateWindow = Marshal.GetFunctionPointerForDelegate(CreateWindowDelegate);
            platformIO.Platform_DestroyWindow = Marshal.GetFunctionPointerForDelegate(DestroyWindowDelegate);
            platformIO.Platform_ShowWindow = Marshal.GetFunctionPointerForDelegate(ShowWindowDelegate);
            platformIO.Platform_SetWindowPos = Marshal.GetFunctionPointerForDelegate(SetWindowPosDelegate);
            platformIO.Platform_SetWindowSize = Marshal.GetFunctionPointerForDelegate(SetWindowSizeDelegate);
            platformIO.Platform_SetWindowFocus = Marshal.GetFunctionPointerForDelegate(SetWindowFocusDelegate);
            platformIO.Platform_GetWindowFocus = Marshal.GetFunctionPointerForDelegate(GetWindowFocusDelegate);
            platformIO.Platform_GetWindowMinimized = Marshal.GetFunctionPointerForDelegate(GetWindowMinimizedDelegate);
            platformIO.Platform_SetWindowTitle = Marshal.GetFunctionPointerForDelegate(SetWindowTitleDelegate);

            ImGuiPlatformIO_Set_Platform_GetWindowPos(platformIO, Marshal.GetFunctionPointerForDelegate(GetWindowPosDelegate));
            ImGuiPlatformIO_Set_Platform_GetWindowSize(platformIO, Marshal.GetFunctionPointerForDelegate(GetWindowSizeDelegate));

            var io = ImGui.GetIO();
            unsafe
            {
                io.NativePtr->BackendPlatformName = (byte*)Marshal.StringToCoTaskMemUTF8("OpenTK Imgui Backend");
                io.NativePtr->BackendRendererName = (byte*)Marshal.StringToCoTaskMemUTF8("OpenTK Imgui OpenGL Renderer");
            }

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

            io.BackendFlags |= ImGuiBackendFlags.PlatformHasViewports;
            io.BackendFlags |= ImGuiBackendFlags.RendererHasViewports;

            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

            io.Fonts.AddFontDefault();

            CreateDeviceResources();
            SetPerFrameImGuiData(1f / 60f);

            UpdateMonitors();

            //ImGui.NewFrame();
            //_frameBegun = true;
        }

        public unsafe void UpdateMonitors()
        {
            ImGuiPlatformIOPtr platformIO = ImGui.GetPlatformIO();
            var monitors = Monitors.GetMonitors();
            Marshal.FreeHGlobal(platformIO.NativePtr->Monitors.Data);
            IntPtr data = Marshal.AllocHGlobal(Unsafe.SizeOf<ImGuiPlatformMonitor>() * monitors.Count);
            platformIO.NativePtr->Monitors = new ImVector(monitors.Count, monitors.Count, data);
            for (int i = 0; i < monitors.Count; i++)
            {
                ImGuiPlatformMonitorPtr monitor = platformIO.Monitors[i];
                Box2i r = monitors[i].ClientArea;

                var pos = r.Min.ToVector2();
                var size = r.Size.ToVector2();
                monitor.DpiScale = 1f;
                monitor.MainPos = Unsafe.As<Vector2, System.Numerics.Vector2>(ref pos);
                monitor.MainSize = Unsafe.As<Vector2, System.Numerics.Vector2>(ref size);
                monitor.WorkPos = monitor.MainPos;
                monitor.WorkSize = monitor.MainSize;
            }
        }

        private unsafe void CreateWindow(ImGuiViewportPtr vp)
        {
            Console.WriteLine("Create Window");

            int width = (int)vp.Size.X;
            int height = (int)vp.Size.Y;
            NativeWindowSettings settings = new NativeWindowSettings()
            {
                StartVisible = false,
                StartFocused = false,
                // FIXME: FocusOnShow
                WindowState = WindowState.Normal,
                WindowBorder = vp.Flags.HasFlag(ImGuiViewportFlags.NoDecoration) ? WindowBorder.Hidden : WindowBorder.Resizable,
                // FIXME: Topmost
                Location = new Vector2i((int)vp.Pos.X, (int)vp.Pos.Y),
                ClientSize = new Vector2i(width, height),
                SharedContext = _mainWindow.Context,
            };
            NativeWindow window = new NativeWindow(settings);
            // FIXME: Create window specific resources.
            //CreateDeviceResources();

            ViewportData vpd = new ViewportData(window, true);
            unsafe
            {
                vp.PlatformHandle = (IntPtr)window.WindowPtr;
            }
            vp.PlatformUserData = (IntPtr)GCHandle.Alloc(vpd, GCHandleType.Normal);

            // Hook up events...
            //_window.Resize += (e) => { vp.PlatformRequestResize = true; };
            //_window.Move += (e) => { vp.PlatformRequestMove = true; };
            //_window.Closing += (e) => { vp.PlatformRequestClose = true; };
            //_window.Resize += (e) => { Resize((int)e.Width, (int)e.Height); };

            
            //OpentkWindow window = new OpentkWindow(vp, windowContext, _ImGuiContext);
        }

        private static unsafe void DestroyWindow(ImGuiViewportPtr vp)
        {
            Console.WriteLine("DestroyWindow");
            if (vp.PlatformUserData != IntPtr.Zero)
            {
                ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
                vpd.Window.MakeCurrent();
                vpd.Window.Dispose();
                vp.PlatformUserData = IntPtr.Zero;
            }
        }

        private static unsafe void ShowWindow(ImGuiViewportPtr vp)
        {
            ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            vpd.Window.IsVisible = true;
        }

        private static unsafe void SetWindowPos(ImGuiViewportPtr vp, Vector2 pos)
        {
            Console.WriteLine("SetWindowPos: " + pos);
            ViewportData window = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            window.Window.Location = new Vector2i((int)Math.Floor(pos.X), (int)Math.Floor(pos.Y));
        }

        public static unsafe void GetWindowPos(ImGuiViewportPtr vp, out Vector2 pos)
        {
            ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            pos = vpd.Window.ClientLocation;
        }

        public static unsafe void SetWindowSize(ImGuiViewportPtr vp, Vector2 size)
        {
            Console.WriteLine("SetWindowSize: " + size);
            ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            vpd.Window.MakeCurrent();
            vpd.Window.Size = new Vector2i((int)size.X, (int)size.Y);
        }
        
        public static unsafe void GetWindowSize(ImGuiViewportPtr vp, out Vector2 size)
        {
            Console.WriteLine("GetWindowSize");
            ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            size = vpd.Window.ClientSize;
        }
        
        public static unsafe void SetWindowFocus(ImGuiViewportPtr vp)
        {
            ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            vpd.Window.Focus();
        }
        
        public static unsafe byte GetWindowFocus(ImGuiViewportPtr vp)
        {
            ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            return (byte)(vpd.Window.IsFocused ? 1 : 0);
        }
        
        public static unsafe byte GetWindowMinimized(ImGuiViewportPtr vp)
        {
            ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            bool minimized = vpd.Window.WindowState == WindowState.Minimized;
            return minimized ? (byte)1 : (byte)0;
        }
        
        public static unsafe void SetWindowTitle(ImGuiViewportPtr vp, IntPtr title)
        {
            ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
            vpd.Window.Title = Marshal.PtrToStringUTF8(title);
        }

        public void WindowResized(int width, int height)
        {
            _windowWidth = width;
            _windowHeight = height;
        }

        public void DestroyDeviceObjects()
        {
            Dispose();
        }

        public void CreateDeviceResources()
        {
            _vertexBufferSize = 10000;
            _indexBufferSize = 2000;

            int prevArrayBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);

            _vertexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            LabelObject(ObjectLabelIdentifier.Buffer, _vertexBuffer, "VBO: ImGui");
            GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            _indexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _indexBuffer);
            LabelObject(ObjectLabelIdentifier.Buffer, _indexBuffer, "EBO: ImGui");
            GL.BufferData(BufferTarget.ArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            GL.BindBuffer(BufferTarget.ArrayBuffer, prevArrayBuffer);

            RecreateFontDeviceTexture();

            string VertexSource = @"#version 330 core

uniform mat4 projection_matrix;

layout(location = 0) in vec2 in_position;
layout(location = 1) in vec2 in_texCoord;
layout(location = 2) in vec4 in_color;

out vec4 color;
out vec2 texCoord;

void main()
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
    color = in_color;
    texCoord = in_texCoord;
}";
            string FragmentSource = @"#version 330 core

uniform sampler2D in_fontTexture;

in vec4 color;
in vec2 texCoord;

out vec4 outputColor;

void main()
{
    outputColor = color * texture(in_fontTexture, texCoord);
}";

            _shader = CreateProgram("ImGui", VertexSource, FragmentSource);
            _shaderProjectionMatrixLocation = GL.GetUniformLocation(_shader, "projection_matrix");
            _shaderFontTextureLocation = GL.GetUniformLocation(_shader, "in_fontTexture");

            _vertexBufferSize = 10000;
            _indexBufferSize = 2000;

            _vertexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            _indexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            CheckGLError("End of ImGui setup");
        }

        /// <summary>
        /// Recreates the device texture used to render text.
        /// </summary>
        public void RecreateFontDeviceTexture()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);

            int mips = (int)Math.Floor(Math.Log(Math.Max(width, height), 2));

            _fontTexture = GL.GenTexture();
            GL.ActiveTexture(0);
            GL.BindTexture(TextureTarget.Texture2D, _fontTexture);
            GL.TexStorage2D(TextureTarget2d.Texture2D, mips, SizedInternalFormat.Rgba8, width, height);
            LabelObject(ObjectLabelIdentifier.Texture, _fontTexture, "ImGui Text Atlas");

            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, mips - 1);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

            io.Fonts.SetTexID((IntPtr)_fontTexture);

            io.Fonts.ClearTexData();
        }

        /// <summary>
        /// Renders the ImGui draw list data.
        /// </summary>
        public void Render()
        {
            if (_frameBegun)
            {
                _frameBegun = false;
                ImGui.Render();
                // FIXME: Don't allocate every frame.
                RenderImDrawData(ImGui.GetDrawData(), new ViewportData(_mainWindow, false));

                //render windows outside of main (this code heavily based of of the Veldrid example implementation)
                if ((ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
                {
                    ImGui.UpdatePlatformWindows();
                    ImGuiPlatformIOPtr platformIO = ImGui.GetPlatformIO();
                    for (int i = 1; i < platformIO.Viewports.Size; i++)
                    {
                        ImGuiViewportPtr vp = platformIO.Viewports[i];
                        ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
                        vpd.Window.MakeCurrent();
                        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
                        RenderImDrawData(vp.DrawData, vpd);
                    }
                }
            }
            SwapExtraWindows();
            _mainWindow.MakeCurrent();
        }
        
        // FIXME: Remove this function.
        public void SwapExtraWindows()
        {
            ImGuiPlatformIOPtr platformIO = ImGui.GetPlatformIO();
            for (int i = 1; i < platformIO.Viewports.Size; i++)
            {
                ImGuiViewportPtr vp = platformIO.Viewports[i];
                ViewportData vpd = (ViewportData)GCHandle.FromIntPtr(vp.PlatformUserData).Target;
                vpd.Window.Context.SwapBuffers();
            }
        }

        public void NewFrame(float deltaTime)
        {
            ImGuiIOPtr io = ImGui.GetIO();

            Vector2 size = _mainWindow.ClientSize;
            Vector2 framebufferSize = _mainWindow.FramebufferSize;
            io.DisplaySize = new System.Numerics.Vector2(size.X, size.Y);
            if (size.X > 0 && size.Y > 0)
            {
                io.DisplayFramebufferScale = new System.Numerics.Vector2(framebufferSize.X / size.X, framebufferSize.Y / size.Y);
            }

            io.DeltaTime = deltaTime;

            // Update mouse data
            // Update mouse cursor
            // Update gamepads
        }

        /// <summary>
        /// Updates ImGui input and IO configuration state.
        /// </summary>
        public void Update(GameWindow wnd, float deltaSeconds)
        {
            if (_frameBegun)
            {
                ImGui.Render();
                ImGui.UpdatePlatformWindows();
            }

            SetPerFrameImGuiData(deltaSeconds);
            UpdateImGuiInput(wnd);
            UpdateMonitors();

            _frameBegun = true;
            ImGui.NewFrame();
        }

        /// <summary>
        /// Sets per-frame data based on the associated window.
        /// This is called by Update(float).
        /// </summary>
        private void SetPerFrameImGuiData(float deltaSeconds)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.DisplaySize = new System.Numerics.Vector2(
                _windowWidth / _scaleFactor.X,
                _windowHeight / _scaleFactor.Y);
            io.DisplayFramebufferScale = _scaleFactor;
            io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.

            //Based of of the Veldrid impolementation
            ImGui.GetPlatformIO().Viewports[0].Pos = new System.Numerics.Vector2(_window.Location.X, _window.Location.Y);
            ImGui.GetPlatformIO().Viewports[0].Size = new System.Numerics.Vector2(_window.Size.X, _window.Size.Y);
        }

        readonly List<char> PressedChars = new List<char>();

        private void UpdateImGuiInput(GameWindow wnd)
        {
            ImGuiIOPtr io = ImGui.GetIO();

            MouseState MouseState = wnd.MouseState;
            KeyboardState KeyboardState = wnd.KeyboardState;

            io.MouseDown[0] = MouseState[MouseButton.Left];
            io.MouseDown[1] = MouseState[MouseButton.Right];
            io.MouseDown[2] = MouseState[MouseButton.Middle];
            io.MouseDown[3] = MouseState[MouseButton.Button4];
            io.MouseDown[4] = MouseState[MouseButton.Button5];

            // FIXME: Get mouse input regardless if it's in a window or not.
            // Alternatively get mouse input from all windows.
            Vector2 cursorPos = wnd.MousePosition + wnd.ClientLocation;
            io.MousePos = new System.Numerics.Vector2(cursorPos.X, cursorPos.Y);

            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if (key == Keys.Unknown)
                {
                    continue;
                }
                io.AddKeyEvent(TranslateKey(key), KeyboardState.IsKeyDown(key));
            }

            foreach (var c in PressedChars)
            {
                io.AddInputCharacter(c);
            }
            PressedChars.Clear();

            io.KeyCtrl = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);
            io.KeyAlt = KeyboardState.IsKeyDown(Keys.LeftAlt) || KeyboardState.IsKeyDown(Keys.RightAlt);
            io.KeyShift = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
            io.KeySuper = KeyboardState.IsKeyDown(Keys.LeftSuper) || KeyboardState.IsKeyDown(Keys.RightSuper);
        }

        internal void PressChar(char keyChar)
        {
            PressedChars.Add(keyChar);
        }

        internal void MouseScroll(Vector2 offset)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            
            io.MouseWheel = offset.Y;
            io.MouseWheelH = offset.X;
        }

        private void SetupRenderState(ImDrawDataPtr draw_data, int fb_width, int fb_height, int vao)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.ScissorTest);
            // FIXME: Check opengl version for these
            GL.Disable(EnableCap.PrimitiveRestart);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.Viewport(0, 0, fb_width, fb_height);

            // FIXME: Support glClipControl(GL_UPPER_LEFT)

            // Setup orthographic projection matrix into our constant buffer
            ImGuiIOPtr io = ImGui.GetIO();
            System.Numerics.Vector2 pos = draw_data.DisplayPos;
            Matrix4 mvp = Matrix4.CreateOrthographicOffCenter(
              pos.X,
              pos.X + draw_data.DisplaySize.X,
              pos.Y + draw_data.DisplaySize.Y,
              pos.Y,
              -1.0f,
              1.0f
            );

            GL.UseProgram(_shader);
            GL.UniformMatrix4(_shaderProjectionMatrixLocation, false, ref mvp);
            GL.Uniform1(_shaderFontTextureLocation, 0);
            CheckGLError("Projection");

            GL.BindVertexArray(vao);

            // Bind the vertex buffer so that we can resize it.
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            // Bind the element buffer so that we can resize it.
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);

            int stride = Unsafe.SizeOf<ImDrawVert>();
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);
            GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
        }

        private void RenderImDrawData(ImDrawDataPtr draw_data, ViewportData vpd)
        {
            if (draw_data.CmdListsCount == 0)
            {
                return;
            }

            int prevVAO = GL.GetInteger(GetPName.VertexArrayBinding);
            
            int vertexArray = GL.GenVertexArray();
            
            for (int i = 0; i < draw_data.CmdListsCount; i++)
            {
                ImDrawListPtr cmd_list = draw_data.CmdLists[i];

                int vertexSize = cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
                if (vertexSize > _vertexBufferSize)
                {
                    int newSize = (int)Math.Max(_vertexBufferSize * 1.5f, vertexSize);
                    
                    GL.BufferData(BufferTarget.ArrayBuffer, newSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                    _vertexBufferSize = newSize;

                    Console.WriteLine($"Resized dear imgui vertex buffer to new size {_vertexBufferSize}");
                }

                int indexSize = cmd_list.IdxBuffer.Size * sizeof(ushort);
                if (indexSize > _indexBufferSize)
                {
                    int newSize = (int)Math.Max(_indexBufferSize * 1.5f, indexSize);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, newSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                    _indexBufferSize = newSize;

                    Console.WriteLine($"Resized dear imgui index buffer to new size {_indexBufferSize}");
                }
            }

            GL.BindVertexArray(vertexArray);
            CheckGLError("VAO");

            Vector2i fb_size = vpd.Window.FramebufferSize;
            SetupRenderState(draw_data, fb_size.X, fb_size.Y, vertexArray);

            // FIXME: What is this for?
            ImGuiIOPtr io = ImGui.GetIO();
            draw_data.ScaleClipRects(io.DisplayFramebufferScale);

            // Render command lists
            for (int n = 0; n < draw_data.CmdListsCount; n++)
            {
                ImDrawListPtr cmd_list = draw_data.CmdLists[n];

                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>(), cmd_list.VtxBuffer.Data);
                CheckGLError($"Data Vert {n}");

                GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, cmd_list.IdxBuffer.Size * sizeof(ushort), cmd_list.IdxBuffer.Data);
                CheckGLError($"Data Idx {n}");

                for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
                {
                    ImDrawCmdPtr pcmd = cmd_list.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);
                        CheckGLError("Texture");

                        // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                        var clip = pcmd.ClipRect;
                        GL.Scissor(
                           0,
                           0,
                           (int)(draw_data.DisplaySize.X),
                           (int)(draw_data.DisplaySize.Y)
                       );
                        CheckGLError("Scissor");

                        if ((io.BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0)
                        {
                            int vertexOffset;
                            unchecked
                            {
                                vertexOffset = (int)pcmd.VtxOffset;
                            }
                            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (IntPtr)(pcmd.IdxOffset * sizeof(ushort)), vertexOffset);
                        }
                        else
                        {
                            GL.DrawElements(BeginMode.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (int)pcmd.IdxOffset * sizeof(ushort));
                        }
                        CheckGLError("Draw");
                    }
                }
            }

            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.ScissorTest);

            GL.BindVertexArray(prevVAO);
            GL.DeleteVertexArray(vertexArray);
        }

        /// <summary>
        /// Frees all graphics resources used by the renderer.
        /// </summary>
        public void Dispose()
        {
            GL.DeleteTexture(_fontTexture);
            GL.DeleteProgram(_shader);
            GL.DeleteBuffer(_vertexBuffer);
            GL.DeleteBuffer(_indexBuffer);

            GC.SuppressFinalize(this);
        }

        public static void LabelObject(ObjectLabelIdentifier objLabelIdent, int glObject, string name)
        {
            if (KHRDebugAvailable)
                GL.ObjectLabel(objLabelIdent, glObject, name.Length, name);
        }

        static bool IsExtensionSupported(string name)
        {
            int n = GL.GetInteger(GetPName.NumExtensions);
            for (int i = 0; i < n; i++)
            {
                string extension = GL.GetString(StringNameIndexed.Extensions, i);
                if (extension == name) return true;
            }

            return false;
        }

        public static int CreateProgram(string name, string vertexSource, string fragmentSoruce)
        {
            int program = GL.CreateProgram();
            LabelObject(ObjectLabelIdentifier.Program, program, $"Program: {name}");

            int vertex = CompileShader(name, ShaderType.VertexShader, vertexSource);
            int fragment = CompileShader(name, ShaderType.FragmentShader, fragmentSoruce);

            GL.AttachShader(program, vertex);
            GL.AttachShader(program, fragment);

            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string info = GL.GetProgramInfoLog(program);
                Debug.WriteLine($"GL.LinkProgram had info log [{name}]:\n{info}");
            }

            GL.DetachShader(program, vertex);
            GL.DetachShader(program, fragment);

            GL.DeleteShader(vertex);
            GL.DeleteShader(fragment);

            return program;
        }

        private static int CompileShader(string name, ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            LabelObject(ObjectLabelIdentifier.Shader, shader, $"Shader: {name}");

            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string info = GL.GetShaderInfoLog(shader);
                Debug.WriteLine($"GL.CompileShader for shader '{name}' [{type}] had info log:\n{info}");
            }

            return shader;
        }

        public static void CheckGLError(string title)
        {
            ErrorCode error;
            int i = 1;
            while ((error = GL.GetError()) != ErrorCode.NoError)
            {
                Debug.Print($"{title} ({i++}): {error}");
            }
        }

        public static ImGuiKey TranslateKey(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
                return key - Keys.D0 + ImGuiKey._0;

            if (key >= Keys.A && key <= Keys.Z)
                return key - Keys.A + ImGuiKey.A;

            if (key >= Keys.KeyPad0 && key <= Keys.KeyPad9)
                return key - Keys.KeyPad0 + ImGuiKey.Keypad0;

           // if (key >= Keys.F1 && key <= Keys.F24)
           //     return key - Keys.F1 + ImGuiKey.F24;

            switch (key)
            {
                case Keys.Tab: return ImGuiKey.Tab;
                case Keys.Left: return ImGuiKey.LeftArrow;
                case Keys.Right: return ImGuiKey.RightArrow;
                case Keys.Up: return ImGuiKey.UpArrow;
                case Keys.Down: return ImGuiKey.DownArrow;
                case Keys.PageUp: return ImGuiKey.PageUp;
                case Keys.PageDown: return ImGuiKey.PageDown;
                case Keys.Home: return ImGuiKey.Home;
                case Keys.End: return ImGuiKey.End;
                case Keys.Insert: return ImGuiKey.Insert;
                case Keys.Delete: return ImGuiKey.Delete;
                case Keys.Backspace: return ImGuiKey.Backspace;
                case Keys.Space: return ImGuiKey.Space;
                case Keys.Enter: return ImGuiKey.Enter;
                case Keys.Escape: return ImGuiKey.Escape;
                case Keys.Apostrophe: return ImGuiKey.Apostrophe;
                case Keys.Comma: return ImGuiKey.Comma;
                case Keys.Minus: return ImGuiKey.Minus;
                case Keys.Period: return ImGuiKey.Period;
                case Keys.Slash: return ImGuiKey.Slash;
                case Keys.Semicolon: return ImGuiKey.Semicolon;
                case Keys.Equal: return ImGuiKey.Equal;
                case Keys.LeftBracket: return ImGuiKey.LeftBracket;
                case Keys.Backslash: return ImGuiKey.Backslash;
                case Keys.RightBracket: return ImGuiKey.RightBracket;
                case Keys.GraveAccent: return ImGuiKey.GraveAccent;
                case Keys.CapsLock: return ImGuiKey.CapsLock;
                case Keys.ScrollLock: return ImGuiKey.ScrollLock;
                case Keys.NumLock: return ImGuiKey.NumLock;
                case Keys.PrintScreen: return ImGuiKey.PrintScreen;
                case Keys.Pause: return ImGuiKey.Pause;
                case Keys.KeyPadDecimal: return ImGuiKey.KeypadDecimal;
                case Keys.KeyPadDivide: return ImGuiKey.KeypadDivide;
                case Keys.KeyPadMultiply: return ImGuiKey.KeypadMultiply;
                case Keys.KeyPadSubtract: return ImGuiKey.KeypadSubtract;
                case Keys.KeyPadAdd: return ImGuiKey.KeypadAdd;
                case Keys.KeyPadEnter: return ImGuiKey.KeypadEnter;
                case Keys.KeyPadEqual: return ImGuiKey.KeypadEqual;
                case Keys.LeftShift: return ImGuiKey.LeftShift;
                case Keys.LeftControl: return ImGuiKey.LeftCtrl;
                case Keys.LeftAlt: return ImGuiKey.LeftAlt;
                case Keys.LeftSuper: return ImGuiKey.LeftSuper;
                case Keys.RightShift: return ImGuiKey.RightShift;
                case Keys.RightControl: return ImGuiKey.RightCtrl;
                case Keys.RightAlt: return ImGuiKey.RightAlt;
                case Keys.RightSuper: return ImGuiKey.RightSuper;
                case Keys.Menu: return ImGuiKey.Menu;
                default: return ImGuiKey.None;
            }
        }
    }
}