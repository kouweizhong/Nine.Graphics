﻿namespace Nine.Graphics.Rendering
{
    using System;
    using System.Collections.Concurrent;
    using System.Drawing;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using OpenTK;
    using OpenTK.Graphics;
    using OpenTK.Graphics.OpenGL;

    public sealed class GLGraphicsHost : IGraphicsHost, IDisposable
    {
        class OpenGLSynchronizationContext : SynchronizationContext
        {
            struct Entry { public SendOrPostCallback Callback; public object State; };

            private readonly ConcurrentQueue<Entry> queue = new ConcurrentQueue<Entry>();

            public override void Post(SendOrPostCallback d, object state)
            {
                queue.Enqueue(new Entry { Callback = d, State = state });
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException();
            }

            public void DrainQueue()
            {
                Entry item;
                while (queue.TryDequeue(out item))
                {
                    item.Callback?.Invoke(item.State);
                }
            }
        }

        public readonly GameWindow Window;

        private static readonly OpenGLSynchronizationContext s_syncContext = new OpenGLSynchronizationContext();

        public static bool IsAvailable
        {
            get
            {
                try
                {
                    return GraphicsMode.Default != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public GLGraphicsHost(int width, int height, GraphicsMode mode = null, bool vSync = true)
            : this(new GameWindow(width, height, mode, "Nine.Graphics", GameWindowFlags.Default) { VSync = vSync ? VSyncMode.On : VSyncMode.Off, Visible = true })
        { }

        public GLGraphicsHost(GameWindow window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            Window = window;

            SynchronizationContext.SetSynchronizationContext(s_syncContext);

            GLDebug.CheckAccess();

            GL.ClearColor(Color.Transparent);
        }

        public bool DrawFrame(Action<int, int> draw, [CallerMemberName]string frameName = null)
        {
            GLDebug.CheckAccess();

            Window.ProcessEvents();
            s_syncContext.DrainQueue();

            if (Window.IsExiting)
            {
                return false;
            }

            GL.Viewport(0, 0, Window.Width, Window.Height);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            draw(Window.Width, Window.Height);

            Window.SwapBuffers();

            return true;
        }

        public void Dispose()
        {
            GLDebug.CheckAccess();

            Window.Dispose();
        }
    }
}
