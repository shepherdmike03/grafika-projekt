// infinite side-walls
using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MyLavaRunner
{
    internal sealed class Walls : IDisposable
    {
        int  _vao, _vbo, _prog;
        bool _ok;

        // height 10, thickness 0 (single-sided quads on X = ±25 plane)
        public void Load()
        {
            if (_ok) return;

            float h = 10f, w = 25f, d = 25f;   // width Z half-extent

            float[] v =
            {
                // left wall  (x = -w)
                -w, 0, -d,   // tri 1
                -w, h, -d,
                -w, h,  d,

                -w, h,  d,   // tri 2
                -w, 0,  d,
                -w, 0, -d,

                // right wall (x = +w)
                 w, 0, -d,
                 w, h,  d,
                 w, h, -d,

                 w, h,  d,
                 w, 0, -d,
                 w, 0,  d
            };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, v.Length * sizeof(float), v, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            const string vs =
                "#version 330 core\nlayout(location=0)in vec3 p;"
              + "uniform mat4 M,V,P;void main(){gl_Position=P*V*M*vec4(p,1);}";

            const string fs =
                "#version 330 core\nout vec4 c;void main(){c=vec4(0.25,0.25,0.25,1);}";

            _prog = Compile(vs, fs);
            _ok   = true;
        }

        public void Draw(Matrix4 model, Matrix4 view, Matrix4 proj)
        {
            if (!_ok) return;
            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "M"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "V"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref proj);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 12);
            GL.BindVertexArray(0);
        }

        // helpers
        static int Compile(string v, string f)
        {
            int V = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(V, v); GL.CompileShader(V);
            int F = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(F, f); GL.CompileShader(F);
            int P = GL.CreateProgram();
            GL.AttachShader(P, V); GL.AttachShader(P, F); GL.LinkProgram(P);
            GL.DeleteShader(V);    GL.DeleteShader(F);
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
