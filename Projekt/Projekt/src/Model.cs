// obj load

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
        int _vao, _vbo, _cnt, _prog;
        bool _ok;

        public void Load(string path)
        {
            if (_ok) return;

            // data lists
            List<Vector3> v = new(); // pos
            List<Vector3> n = new(); // nor
            List<int> idx = new(); // indices (v,n) pairs

            foreach (var line in File.ReadLines(path))
            {
                var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (s.Length == 0) continue;

                if (s[0] == "v") v.Add(Parse(s));
                if (s[0] == "vn") n.Add(Parse(s));

                if (s[0] == "f")
                {
                    // tokens after f
                    int c = s.Length - 1;
                    // triangulate fan
                    for (int i = 1; i < c - 1; i++)
                        AddTri(s[1], s[i + 1], s[i + 2], idx);
                }
            }

            // build vbo
            float[] data = new float[idx.Count / 2 * 6];
            for (int i = 0; i < idx.Count; i += 2)
            {
                Vector3 p = v[idx[i]];
                Vector3 nor = n[idx[i + 1]];
                int o = i / 2 * 6;
                data[o] = p.X;
                data[o + 1] = p.Y;
                data[o + 2] = p.Z;
                data[o + 3] = nor.X;
                data[o + 4] = nor.Y;
                data[o + 5] = nor.Z;
            }

            _cnt = idx.Count / 2;
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);

            const int stride = 6 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

            const string vs =
                "#version 330 core\nlayout(location=0)in vec3 p;layout(location=1)in vec3 n;"
                + "uniform mat4 M,V,P;out vec3 N;"
                + "void main(){N=mat3(transpose(inverse(M)))*n;gl_Position=P*V*M*vec4(p,1);}";

            const string fs =
                "#version 330 core\nin vec3 N;out vec4 C;"
                + "void main(){float d=max(dot(normalize(N),vec3(0,1,0)),0.2);C=vec4(d,d,d,1);}";

            _prog = Compile(vs, fs);
            _ok = true;

            // local helpers
            void AddTri(string a, string b, string c, List<int> list)
            {
                AddVert(a, list);
                AddVert(b, list);
                AddVert(c, list);
            }

            void AddVert(string tok, List<int> list)
            {
                var p = tok.Split('/');
                list.Add(int.Parse(p[0]) - 1); // pos
                list.Add(int.Parse(p[^1]) - 1); // nor
            }
        }

        public void Draw(Matrix4 m, Matrix4 v, Matrix4 p)
        {
            if (!_ok) return;
            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "M"), false, ref m);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "V"), false, ref v);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref p);
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _cnt);
            GL.BindVertexArray(0);
        }

        static Vector3 Parse(string[] s) => new(
            float.Parse(s[1], CultureInfo.InvariantCulture),
            float.Parse(s[2], CultureInfo.InvariantCulture),
            float.Parse(s[3], CultureInfo.InvariantCulture));

        static int Compile(string v, string f)
        {
            int V = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(V, v);
            GL.CompileShader(V);
            int F = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(F, f);
            GL.CompileShader(F);
            int P = GL.CreateProgram();
            GL.AttachShader(P, V);
            GL.AttachShader(P, F);
            GL.LinkProgram(P);
            GL.DeleteShader(V);
            GL.DeleteShader(F);
            return P;
        }

        public void Dispose()
        {
            if (!_ok) return;
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GL.DeleteProgram(_prog);
        }
    }
}