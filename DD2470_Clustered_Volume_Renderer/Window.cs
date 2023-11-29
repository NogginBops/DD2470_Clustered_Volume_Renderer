using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Window : GameWindow
    {
        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        public List<Entity> Entities;

        protected override void OnLoad()
        {
            base.OnLoad();

            Entities = Model.LoadModel("C:/Users/juliu/source/repos/AerialRace/AerialRace/Assets/Models/Sponza/sponza.obj");
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);


        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.ClearColor(Color4.Coral);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            // FIXME: Loop through all entities and draw them.
            for (int i = 0; i < Entities.Count; i++)
            {
                Entity entity = Entities[i];

            }

            SwapBuffers();
        }
    }
}
