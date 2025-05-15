

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
        const float SEG = 50f; // length of one platform section
        const float ROCKET_Y = 1.0f; // rocket’s vertical lift
        const float EXHAUST_Y = 1.0f; // exhaust vertical lift (a bit lower)

        Platform _plat;
        Walls _walls;
        PlayerController _player;
        Camera _cam;
        SkyBox _sky;

        Model _playerM, _rocketM, _currentM;
        bool _rocket; // true = rocket mode

        UIManager _ui;
        ParticleExplosion _boom;

        readonly Random _rng = new();

        // exhaust helper 
        float _exhaustTimer; // time accumulator for exhaust spawns

        public Game(GameWindowSettings gw, NativeWindowSettings nw) : base(gw, nw)
        {
        }

        
        protected override void OnLoad()
        {
            base.OnLoad();
            CursorState = CursorState.Grabbed;

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.ProgramPointSize);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
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
            _rocketM = new Model();
            _rocketM.Load("Resources/Models/rocket.obj");
            _currentM = _playerM;

            _ui = new UIManager();
            _ui.Load(Size.X, Size.Y);
            _boom = new ParticleExplosion();
            _boom.Load();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
            _ui.Resize(Size.X, Size.Y);
        }

        
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            // hot-keys 
            if (KeyboardState.IsKeyPressed(Keys.Escape)) Close();

            if (KeyboardState.IsKeyPressed(Keys.F))
                WindowState = WindowState == WindowState.Fullscreen
                    ? WindowState.Normal
                    : WindowState.Fullscreen;

            // quick test burst
            if (KeyboardState.IsKeyPressed(Keys.E))
                _boom.Spawn(_player.Pos + new Vector3(0,
                        _rocket ? EXHAUST_Y : 1f, 0),
                    _rng.Next(60, 100));

            // simulation 
            _player.Update(e, KeyboardState);

            _cam.Target = _player.Pos;
            _cam.Update(e, MouseState, IsFocused);

            float spd = _player.SpeedKmh;
            _ui.UpdateText($"Speed: {(int)spd} km/h");

            // rocket mode switch 
            bool wantRocket = spd >= 100f;
            if (wantRocket != _rocket)
            {
                _rocket = wantRocket;
                _currentM = _rocket ? _rocketM : _playerM;
                _boom.Spawn(_player.Pos + new Vector3(0,
                        _rocket ? EXHAUST_Y : 1f, 0),
                    _rng.Next(80, 120));
            }

            // continuous exhaust while in rocket mode 
            if (_rocket && _player.Velocity.LengthSquared > 1f)
            {
                _exhaustTimer += (float)e.Time;
                const float RATE = 1f / 30f; // 30 spawns / s
                while (_exhaustTimer >= RATE)
                {
                    _exhaustTimer -= RATE;

                    Vector3 dir = _player.Velocity;
                    dir.Y = 0;
                    if (dir.LengthSquared > 0)
                    {
                        dir.Normalize();
                        Vector3 pos = _player.Pos
                                      - dir * 1.5f
                                      + new Vector3(0, EXHAUST_Y, 0);
                        _boom.Spawn(pos, 6); // small puff
                    }
                }
            }
            else
            {
                _exhaustTimer = 0f;
            }

            _boom.Update((float)e.Time);
        }

        
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4 view = _cam.View;
            Matrix4 proj = _cam.Proj(Size.X / (float)Size.Y);

            _sky.Draw(view, proj);

            int baseIdx = (int)MathF.Floor(_player.Pos.Z / SEG);
            for (int i = -1; i <= 3; ++i)
            {
                float z = (baseIdx + i) * SEG;
                Matrix4 m = Matrix4.CreateTranslation(0, 0, z);
                _plat.Draw(m, view, proj);
                _walls.Draw(m, view, proj);
            }

            // draw the player / rocket 
            float scale = _rocket ? 0.5f : 0.05f; // rocket is 10× larger
            float yOffset = _rocket ? ROCKET_Y : 0f; // lift rocket

            Matrix4 mPlayer =
                Matrix4.CreateScale(scale) *
                (_rocket
                    ? Matrix4.Identity
                    : Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180))) *
                Matrix4.CreateTranslation(_player.Pos + new Vector3(0, yOffset, 0));

            _currentM.Draw(mPlayer, view, proj);

            _boom.Draw(view, proj);
            _ui.Draw();

            SwapBuffers();
        }

        
        protected override void OnUnload()
        {
            base.OnUnload();
            _plat.Dispose();
            _walls.Dispose();
            _playerM.Dispose();
            _rocketM.Dispose();
            _sky.Dispose();
            _ui.Dispose();
            _boom.Dispose();
        }
    }
}