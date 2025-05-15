//
// textured walls – single-sided quads on X = ±25 plane
//
using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ImgPF = System.Drawing.Imaging.PixelFormat;
using GlPF  = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace MyLavaRunner
{
    internal sealed class Walls : IDisposable
    {
        int  _vao, _vbo, _tex, _prog;
        bool _ok;

        // height 10, thickness 0
        public void Load(string tex = "Resources/Textures/wall_texture.jpg")
        {
            if (_ok) return;

            float h = 90f, w = 25f, d = 25f;        // width Z half-extent

            float[] v =
            {
                // left wall  (x = -w)         u v
                -w, 0, -d, 0, 0,
                -w, h, -d, 0, 1,
                -w, h,  d, 1, 1,

                -w, h,  d, 1, 1,
                -w, 0,  d, 1, 0,
                -w, 0, -d, 0, 0,

                // right wall (x = +w)
                 w, 0, -d, 1, 0,
                 w, h,  d, 0, 1,
                 w, h, -d, 1, 1,

                 w, h,  d, 0, 1,
                 w, 0, -d, 1, 0,
                 w, 0,  d, 0, 0
            };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, v.Length * sizeof(float), v, BufferUsageHint.StaticDraw);

            const int s = 5 * sizeof(float);
            GL.EnableVertexAttribArray(0);                          // pos
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, s, 0);
            GL.EnableVertexAttribArray(1);                          // uv
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, s, 3 * sizeof(float));

            const string vs =
                "#version 330 core\nlayout(location=0)in vec3 p;layout(location=1)in vec2 uv;"
              + "uniform mat4 M,V,P;out vec2 t;"
              + "void main(){t=uv*30.0;gl_Position=P*V*M*vec4(p,1);}"; // small repeat

            const string fs =
                "#version 330 core\nin vec2 t;out vec4 c;uniform sampler2D T;"
              + "void main(){c=texture(T,t);}";

            _prog = Compile(vs, fs);
            GL.UseProgram(_prog);
            GL.Uniform1(GL.GetUniformLocation(_prog, "T"), 0);      // texture unit 0

            _tex = LoadTexture(tex);
            _ok  = true;
        }

        public void Draw(Matrix4 M, Matrix4 V, Matrix4 P)
        {
            if (!_ok) return;

            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "M"), false, ref M);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "V"), false, ref V);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref P);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _tex);

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
            GL.DeleteShader(V); GL.DeleteShader(F);
            return P;
        }

        static int LoadTexture(string p)
        {
            int id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);
            using var bmp = new Bitmap(p);
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                 ImageLockMode.ReadOnly, ImgPF.Format32bppArgb);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                          bmp.Width, bmp.Height, 0, GlPF.Bgra,
                          PixelType.UnsignedByte, d.Scan0);
            bmp.UnlockBits(d);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                            (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                            (int)TextureMagFilter.Linear);
            return id;
        }

        public void Dispose()
        {
            if (!_ok) return;
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GL.DeleteTexture(_tex);
            GL.DeleteProgram(_prog);
        }
    }
}
