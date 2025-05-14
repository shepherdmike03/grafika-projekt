// main loop

using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MyLavaRunner
{
    internal class Game : GameWindow
    {
        // world
        const float SEG = 50f;
        Platform _plat;
        Walls _walls;
        PlayerController _player;
        Model _playerM;
        Camera _cam;
        SkyBox _sky;

        public Game(GameWindowSettings gw, NativeWindowSettings nw) : base(gw, nw)
        {
        }

        // init
        protected override void OnLoad()
        {
            base.OnLoad();
            CursorState = CursorState.Grabbed;
            GL.Enable(EnableCap.DepthTest);
            GL.ClearColor(0.1f, 0.2f, 0.3f, 1);

            _cam = new Camera();
            _sky = new SkyBox();
            _sky.Load();
            _plat = new Platform();
            _plat.Load();
            _walls = new Walls();
            _walls.Load();

            _player = new PlayerController();
            _playerM = new Model();
            _playerM.Load("Resources/Models/player.obj");
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
        }

        // update
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            if (KeyboardState.IsKeyPressed(Keys.Escape)) Close();

            _player.Update(e, KeyboardState);
            _cam.Target = _player.Pos;
            _cam.Update(e, MouseState, IsFocused);
        }

        // draw
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4 view = _cam.View;
            Matrix4 proj = _cam.Proj(Size.X / (float)Size.Y);

            _sky.Draw(view, proj);

            int baseIdx = (int)MathF.Floor(_player.Pos.Z / SEG);
            for (int i = -1; i <= 3; i++)
            {
                float z = (baseIdx + i) * SEG;
                Matrix4 m = Matrix4.CreateTranslation(0, 0, z);
                _plat.Draw(m, view, proj);
                _walls.Draw(m, view, proj);
            }

            Matrix4 mPlayer =
                Matrix4.CreateScale(0.05f) *
                Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180)) *
                Matrix4.CreateTranslation(_player.Pos);
            _playerM.Draw(mPlayer, view, proj);

            GL.Enable(EnableCap.CullFace);
            SwapBuffers();
        }

        // quit
        protected override void OnUnload()
        {
            base.OnUnload();
            _plat.Dispose();
            _walls.Dispose();
            _playerM.Dispose();
            _sky.Dispose();
        }
    }
}