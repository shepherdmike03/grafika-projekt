using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MyLavaRunner
{
    internal sealed class Model : IDisposable
    {
        // GPU data
        int _vao, _vbo, _cnt, _prog, _tintLoc;
        bool _ok;

        // properties
        public Vector3 Min { get; private set; } = new(float.MaxValue);
        public Vector3 Max { get; private set; } = new(float.MinValue);
        public Vector3 Tint { get; set; } = Vector3.One;

        // load model
        public void Load(string path)
        {
            if (_ok) return;

            // parse obj
            List<Vector3> v = new();
            List<Vector3> n = new();
            List<int> idx = new();

            foreach (string line in File.ReadLines(path))
            {
                string[] su = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (su.Length == 0) continue;

                if (su[0] == "v")
                {
                    Vector3 p = Parse(su);
                    v.Add(p);
                    Min = Vector3.ComponentMin(Min, p);
                    Max = Vector3.ComponentMax(Max, p);
                }
                else if (su[0] == "vn") n.Add(Parse(su));
                else if (su[0] == "f")
                {
                    int c = su.Length - 1;
                    for (int i = 1; i < c - 1; ++i)
                        AddTri(su[1], su[i + 1], su[i + 2], idx);
                }
            }

            // interleave data
            float[] data = new float[idx.Count / 2 * 6];
            for (int i = 0; i < idx.Count; i += 2)
            {
                Vector3 p = v[idx[i]];
                Vector3 nor = n[idx[i + 1]];
                int o = i / 2 * 6;
                data[o + 0] = p.X;
                data[o + 1] = p.Y;
                data[o + 2] = p.Z;
                data[o + 3] = nor.X;
                data[o + 4] = nor.Y;
                data[o + 5] = nor.Z;
            }

            _cnt = idx.Count / 2;

            // upload GPU
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float),
                data, BufferUsageHint.StaticDraw);

            const int s = 6 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, s, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, s, 3 * sizeof(float));

            // shader code
            const string vs =
                "#version 330 core\nlayout(location=0)in vec3 p;layout(location=1)in vec3 n;"
                + "uniform mat4 M,V,P;out vec3 N;void main(){N=mat3(transpose(inverse(M)))*n;"
                + "gl_Position=P*V*M*vec4(p,1);}";

            const string fs =
                "#version 330 core\nin vec3 N;out vec4 C;uniform vec3 Tint;"
                + "void main(){float d=max(dot(normalize(N),vec3(0,1,0)),0.25);"
                + "vec3 col=vec3(d)*Tint;C=vec4(col,1);}";

            _prog = Compile(vs, fs);
            _tintLoc = GL.GetUniformLocation(_prog, "Tint");
            _ok = true;

            // local helpers
            void AddTri(string a, string b, string c, List<int> list)
            {
                Add(a, list);
                Add(b, list);
                Add(c, list);
            }

            static void Add(string tok, List<int> list)
            {
                string[] p = tok.Split('/');
                list.Add(int.Parse(p[0]) - 1);
                list.Add(int.Parse(p[^1]) - 1);
            }
        }

        // draw model
        public void Draw(Matrix4 m, Matrix4 v, Matrix4 p)
        {
            if (!_ok) return;

            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "M"), false, ref m);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "V"), false, ref v);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref p);
            GL.Uniform3(_tintLoc, Tint);
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _cnt);
        }

        // parse line
        static Vector3 Parse(string[] s) => new(
            float.Parse(s[1], CultureInfo.InvariantCulture),
            float.Parse(s[2], CultureInfo.InvariantCulture),
            float.Parse(s[3], CultureInfo.InvariantCulture));

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

        // free GPU
        public void Dispose()
        {
            if (!_ok) return;
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GL.DeleteProgram(_prog);
            _ok = false;
        }
    }
}