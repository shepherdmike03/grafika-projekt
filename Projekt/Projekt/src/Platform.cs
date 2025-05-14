// ground tile

using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ImgPF = System.Drawing.Imaging.PixelFormat;
using GlPF = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace MyLavaRunner
{
    internal sealed class Platform : IDisposable
    {
        int _vao, _vbo, _tex, _prog;
        bool _ok;

        public void Load(string tex = "Resources/Textures/platform_diffuse.png")
        {
            if (_ok) return;

            float[] v =
            {
                -25, 0, -25, 0, 0,
                25, 0, -25, 1, 0,
                25, 0, 25, 1, 1,
                25, 0, 25, 1, 1,
                -25, 0, 25, 0, 1,
                -25, 0, -25, 0, 0
            };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, v.Length * sizeof(float), v, BufferUsageHint.StaticDraw);
            const int s = 5 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, s, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, s, 3 * sizeof(float));

            const string vs =
                "#version 330 core\nlayout(location=0)in vec3 p;layout(location=1)in vec2 uv;"
                + "uniform mat4 M,V,P;out vec2 t;"
                + "void main(){t=uv*55.0;gl_Position=P*V*M*vec4(p,1);}"; // smaller tex

            const string fs =
                "#version 330 core\nin vec2 t;out vec4 c;uniform sampler2D T;"
                + "void main(){c=texture(T,t);}";

            _prog = Compile(vs, fs);
            GL.UseProgram(_prog);
            GL.Uniform1(GL.GetUniformLocation(_prog, "T"), 0);

            _tex = LoadTexture(tex);
            _ok = true;
        }

        public void Draw(Matrix4 m, Matrix4 v, Matrix4 p)
        {
            if (!_ok) return;
            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "M"), false, ref m);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "V"), false, ref v);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref p);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _tex);
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
        }

        // helpers
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

        static int LoadTexture(string p)
        {
            int id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);
            using var bmp = new Bitmap(p);
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly,
                ImgPF.Format32bppArgb);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0, GlPF.Bgra,
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