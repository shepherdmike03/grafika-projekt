using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Projekt;
using ImgPF = System.Drawing.Imaging.PixelFormat;
using GlPF = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace MyLavaRunner
{
    internal sealed class Game : GameWindow
    {
        // constants
        const float SEG = 50f; // ground length
        const float ROCKET_Y = 1.0f; // rocket offset
        const float EXHAUST_Y = 1.0f; // exhaust offset

        // gameplay state
        const int MAX_LIVES = 3;
        const float SCORE_MULTIPLIER = 20f; // fast scoring
        int _lives = MAX_LIVES;
        int _score; // distance score
        float _prevZ; // prev Z

        // world parts
        readonly Platform _plat = new();
        readonly Walls _walls = new();
        readonly LavaFlow _lava = new();
        readonly PlayerController _player = new();
        readonly Camera _cam = new();
        readonly SkyBox _sky = new();
        readonly UIManager _ui = new();
        readonly ParticleExplosion _boom = new();
        readonly ObstacleController _obs = new();

        // models
        readonly Model _playerM = new();
        readonly Model _rocketM = new();
        Model _currentM;
        bool _rocket; // rocket mode

        // death banner
        int _deathVAO, _deathVBO, _deathTex, _deathProg;
        bool _deathReady;

        // other state
        readonly Random _rng = new();
        float _exhaustTimer;
        bool _dead;
        double _time; // global time

        // constructor
        public Game(GameWindowSettings gws, NativeWindowSettings nws)
            : base(gws, nws)
        {
        }

        // load
        protected override void OnLoad()
        {
            base.OnLoad();
            CursorState = CursorState.Grabbed;

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.ProgramPointSize);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.ClearColor(0.10f, 0.20f, 0.30f, 1f);

            _sky.Load();
            _plat.Load();
            _walls.Load();
            _lava.Load();
            _obs.Load();

            _playerM.Load("Resources/Models/player.obj");
            _rocketM.Load("Resources/Models/rocket.obj");
            _currentM = _playerM;

            _ui.Load(Size.X, Size.Y);
            _boom.Load();

            InitDeathBanner(Size.X, Size.Y);

            _lava.Reset(_player.Pos.Z + 30f);
            _prevZ = _player.Pos.Z;
        }

        // resize
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
            _ui.Resize(Size.X, Size.Y);
            UpdateDeathBanner(Size.X, Size.Y);
        }

        // update
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            _time += e.Time;

            var k = KeyboardState;
            if (k.IsKeyPressed(Keys.Escape))
                Close();

            if (k.IsKeyPressed(Keys.F))
                WindowState = WindowState == WindowState.Fullscreen
                    ? WindowState.Normal
                    : WindowState.Fullscreen;

            if (!_dead)
            {
                // update logic
                _player.Update(e, k);
                _lava.Update((float)e.Time);
                _obs.Update((float)e.Time, _player.Pos.Z);

                // update score
                float dz = _prevZ - _player.Pos.Z; // Z movement
                if (dz > 0)
                    _score += (int)(dz * SCORE_MULTIPLIER);
                _prevZ = _player.Pos.Z;

                // check collision
                if (_obs.CheckCollision(_player.Pos, out Vector3 hit))
                {
                    Vector3 col = _lives > 1 ? new(0f, .4f, 1f) : new(.8f, 0f, 1f);
                    _boom.Spawn(hit + new Vector3(0, 1, 0), 120, col);
                    _obs.RemoveAt(hit);
                    if (--_lives <= 0) Die();
                }

                // check lava
                if (_lava.Hits(_player.Pos))
                    Die();

                // update camera
                _cam.Target = _player.Pos;
                _cam.Update(e, MouseState, IsFocused);

                // update HUD
                UpdateHud();

                // rocket mode
                bool wantRocket = _player.SpeedKmh >= 100f;
                if (wantRocket != _rocket)
                {
                    _rocket = wantRocket;
                    _currentM = _rocket ? _rocketM : _playerM;
                    _boom.Spawn(_player.Pos + new Vector3(0, _rocket ? EXHAUST_Y : 1f, 0),
                        _rng.Next(80, 120));
                }

                // exhaust particles
                if (_rocket && _player.Velocity.LengthSquared > 1f)
                {
                    _exhaustTimer += (float)e.Time;
                    const float RATE = 1f / 30f;
                    while (_exhaustTimer >= RATE)
                    {
                        _exhaustTimer -= RATE;
                        Vector3 dir = _player.Velocity;
                        dir.Y = 0;
                        if (dir.LengthSquared > 0)
                        {
                            dir.Normalize();
                            Vector3 pos = _player.Pos - dir * 1.5f + new Vector3(0, EXHAUST_Y, 0);
                            _boom.Spawn(pos, 6);
                        }
                    }
                }
                else _exhaustTimer = 0f;
            }
            else if (k.IsKeyPressed(Keys.R))
                Respawn();

            _boom.Update((float)e.Time);
        }

        // render
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4 view = _cam.View;
            Matrix4 proj = _cam.Proj(Size.X / (float)Size.Y);

            // draw sky
            _sky.Draw(view, proj);

            // draw ground
            int baseIdx = (int)MathF.Floor(_player.Pos.Z / SEG);
            for (int i = -1; i <= 3; ++i)
            {
                float z = (baseIdx + i) * SEG;
                Matrix4 m = Matrix4.CreateTranslation(0, 0, z);
                _plat.Draw(m, view, proj);
                _walls.Draw(m, view, proj);
            }

            // draw world
            _obs.Draw(view, proj);
            _lava.Draw(view, proj, (float)_time);

            // draw player
            float scale = _rocket ? 0.5f : 0.05f;
            float yOffset = _rocket ? ROCKET_Y : 0f;
            Matrix4 mPlayer =
                Matrix4.CreateScale(scale) *
                (_rocket
                    ? Matrix4.Identity
                    : Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180))) *
                Matrix4.CreateTranslation(_player.Pos + new Vector3(0, yOffset, 0));

            _currentM.Draw(mPlayer, view, proj);

            // draw effects
            _boom.Draw(view, proj);
            _ui.Draw();

            if (_dead)
                DrawDeathBanner();

            SwapBuffers();
        }

        // unload
        protected override void OnUnload()
        {
            base.OnUnload();
            _plat.Dispose();
            _walls.Dispose();
            _lava.Dispose();
            _playerM.Dispose();
            _rocketM.Dispose();
            _sky.Dispose();
            _ui.Dispose();
            _boom.Dispose();
            _obs.Dispose();

            if (_deathReady)
            {
                GL.DeleteBuffer(_deathVBO);
                GL.DeleteVertexArray(_deathVAO);
                GL.DeleteTexture(_deathTex);
                GL.DeleteProgram(_deathProg);
            }
        }

        // HUD helper
        void UpdateHud()
        {
            _ui.UpdateText(
                $"Speed: {(int)_player.SpeedKmh} km/h\n" +
                $"Lives: {_lives}\n" +
                $"Score: {_score}"
            );
        }

        // death/respawn
        void Die()
        {
            _dead = true;
            _ui.UpdateText("");
        }

        void Respawn()
        {
            _dead = false;
            _lives = MAX_LIVES;
            _score = 0;
            _prevZ = _player.Pos.Z;
            _player.Respawn();
            _lava.Reset(_player.Pos.Z + 30f);
            _obs.Reset();
            UpdateHud();
        }

        // banner init
        void InitDeathBanner(int viewW, int viewH)
        {
            const int TW = 1024, TH = 128;

            // setup geo
            _deathVAO = GL.GenVertexArray();
            _deathVBO = GL.GenBuffer();
            GL.BindVertexArray(_deathVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _deathVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, 6 * 4 * sizeof(float),
                IntPtr.Zero, BufferUsageHint.DynamicDraw);

            const int S = 4 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, S, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, S, 2 * sizeof(float));

            // setup shader
            const string vs =
                "#version 330 core\nlayout(location=0)in vec2 p;layout(location=1)in vec2 uv;"
                + "out vec2 t;uniform mat4 P;void main(){t=uv;gl_Position=P*vec4(p,0,1);}";

            const string fs =
                "#version 330 core\nin vec2 t;out vec4 C;uniform sampler2D T;"
                + "void main(){C=texture(T,t);}";

            _deathProg = Compile(vs, fs);
            GL.UseProgram(_deathProg);
            GL.Uniform1(GL.GetUniformLocation(_deathProg, "T"), 0);

            // setup texture
            using var bmp = new Bitmap(TW, TH, ImgPF.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using var f = new Font("Impact", 64, FontStyle.Bold, GraphicsUnit.Pixel);
                string msg = "YOU  DIED   –   PRESS  R  TO  RESPAWN";
                SizeF sz = g.MeasureString(msg, f);
                g.DrawString(msg, f, Brushes.Red,
                    (TW - sz.Width) * 0.5f, (TH - sz.Height) * 0.5f);
            }

            _deathTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _deathTex);
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, TW, TH),
                ImageLockMode.ReadOnly, ImgPF.Format32bppArgb);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                TW, TH, 0, GlPF.Bgra, PixelType.UnsignedByte, d.Scan0);
            bmp.UnlockBits(d);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);

            UpdateDeathBanner(viewW, viewH);
            _deathReady = true;
        }

        // banner update
        void UpdateDeathBanner(int vw, int vh)
        {
            if (!_deathReady) return;

            const int TW = 1024, TH = 128;
            float x0 = (vw - TW) * 0.5f;
            float y0 = (vh - TH) * 0.5f;
            float x1 = x0 + TW;
            float y1 = y0 + TH;

            float[] quad =
            {
                x0, y0, 0, 0,
                x1, y0, 1, 0,
                x1, y1, 1, 1,
                x1, y1, 1, 1,
                x0, y1, 0, 1,
                x0, y0, 0, 0
            };

            GL.BindBuffer(BufferTarget.ArrayBuffer, _deathVBO);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                quad.Length * sizeof(float), quad);

            Matrix4 P = Matrix4.CreateOrthographicOffCenter(0, vw, vh, 0, -1, 1);
            GL.UseProgram(_deathProg);
            GL.UniformMatrix4(GL.GetUniformLocation(_deathProg, "P"), false, ref P);
        }

        // banner draw
        void DrawDeathBanner()
        {
            bool depth = GL.IsEnabled(EnableCap.DepthTest);
            bool cull = GL.IsEnabled(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.UseProgram(_deathProg);
            GL.BindTexture(TextureTarget.Texture2D, _deathTex);
            GL.BindVertexArray(_deathVAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            if (depth) GL.Enable(EnableCap.DepthTest);
            if (cull) GL.Enable(EnableCap.CullFace);
        }

        // compile shader
        static int Compile(string v, string f)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, v);
            GL.CompileShader(vs);
            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, f);
            GL.CompileShader(fs);
            int p = GL.CreateProgram();
            GL.AttachShader(p, vs);
            GL.AttachShader(p, fs);
            GL.LinkProgram(p);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            return p;
        }
    }
}
