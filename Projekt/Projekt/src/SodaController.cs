using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MyLavaRunner
{
    internal sealed class SodaController : IDisposable
    {
        // constants
        const float  SEG_LEN     = 50f;
        const int    MAX_ACTIVE  = 3;
        const float  X_RANGE     = 7.5f;
        const float  Y_POS       = 0.6f;
        const float  COLLIDE_RAD = 1.4f;
        const float  SPIN_SPEED  = 8.5f;
        const float  SCALE_CAN   = 0.12f;

        // halo glow
        const int    HALO_PIXELS = 120;

        // soda struct
        struct Soda
        {
            public Vector3 Pos;
            public float   Spin;
        }

        // state
        readonly List<Soda> _cans = new();
        readonly Random     _rng  = new();
        readonly Model      _mdl  = new();

        // cached player pos
        Vector3 _playerPos;

        // halo GPU data
        int  _haloVao, _haloVbo, _haloProg;
        bool _haloReady;

        // load assets
        public void Load()
        {
            _mdl.Load("Resources/Models/soda.obj");
            _mdl.Tint = new Vector3(1f, 0.2f, 1f);

            string vs =
                "#version 330 core\n" +
                "layout(location=0)in vec3 P;" +
                "uniform mat4 V;uniform mat4 Pm;" +
                "void main(){gl_Position=Pm*V*vec4(P,1);" +
                "gl_PointSize=" + HALO_PIXELS + ".0;}";

            string fs =
                "#version 330 core\n" +
                "in vec2 gl_PointCoord;out vec4 C;" +
                "void main(){vec2 d=gl_PointCoord-vec2(.5);" +
                "float r=length(d);" +
                "if(r>0.5)discard;" +
                "float a=(0.5-r)/0.5;" +
                "C=vec4(1.0,0.2,1.0,a*1.0);}";

            _haloProg = Compile(vs, fs);
            _haloVao  = GL.GenVertexArray();
            _haloVbo  = GL.GenBuffer();

            GL.BindVertexArray(_haloVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _haloVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, MAX_ACTIVE * 3 * sizeof(float),
                IntPtr.Zero, BufferUsageHint.DynamicDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            _haloReady = true;
        }

        // update logic
        public void Update(float dt, Vector3 playerPos, Camera cam)
        {
            _playerPos = playerPos;

            // animate + remove old
            for (int i = _cans.Count - 1; i >= 0; --i)
            {
                Soda s = _cans[i];
                s.Spin += SPIN_SPEED * dt;
                _cans[i] = s;
                if (s.Pos.Z > playerPos.Z + 10f)
                    _cans.RemoveAt(i);
            }

            // spawn new
            while (_cans.Count < MAX_ACTIVE)
            {
                float zMin = playerPos.Z - SEG_LEN;
                float zMax = playerPos.Z - SEG_LEN * 4f;
                float z    = Lerp(zMin, zMax, Rand());
                float x    = (Rand() * 2f - 1f) * X_RANGE;
                _cans.Add(new Soda { Pos = new Vector3(x, Y_POS, z), Spin = Rand() * MathF.Tau });
            }

            // update halo buffer
            if (_haloReady)
            {
                Span<float> buf = stackalloc float[_cans.Count * 3];
                for (int i = 0; i < _cans.Count; ++i)
                {
                    buf[i * 3 + 0] = _cans[i].Pos.X;
                    buf[i * 3 + 1] = _cans[i].Pos.Y + 0.05f;
                    buf[i * 3 + 2] = _cans[i].Pos.Z;
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, _haloVbo);
                unsafe
                {
                    fixed (float* p = buf)
                        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                            buf.Length * sizeof(float), (IntPtr)p);
                }
            }

            // send view/proj
            if (_haloReady)
            {
                GL.UseProgram(_haloProg);
                Matrix4 V  = cam.View;
                Matrix4 Pm = cam.Proj(cam.Aspect);
                GL.UniformMatrix4(GL.GetUniformLocation(_haloProg, "V"),  false, ref V);
                GL.UniformMatrix4(GL.GetUniformLocation(_haloProg, "Pm"), false, ref Pm);
            }
        }

        // check collection
        public bool CheckCollect(out Vector3 hit)
        {
            return CheckCollision(_playerPos, out hit);
        }

        // render all
        public void Draw(Matrix4 view, Matrix4 proj)
        {
            // draw cans
            foreach (Soda s in _cans)
            {
                Matrix4 m =
                    Matrix4.CreateScale(SCALE_CAN) *
                    Matrix4.CreateRotationX(-MathHelper.PiOver2) *
                    Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(10f)) *
                    Matrix4.CreateRotationY(s.Spin) *
                    Matrix4.CreateTranslation(s.Pos);

                _mdl.Draw(m, view, proj);
            }

            // draw halos
            if (_haloReady && _cans.Count > 0)
            {
                bool depth = GL.IsEnabled(EnableCap.DepthTest);
                GL.Disable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

                GL.UseProgram(_haloProg);
                GL.BindVertexArray(_haloVao);
                GL.DrawArrays(PrimitiveType.Points, 0, _cans.Count);

                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                if (depth) GL.Enable(EnableCap.DepthTest);
            }
        }

        // clear cans
        public void Reset() => _cans.Clear();

        // dispose resources
        public void Dispose()
        {
            _mdl.Dispose();
            if (_haloReady)
            {
                GL.DeleteBuffer(_haloVbo);
                GL.DeleteVertexArray(_haloVao);
                GL.DeleteProgram(_haloProg);
            }
        }

        // collision check
        bool CheckCollision(Vector3 p, out Vector3 hit)
        {
            for (int i = 0; i < _cans.Count; ++i)
            {
                if ((_cans[i].Pos - p).LengthSquared <= COLLIDE_RAD * COLLIDE_RAD)
                {
                    hit = _cans[i].Pos;
                    _cans.RemoveAt(i);
                    return true;
                }
            }
            hit = default;
            return false;
        }

        // random float
        float Rand() => (float)_rng.NextDouble();

        // linear interpolate
        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // compile shader
        static int Compile(string v, string f)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, v); GL.CompileShader(vs);
            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, f); GL.CompileShader(fs);
            int p  = GL.CreateProgram();
            GL.AttachShader(p, vs); GL.AttachShader(p, fs); GL.LinkProgram(p);
            GL.DeleteShader(vs); GL.DeleteShader(fs);
            return p;
        }
    }
}
