using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MyLavaRunner
{
    internal sealed class ParticleExplosion : IDisposable
    {
        struct P
        {
            public Vector3 Pos;
            public Vector3 Vel;
            public float Life; // 1 → 0
            public Vector3 Col;
        }

        const int MAX = 2048; // room for trail + bursts
        readonly List<P> _live = new();
        readonly Random _rng = new();

        int _vao, _vbo, _prog;
        bool _ok;

        public void Load()
        {
            if (_ok) return;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, MAX * 7 * sizeof(float),
                IntPtr.Zero, BufferUsageHint.DynamicDraw);

            int stride = 7 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));

            const string vs =
                "#version 330 core\nlayout(location=0)in vec3 p;layout(location=1)in vec3 clr;"
                + "layout(location=2)in float a;out vec3 vCol;out float vA;"
                + "uniform mat4 V,P;"
                + "void main(){vCol=clr;vA=a;gl_Position=P*V*vec4(p,1);"
                + "gl_PointSize = mix(20.0, 120.0, a);}";

            const string fs =
                "#version 330 core\nin vec3 vCol;in float vA;out vec4 C;"
                + "void main(){vec2 d=gl_PointCoord-vec2(.5);"
                + "float fade = 1.0-smoothstep(0.0,0.25,dot(d,d));"
                + "C=vec4(vCol, vA*fade);}";

            _prog = Compile(vs, fs);
            GL.Enable(EnableCap.ProgramPointSize);
            _ok = true;
        }

  
        public void Spawn(Vector3 origin, int num = 80)
        {
            num = Math.Clamp(num, 1, 100);
            for (int i = 0; i < num && _live.Count < MAX; ++i)
            {
                P p;
                p.Pos = origin;
                p.Vel = RandomDir() * (10 + (float)_rng.NextDouble() * 15);
                p.Life = 1f;
                p.Col = RandomColour();
                _live.Add(p);
            }
        }

        public void Update(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; --i)
            {
                var p = _live[i];
                p.Pos += p.Vel * dt;
                p.Vel += new Vector3(0, -9.8f, 0) * dt;
                p.Life -= dt;
                if (p.Life <= 0) _live.RemoveAt(i);
                else _live[i] = p;
            }
        }

        public void Draw(Matrix4 view, Matrix4 proj)
        {
            if (_live.Count == 0) return;

            Span<float> buf = stackalloc float[_live.Count * 7];
            for (int i = 0; i < _live.Count; ++i)
            {
                var p = _live[i];
                int o = i * 7;
                buf[o + 0] = p.Pos.X;
                buf[o + 1] = p.Pos.Y;
                buf[o + 2] = p.Pos.Z;
                buf[o + 3] = p.Col.X;
                buf[o + 4] = p.Col.Y;
                buf[o + 5] = p.Col.Z;
                buf[o + 6] = p.Life;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            unsafe
            {
                fixed (float* dataPtr = buf)
                {
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, buf.Length * sizeof(float), (IntPtr)dataPtr);
                }
            }

            bool depth = GL.IsEnabled(EnableCap.DepthTest);
            bool cull = GL.IsEnabled(EnableCap.CullFace);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "V"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref proj);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Points, 0, _live.Count);

            if (depth) GL.Enable(EnableCap.DepthTest);
            if (cull) GL.Enable(EnableCap.CullFace);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        public void Dispose()
        {
            if (!_ok) return;
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GL.DeleteProgram(_prog);
        }

        // helpers 
        Vector3 RandomDir()
        {
            float z = (float)(_rng.NextDouble() * 2 - 1);
            float a = (float)(_rng.NextDouble() * MathF.Tau);
            float r = MathF.Sqrt(1 - z * z);
            return new Vector3(r * MathF.Cos(a), z, r * MathF.Sin(a));
        }

        Vector3 RandomColour()
        {
            float t = (float)_rng.NextDouble();
            if (t < .25f) return new(1, .9f, 0);
            if (t < .5f) return new(1, .6f, 0);
            if (t < .75f) return new(1, .2f, 0);
            return new(.1f, .1f, .1f);
        }

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