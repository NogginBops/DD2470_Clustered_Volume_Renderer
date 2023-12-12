using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Framebuffer
    {
        public int Handle;

        // FIXME: Mark relevant fields as nullable.
        // FIXME: Support multiple color attachments
        public Texture ColorAttachment0;

        public Texture DepthStencilAttachment;

        public Framebuffer(int handle, Texture colorAttachment0, Texture depthStencilAttachment)
        {
            Handle = handle;
            ColorAttachment0 = colorAttachment0;
            DepthStencilAttachment = depthStencilAttachment;
        }


        public static Framebuffer CreateHDRFramebuffer(string name, int width, int height)
        {
            GL.CreateFramebuffers(1, out int framebuffer);
            GL.ObjectLabel(ObjectLabelIdentifier.Framebuffer, framebuffer, -1, name);

            Texture colorTexture = Texture.CreateEmpty2D($"{name}_color", width, height, SizedInternalFormat.Rgba16f, false);
            // FIXME: Maybe change to 32F depth?
            Texture depthStencilTexture = Texture.CreateEmpty2D($"{name}_depth", width, height, SizedInternalFormat.Depth24Stencil8, false);

            GL.NamedFramebufferTexture(framebuffer, FramebufferAttachment.ColorAttachment0, colorTexture.Handle, 0);
            GL.NamedFramebufferTexture(framebuffer, FramebufferAttachment.DepthStencilAttachment, depthStencilTexture.Handle, 0);

            FramebufferStatus status = GL.CheckNamedFramebufferStatus(framebuffer, FramebufferTarget.Framebuffer);
            if (status != FramebufferStatus.FramebufferComplete)
            {
                Console.WriteLine($"Framebuffer incomplete: {status}");
            }

            return new Framebuffer(framebuffer, colorTexture, depthStencilTexture);
        }

        public static Framebuffer CreateHiZFramebuffer(string name, int width, int height)
        {
            GL.CreateFramebuffers(1, out int framebuffer);
            GL.ObjectLabel(ObjectLabelIdentifier.Framebuffer, framebuffer, -1, name);

            Texture hiz = Texture.CreateEmpty2D($"{name}_Hi-Z", width, height, SizedInternalFormat.R32f, true);

            GL.NamedFramebufferTexture(framebuffer, FramebufferAttachment.ColorAttachment0, hiz.Handle, 0);

            FramebufferStatus status = GL.CheckNamedFramebufferStatus(framebuffer, FramebufferTarget.Framebuffer);
            if (status != FramebufferStatus.FramebufferComplete)
            {
                Console.WriteLine($"Framebuffer incomplete: {status}");
            }

            return new Framebuffer(framebuffer, hiz, null!);
        }
    }
}
