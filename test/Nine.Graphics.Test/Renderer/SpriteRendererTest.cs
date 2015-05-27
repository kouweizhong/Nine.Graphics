﻿namespace Nine.Graphics
{
    using System;
    using System.Threading.Tasks;
    using Xunit;

    public class SpriteRendererTest : TestBase
    {
        public static readonly TheoryData<Type, Type, string> Dimensions = new TheoryData<Type, Type, string>
        {
            { typeof(OpenGL.GraphicsHost), typeof(OpenGL.SpriteRenderer), "Content/Logo.png" },
        };

        [Theory]
        [MemberData(nameof(Dimensions))]
        public async Task draw_an_image(Type hostType, Type rendererType, string texture)
        {
            using (var host = Container.Get(hostType) as IGraphicsHost)
            {
                var renderer = Container.Get(rendererType) as IRenderer<Sprite>;
                renderer.Draw(new[] { new Sprite(texture) });

                host.GetTexture();
            }
        }
    }
}
