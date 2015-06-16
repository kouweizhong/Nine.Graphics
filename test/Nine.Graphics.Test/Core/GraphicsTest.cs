﻿namespace Nine.Graphics
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Nine.Imaging;
    using Nine.Injection;
    using Nine.Graphics.Content;
    using Xunit;

    /// <summary>
    /// Enables a couple of key graphics testing scenarios:
    /// 
    /// CORRECTNESS:
    ///   - The image should be idential to an expected image.
    ///     The expected image is usually pre-rendered and verified manually.
    ///   - The same input state should always produce the same result between 2 frames.
    ///   - The same input state should produce nearly idential images between different renderers.
    ///   - The renderer should only read the input state and never alter the state.
    /// 
    /// PERFORMANCE:
    ///   - The input state is rendered multiple times to abtain time info.
    ///   - The time info is compared against a baseline to warn if something become drastically slower.
    ///   - The time info is compared against multiple renderers for comparison.
    /// 
    /// DEBUGGING:
    ///   - Be able to render a frame to an image.
    ///   - Be able to render a frame to the output window.
    /// 
    /// </summary>
    [Trait("ci", "false")]
    [Collection("Graphics")]
    public class GraphicsTest
    {
        public static bool Hide;
        public static bool Verify;
        public static int? Repeat;
        public static int Width = 1024;
        public static int Height = 768;
        public static int Delay = 1;
        public static string Output = "TestResults";

        private int frameCounter = 0;

        private static bool initialized;
        public static readonly IContainer Container = new Container();

        public GraphicsTest()
        {
            if (!initialized)
            {
                initialized = true;
                Container
                   .Map<IContentLocator, ContentLocator>()
                   .Map<IContentLoader<TextureContent>, TextureLoader>()
                   .Map(new OpenGL.GraphicsHost(Width, Height, null, Hide, false));
            }
        }

        public void LoadTextures(string[] textures)
        {
            var contentLoader = Container.Get<IContentLoader<TextureContent>>();
            var load = new Func<string, Task>(async name =>
            {
                var color = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"Loading { name }");
                    Console.ForegroundColor = color;
                    if (await contentLoader.Load(name) == null) throw new FileNotFoundException();
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"Error loading: { name }");
                    Console.ForegroundColor = color;
                }
            });

            Task.WaitAll(textures.Select(load).ToArray());
        }

        public void Frame(Type hostType, Action draw, [CallerMemberName]string name = null)
        {
            var i = 0;
            var frameName = $"{ GetType().Name }/{ name }" + (frameCounter > 0 ? $"-{ frameCounter }" : "");
            var outputPath = Path.GetDirectoryName($"{ Output }/{ frameName }");

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var host = Container.Get(hostType) as IGraphicsHost;
            var previousFrame = (TextureContent)null;
            var watch = Stopwatch.StartNew();

            while (true)
            {
                if (!host.BeginFrame())
                {
                    return;
                }

                draw();

                if (Verify)
                {
                    var currentFrame = host.GetTexture();
                    if (i == 0)
                    {
                        SaveAndVerifyFrame(host.GetTexture(), frameName);
                    }
                    else
                    {
                        Assert.Equal(previousFrame.Pixels, currentFrame.Pixels);
                    }
                    previousFrame = currentFrame;
                }

                host.EndFrame();

                i++;

                if (Repeat.HasValue && i >= Repeat)
                {
                    break;
                }

                if (watch.Elapsed.TotalSeconds > Delay)
                {
                    break;
                }
            }
            watch.Stop();

            SaveAndVerifyPerf(i, watch, frameName);

            frameCounter++;
        }

        private void SaveAndVerifyFrame(TextureContent frame, string name)
        {
            var expectedFile = $"{ Output }/{ name }.png";

            if (File.Exists(expectedFile))
            {
                using (var expectedStream = File.OpenRead(expectedFile))
                {
                    var expectedImage = new Image(expectedStream);

                    try
                    {
                        Assert.Equal(expectedImage.PixelWidth, frame.Width);
                        Assert.Equal(expectedImage.PixelHeight, frame.Height);
                        Assert.Equal(expectedImage.Pixels, frame.Pixels);
                        return;
                    }
                    catch
                    {
                        SaveFrame(frame, $"{ Output }/{ name }.actual.png");
                        throw;
                    }
                }
            }

            SaveFrame(frame, expectedFile);
        }

        private void SaveFrame(TextureContent texture, string filename)
        {
            var image = new Image();
            image.SetPixels(texture.Width, texture.Height, texture.Pixels);

            using (var stream = File.OpenWrite(filename))
            {
                image.SaveAsPng(stream);
            }
        }

        private void SaveAndVerifyPerf(int count, Stopwatch watch, string frameName)
        {
            var previousRunFile = $"{ Output }/{frameName}.perf.txt";
            var previousTime = File.Exists(previousRunFile) ? double.Parse(File.ReadAllText(previousRunFile)) : 99999999;
            var previousFps = 1000 * count / previousTime;

            var time = watch.Elapsed.TotalMilliseconds;
            var fps = 1000 * count / time;
            var isRunningSlower = time > previousTime * 1.25;

            var color = Console.ForegroundColor;
            var highlight = isRunningSlower ? ConsoleColor.Red : ConsoleColor.DarkGreen;

            Console.Write(frameName);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" finished { count } frames in ");
            Console.ForegroundColor = highlight;
            Console.Write(time.ToString("N4"));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            if (isRunningSlower) Console.Write($"({ previousTime.ToString("N4") })");
            Console.Write(" ms, ");
            Console.ForegroundColor = highlight;
            Console.Write(fps.ToString("N4"));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            if (isRunningSlower) Console.Write($"({ previousFps.ToString("N4") })");
            Console.WriteLine(" fps");
            Console.ForegroundColor = color;

            File.WriteAllText(previousRunFile, time.ToString());
        }
    }
}
